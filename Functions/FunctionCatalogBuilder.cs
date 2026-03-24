using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QaaS.Docs.Generator.Cli;

namespace QaaS.Docs.Generator.Functions;

internal sealed record FunctionCatalog(IReadOnlyList<FunctionEntry> Entries);

internal sealed record FunctionEntry(
    string Product,
    string Group,
    string Subgroup,
    string Kind,
    string DisplayName,
    string ShortName,
    string OverloadName,
    string Signature,
    string Summary,
    string Remarks,
    string RelativePath,
    int LineNumber,
    string DeclaringType,
    bool IsExtensionMethod,
    bool HasExplicitPlacement);

internal sealed record DocsPlacement(string Group, string Subgroup);

internal sealed record DocumentationComment(string Summary, string Remarks, DocsPlacement? Placement);

internal static class FunctionCatalogBuilder
{
    internal static readonly string[] SupportedProducts = ["Runner", "Mocker", "Framework"];

    public static async Task<FunctionCatalog> BuildAsync(
        string runnerRoot,
        string mockerRoot,
        string frameworkRoot)
    {
        var entries = new List<FunctionEntry>();

        foreach (var product in SupportedProducts)
        {
            var productRoot = ResolveProductRoot(product, runnerRoot, mockerRoot, frameworkRoot);
            foreach (var filePath in EnumerateSourceFiles(productRoot))
            {
                var text = await File.ReadAllTextAsync(filePath);
                var tree = CSharpSyntaxTree.ParseText(text, path: filePath);
                var root = await tree.GetRootAsync();

                foreach (var member in root.DescendantNodes()
                             .OfType<BaseMethodDeclarationSyntax>()
                             .Where(IsUserFacingMember)
                             .OrderBy(documentedMember => documentedMember.SpanStart))
                {
                    var documentation = DocumentationCommentParser.Parse(member);
                    var typeDeclaration = member.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                    if (typeDeclaration is null)
                    {
                        continue;
                    }

                    var isExtensionMethod = IsExtensionMethod(member);
                    if (!ShouldDocumentMember(product, member, documentation, isExtensionMethod))
                    {
                        continue;
                    }

                    var placement = ResolvePlacement(documentation, typeDeclaration, isExtensionMethod);
                    if (placement is null)
                    {
                        continue;
                    }

                    var lineNumber = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    entries.Add(new FunctionEntry(
                        product,
                        placement.Group,
                        placement.Subgroup,
                        GetMemberKind(member),
                        SignatureFormatter.FormatDisplayName(typeDeclaration, member),
                        SignatureFormatter.FormatShortName(typeDeclaration, member),
                        SignatureFormatter.FormatOverloadName(typeDeclaration, member),
                        SignatureFormatter.FormatComplete(member),
                        string.IsNullOrWhiteSpace(documentation.Summary)
                            ? "_No XML summary provided._"
                            : documentation.Summary,
                        documentation.Remarks,
                        Path.GetRelativePath(productRoot, filePath).Replace('\\', '/'),
                        lineNumber,
                        typeDeclaration.Identifier.Text,
                        isExtensionMethod,
                        documentation.Placement is not null));
                }
            }
        }

        return new FunctionCatalog(entries
            .OrderBy(entry => entry.Product, StringComparer.Ordinal)
            .ThenBy(entry => entry.Group, StringComparer.Ordinal)
            .ThenBy(entry => entry.Subgroup, StringComparer.Ordinal)
            .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ThenBy(entry => entry.LineNumber)
            .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
            .ToList());
    }

    private static IEnumerable<string> EnumerateSourceFiles(string productRoot)
    {
        return Directory.EnumerateFiles(productRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(productRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static bool IsIgnoredPath(string productRoot, string path)
    {
        var relativePath = Path.GetRelativePath(productRoot, path);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "TestResults", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "site", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "_isolated", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, "_tmp", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUserFacingMember(BaseMethodDeclarationSyntax member)
    {
        return member is MethodDeclarationSyntax or ConstructorDeclarationSyntax &&
               member.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) &&
               !IsInternalLeakingMember(member);
    }

    private static bool ShouldDocumentMember(
        string product,
        BaseMethodDeclarationSyntax member,
        DocumentationComment documentation,
        bool isExtensionMethod)
    {
        if (IsExcludedSerializerMember(member) || IsObsoleteMember(member))
        {
            return false;
        }

        if (documentation.Placement is not null)
        {
            return true;
        }

        return isExtensionMethod && HasRenderableDocumentation(documentation);
    }

    private static bool HasRenderableDocumentation(DocumentationComment documentation)
    {
        return !string.IsNullOrWhiteSpace(documentation.Summary) ||
               !string.IsNullOrWhiteSpace(documentation.Remarks);
    }

    private static bool IsExcludedSerializerMember(BaseMethodDeclarationSyntax member)
    {
        if (member is not MethodDeclarationSyntax method)
        {
            return false;
        }

        return method.Identifier.Text is "Read" or "Write";
    }

    private static bool IsObsoleteMember(MemberDeclarationSyntax member)
    {
        return member.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(attributeName =>
                attributeName.EndsWith("Obsolete", StringComparison.Ordinal) ||
                attributeName.EndsWith("ObsoleteAttribute", StringComparison.Ordinal));
    }

    private static DocsPlacement? ResolvePlacement(
        DocumentationComment documentation,
        TypeDeclarationSyntax typeDeclaration,
        bool isExtensionMethod)
    {
        if (documentation.Placement is not null)
        {
            return documentation.Placement;
        }

        return isExtensionMethod
            ? new DocsPlacement("Extension Methods", InferExtensionSubgroup(typeDeclaration.Identifier.Text))
            : null;
    }

    private static string InferExtensionSubgroup(string typeName)
    {
        var trimmedTypeName = typeName;
        if (trimmedTypeName.EndsWith("Extensions", StringComparison.Ordinal))
        {
            trimmedTypeName = trimmedTypeName[..^"Extensions".Length];
        }
        else if (trimmedTypeName.EndsWith("Extension", StringComparison.Ordinal))
        {
            trimmedTypeName = trimmedTypeName[..^"Extension".Length];
        }

        if (trimmedTypeName.Length > 1 &&
            trimmedTypeName[0] == 'I' &&
            char.IsUpper(trimmedTypeName[1]))
        {
            trimmedTypeName = trimmedTypeName[1..];
        }

        var subgroup = TypeDisplayFormatter.FormatSourceType(trimmedTypeName);
        return subgroup.EndsWith(" utils", StringComparison.Ordinal)
            ? subgroup[..^" utils".Length] + " utilities"
            : subgroup;
    }

    private static bool IsInternalLeakingMember(BaseMethodDeclarationSyntax member)
    {
        if (member is MethodDeclarationSyntax methodDeclaration &&
            methodDeclaration.Identifier.Text.Contains("Internal", StringComparison.Ordinal))
        {
            return true;
        }

        var typeNames = member switch
        {
            MethodDeclarationSyntax methodWithTypes => methodWithTypes.ParameterList.Parameters
                .Select(parameter => parameter.Type)
                .Prepend(methodWithTypes.ReturnType),
            ConstructorDeclarationSyntax constructorWithTypes => constructorWithTypes.ParameterList.Parameters
                .Select(parameter => parameter.Type),
            _ => Enumerable.Empty<TypeSyntax>()
        };

        return typeNames
            .Where(typeSyntax => typeSyntax is not null)
            .Select(typeSyntax => typeSyntax!.ToString())
            .Any(typeName => typeName.Contains("Internal", StringComparison.Ordinal));
    }

    private static bool IsExtensionMethod(BaseMethodDeclarationSyntax member)
    {
        return member is MethodDeclarationSyntax method &&
               method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)) &&
               method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ThisKeyword)) == true;
    }

    private static string GetMemberKind(BaseMethodDeclarationSyntax member)
    {
        return member is ConstructorDeclarationSyntax ? "constructor" : "function";
    }

    private static string ResolveProductRoot(
        string product,
        string runnerRoot,
        string mockerRoot,
        string frameworkRoot)
    {
        return product switch
        {
            "Runner" => runnerRoot,
            "Mocker" => mockerRoot,
            "Framework" => frameworkRoot,
            _ => throw new InvalidOperationException($"Unsupported function documentation product '{product}'.")
        };
    }
}

internal sealed class FunctionReferenceRenderer
{
    private static readonly string[] RunnerAndMockerGroupOrder = ["Builders", "Commands"];
    private static readonly string[] FrameworkGroupOrder = ["Builders", "Functions"];

    public IReadOnlyList<GeneratedDocument> Render(FunctionCatalog catalog)
    {
        var documents = new List<GeneratedDocument>();

        foreach (var product in FunctionCatalogBuilder.SupportedProducts)
        {
            var productEntries = catalog.Entries
                .Where(entry => string.Equals(entry.Product, product, StringComparison.Ordinal))
                .ToList();
            var renderedEntries = productEntries
                .Select(entry => new RenderedFunctionEntry(entry, ResolvePlacement(entry)))
                .ToList();
            var extensionEntries = productEntries
                .Where(entry => entry.IsExtensionMethod)
                .ToList();
            var renderedExplicitEntries = renderedEntries
                .Where(entry => entry.Entry.HasExplicitPlacement)
                .ToList();

            documents.Add(new GeneratedDocument(
                GetOverviewOutputPath(product),
                GeneratedDocumentHasher.WithHeader(
                    RenderOverviewPage(product, renderedExplicitEntries, extensionEntries),
                    [product, "functions", "overview"])));

            foreach (var category in renderedExplicitEntries
                         .GroupBy(entry => entry.Placement)
                         .OrderBy(group => GroupOrder(product, group.Key.DisplayGroup))
                         .ThenBy(group => group.Key.DisplayGroup, StringComparer.Ordinal)
                         .ThenBy(group => group.Key.Subgroup, StringComparer.Ordinal))
            {
                documents.Add(new GeneratedDocument(
                    GetCategoryOutputPath(product, category.Key),
                    GeneratedDocumentHasher.WithHeader(
                        RenderCategoryPage(product, category.Key, category.Select(entry => entry.Entry).ToList()),
                        [product, "functions", category.Key.DisplayGroup, category.Key.Subgroup])));
            }

            documents.Add(new GeneratedDocument(
                GetExtensionOutputPath(product),
                GeneratedDocumentHasher.WithHeader(
                    RenderExtensionPage(product, extensionEntries),
                    [product, "functions", "extension-methods"])));
        }

        return documents;
    }

    private static string GetOverviewOutputPath(string product) => $"{GetProductRoot(product)}/index.md";

    private static string GetCategoryOutputPath(string product, FunctionPagePlacement placement)
    {
        return placement.PathGroup is null
            ? $"{GetProductRoot(product)}/{Slugify(placement.Subgroup)}.md"
            : $"{GetProductRoot(product)}/{Slugify(placement.PathGroup)}/{Slugify(placement.Subgroup)}.md";
    }

    private static string GetExtensionOutputPath(string product) => $"{GetProductRoot(product)}/extension-methods.md";

    private static string GetProductRoot(string product)
    {
        return product switch
        {
            "Runner" => "qaas/functions",
            "Mocker" => "mocker/functions",
            "Framework" => "framework/functions",
            _ => $"{product.ToLowerInvariant()}/functions"
        };
    }

    private static string RenderOverviewPage(
        string product,
        IReadOnlyList<RenderedFunctionEntry> explicitlyDocumentedEntries,
        IReadOnlyList<FunctionEntry> extensionEntries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {product} Functions");
        builder.AppendLine();
        builder.AppendLine("This overview is generated from source-level `qaas-docs` annotations and the current public extension-method surface.");
        builder.AppendLine();
        builder.AppendLine("Each category page keeps the table of contents focused on short function names and collapses the location, signature, and XML doc comments behind each entry.");

        if (explicitlyDocumentedEntries.Count == 0 && extensionEntries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No user-facing functions are currently documented for this product.");
            return builder.ToString().TrimEnd();
        }

        foreach (var group in explicitlyDocumentedEntries
                     .GroupBy(entry => entry.Placement.DisplayGroup, StringComparer.Ordinal)
                     .OrderBy(group => GroupOrder(product, group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine($"## {group.Key}");
            builder.AppendLine();

            foreach (var subgroup in group
                         .GroupBy(entry => entry.Placement, FunctionPagePlacementComparer.Instance)
                         .OrderBy(subgroup => subgroup.Key.Subgroup, StringComparer.Ordinal))
            {
                builder.AppendLine(
                    $"- [{subgroup.Key.Subgroup}]({GetCategoryRelativeLink(subgroup.Key)})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Extension Methods");
        builder.AppendLine();
        builder.AppendLine("- [Extension Methods](extension-methods.md)");

        return builder.ToString().TrimEnd();
    }

    private static string RenderCategoryPage(
        string product,
        FunctionPagePlacement category,
        IReadOnlyList<FunctionEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {category.Subgroup}");
        builder.AppendLine();
        builder.AppendLine(
            $"Source-driven reference for `{product}` functions in the `{category.DisplayGroup} / {category.Subgroup}` category.");
        builder.AppendLine();
        builder.AppendLine("Each entry uses the short function name as the table-of-contents label. Expand an entry to inspect its location, signature, and XML doc comments.");

        RenderFunctionSections(builder, entries, headingLevel: 2);

        return builder.ToString().TrimEnd();
    }

    private static string RenderExtensionPage(string product, IReadOnlyList<FunctionEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Extension Methods");
        builder.AppendLine();
        builder.AppendLine($"This page collects public `{product}` extension methods that have XML documentation or explicit docs annotations.");

        if (entries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No user-facing extension methods are currently documented for this product.");
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine("Annotated extension methods continue to appear in their regular category pages; this page gives the extension surface a dedicated view.");

        var groups = entries
            .GroupBy(entry => entry.Group, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        var collapseTopLevelGroup = groups.Count == 1 &&
                                    string.Equals(groups[0].Key, "Extension Methods", StringComparison.Ordinal);

        foreach (var group in groups)
        {
            if (!collapseTopLevelGroup)
            {
                builder.AppendLine();
                builder.AppendLine($"## {group.Key}");
            }

            foreach (var subgroup in group
                         .GroupBy(entry => entry.Subgroup, StringComparer.Ordinal)
                         .OrderBy(subgroup => subgroup.Key, StringComparer.Ordinal))
            {
                builder.AppendLine();
                builder.AppendLine($"{new string('#', collapseTopLevelGroup ? 2 : 3)} {subgroup.Key}");
                RenderFunctionSections(builder, subgroup.ToList(), headingLevel: collapseTopLevelGroup ? 3 : 4);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void RenderFunctionSections(StringBuilder builder, IReadOnlyList<FunctionEntry> entries, int headingLevel)
    {
        var headingLabels = BuildHeadingLabels(entries);
        var headingPrefix = new string('#', headingLevel);

        foreach (var entry in entries)
        {
            builder.AppendLine();
            builder.AppendLine($"{headingPrefix} `{headingLabels[entry]}`");
            builder.AppendLine();
            builder.AppendLine("??? info \"Location, signature, and docstring\"");
            AppendIndentedLine(builder, "**Member**");
            AppendIndentedLine(builder, $"`{entry.DisplayName}`");
            AppendIndentedLine(builder);
            AppendIndentedLine(builder, $"**Kind** `{entry.Kind}`");
            AppendIndentedLine(builder);
            AppendIndentedLine(builder, $"**Declaring Type** `{GetDeclaringTypeLabel(entry)}`");
            AppendIndentedLine(builder);
            AppendIndentedLine(builder, $"**Location** `{entry.RelativePath}:{entry.LineNumber}`");
            AppendIndentedLine(builder);
            AppendIndentedLine(builder, "**Signature**");
            AppendIndentedLine(builder, "```csharp");
            AppendIndentedLine(builder, entry.Signature);
            AppendIndentedLine(builder, "```");
            AppendIndentedLine(builder);
            AppendIndentedLine(builder, "**Docstring**");
            AppendIndentedLine(builder);
            AppendIndentedMarkdownBlock(builder, entry.Summary, 4);

            if (!string.IsNullOrWhiteSpace(entry.Remarks))
            {
                AppendIndentedLine(builder);
                AppendIndentedMarkdownBlock(builder, entry.Remarks, 4);
            }
        }
    }

    private static IReadOnlyDictionary<FunctionEntry, string> BuildHeadingLabels(IReadOnlyList<FunctionEntry> entries)
    {
        return entries.ToDictionary(
            entry => entry,
            entry => entry.ShortName);
    }

    private static string GetDeclaringTypeLabel(FunctionEntry entry)
    {
        return entry.IsExtensionMethod
            ? $"{entry.DeclaringType} (extension type)"
            : entry.DeclaringType;
    }

    private static void AppendIndentedMarkdownBlock(StringBuilder builder, string text, int indentation)
    {
        var indent = new string(' ', indentation);
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.AppendLine(string.IsNullOrEmpty(line) ? indent : indent + line);
        }
    }

    private static void AppendIndentedLine(StringBuilder builder, string text = "", int indentation = 4)
    {
        var indent = new string(' ', indentation);
        builder.AppendLine(text.Length == 0 ? indent : indent + text);
    }

    private static string GetCategoryRelativeLink(string group, string subgroup)
    {
        return $"{Slugify(group)}/{Slugify(subgroup)}.md";
    }

    private static string GetCategoryRelativeLink(FunctionPagePlacement placement)
    {
        return placement.PathGroup is null
            ? $"{Slugify(placement.Subgroup)}.md"
            : GetCategoryRelativeLink(placement.PathGroup, placement.Subgroup);
    }

    private static int GroupOrder(string product, string group)
    {
        var orderedGroups = product switch
        {
            "Runner" or "Mocker" => RunnerAndMockerGroupOrder,
            "Framework" => FrameworkGroupOrder,
            _ => Array.Empty<string>()
        };

        var index = Array.FindIndex(orderedGroups, candidate => string.Equals(candidate, group, StringComparison.Ordinal));
        return index >= 0 ? index : orderedGroups.Length;
    }

    private static FunctionPagePlacement ResolvePlacement(FunctionEntry entry)
    {
        return entry.Product switch
        {
            "Runner" => ResolveRunnerPlacement(entry),
            "Mocker" => ResolveMockerPlacement(entry),
            "Framework" => ResolveFrameworkPlacement(entry),
            _ => new FunctionPagePlacement(entry.Group, entry.Group, entry.Subgroup)
        };
    }

    private static FunctionPagePlacement ResolveRunnerPlacement(FunctionEntry entry)
    {
        return entry.Group switch
        {
            "Configuration as Code" => new FunctionPagePlacement("Builders", "Builders", entry.Subgroup),
            "Getting Started" when string.Equals(entry.Subgroup, "Bootstrap", StringComparison.Ordinal)
                => new FunctionPagePlacement("Commands", "Commands", entry.Subgroup),
            "Runtime" when string.Equals(entry.Subgroup, "Runner", StringComparison.Ordinal)
                => new FunctionPagePlacement("Commands", "Commands", entry.Subgroup),
            _ => new FunctionPagePlacement(entry.Group, entry.Group, entry.Subgroup)
        };
    }

    private static FunctionPagePlacement ResolveMockerPlacement(FunctionEntry entry)
    {
        return entry.Group switch
        {
            "Configuration as Code" => new FunctionPagePlacement("Builders", "Builders", entry.Subgroup),
            "Getting Started" when string.Equals(entry.Subgroup, "Bootstrap", StringComparison.Ordinal)
                => new FunctionPagePlacement("Commands", "Commands", entry.Subgroup),
            "Runtime" => new FunctionPagePlacement("Commands", "Commands", entry.Subgroup),
            _ => new FunctionPagePlacement(entry.Group, entry.Group, entry.Subgroup)
        };
    }

    private static FunctionPagePlacement ResolveFrameworkPlacement(FunctionEntry entry)
    {
        return entry.Subgroup switch
        {
            "Data Sources" or "Policies" => new FunctionPagePlacement("Builders", "Builders", entry.Subgroup),
            _ => new FunctionPagePlacement("Functions", null, entry.Subgroup)
        };
    }

    private static string Slugify(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-");
        return normalized.Trim('-');
    }

    private static string CountLabel(int count, string singularNoun)
    {
        return count == 1 ? $"1 {singularNoun}" : $"{count} {singularNoun}s";
    }

    private sealed record RenderedFunctionEntry(FunctionEntry Entry, FunctionPagePlacement Placement);

    private sealed record FunctionPagePlacement(string DisplayGroup, string? PathGroup, string Subgroup) : IComparable<FunctionPagePlacement>
    {
        public int CompareTo(FunctionPagePlacement? other)
        {
            if (other is null)
            {
                return 1;
            }

            var pathGroupComparison = StringComparer.Ordinal.Compare(PathGroup ?? string.Empty, other.PathGroup ?? string.Empty);
            if (pathGroupComparison != 0)
            {
                return pathGroupComparison;
            }

            return StringComparer.Ordinal.Compare(Subgroup, other.Subgroup);
        }
    }

    private sealed class FunctionPagePlacementComparer : IEqualityComparer<FunctionPagePlacement>
    {
        public static readonly FunctionPagePlacementComparer Instance = new();

        public bool Equals(FunctionPagePlacement? x, FunctionPagePlacement? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.DisplayGroup, y.DisplayGroup, StringComparison.Ordinal) &&
                   string.Equals(x.PathGroup, y.PathGroup, StringComparison.Ordinal) &&
                   string.Equals(x.Subgroup, y.Subgroup, StringComparison.Ordinal);
        }

        public int GetHashCode(FunctionPagePlacement obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.DisplayGroup),
                obj.PathGroup is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.PathGroup),
                StringComparer.Ordinal.GetHashCode(obj.Subgroup));
        }
    }
}

internal static class DocumentationCommentParser
{
    public static DocumentationComment Parse(MemberDeclarationSyntax member)
    {
        var xml = ParseXml(member);
        if (xml is null)
        {
            return new DocumentationComment(string.Empty, string.Empty, null);
        }

        var placementElement = xml.Descendants("qaas-docs").FirstOrDefault();
        var group = NormalizeAttribute(placementElement?.Attribute("group")?.Value);
        var subgroup = NormalizeAttribute(placementElement?.Attribute("subgroup")?.Value);
        var placement = !string.IsNullOrWhiteSpace(group) && !string.IsNullOrWhiteSpace(subgroup)
            ? new DocsPlacement(group, subgroup)
            : null;

        return new DocumentationComment(
            RenderBlock(xml.Descendants("summary").FirstOrDefault()),
            RenderBlock(xml.Descendants("remarks").FirstOrDefault()),
            placement);
    }

    public static XDocument? ParseXml(MemberDeclarationSyntax member)
    {
        var trivia = member.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();
        if (trivia is null)
        {
            return null;
        }

        try
        {
            var normalizedXml = NormalizeDocumentationTrivia(trivia.ToFullString());
            return XDocument.Parse("<root>" + normalizedXml + "</root>");
        }
        catch
        {
            return null;
        }
    }

    private static string RenderBlock(XElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        var paragraphs = ExtractParagraphs(element)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            paragraphs);
    }

    private static IEnumerable<string> ExtractParagraphs(XElement element)
    {
        var paragraphElements = element.Elements()
            .Where(child => child.Name.LocalName == "para")
            .ToList();
        if (paragraphElements.Count == 0)
        {
            yield return FlattenNodes(element.Nodes());
            yield break;
        }

        var leadingNodes = element.Nodes()
            .Where(node => node is not XElement child || child.Name.LocalName != "para");
        var leadingParagraph = FlattenNodes(leadingNodes);
        if (!string.IsNullOrWhiteSpace(leadingParagraph))
        {
            yield return leadingParagraph;
        }

        foreach (var paragraph in paragraphElements)
        {
            var text = FlattenNodes(paragraph.Nodes());
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
        }
    }

    private static string FlattenNodes(IEnumerable<XNode> nodes)
    {
        var text = string.Join(" ", nodes
            .SelectMany(FlattenNode)
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    private static IEnumerable<string> FlattenNode(XNode node)
    {
        switch (node)
        {
            case XText text:
                yield return text.Value.Trim();
                yield break;
            case XElement element:
                if (element.Name.LocalName is "see" or "seealso")
                {
                    var cref = (string?)element.Attribute("cref");
                    if (!string.IsNullOrWhiteSpace(cref))
                    {
                        yield return SimplifyReference(cref);
                    }

                    var langword = (string?)element.Attribute("langword");
                    if (!string.IsNullOrWhiteSpace(langword))
                    {
                        yield return langword;
                    }

                    var href = (string?)element.Attribute("href");
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        yield return href;
                    }

                    yield break;
                }

                if (element.Name.LocalName == "paramref")
                {
                    var name = (string?)element.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        yield return name;
                    }

                    yield break;
                }

                foreach (var nested in element.Nodes().SelectMany(FlattenNode))
                {
                    yield return nested;
                }

                yield break;
        }
    }

    private static string NormalizeDocumentationTrivia(string rawDocumentation)
    {
        return string.Join(
            Environment.NewLine,
            rawDocumentation
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => Regex.Replace(line, "^\\s*///\\s?", string.Empty)));
    }

    private static string NormalizeAttribute(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, "\\s+", " ").Trim();
    }

    private static string SimplifyReference(string reference)
    {
        var trimmed = reference.Length > 2 && reference[1] == ':'
            ? reference[2..]
            : reference;
        var lastSegment = trimmed.Split('.').LastOrDefault();
        return string.IsNullOrWhiteSpace(lastSegment) ? trimmed : lastSegment;
    }
}

internal static class SignatureFormatter
{
    public static string FormatDisplayName(TypeDeclarationSyntax typeDeclaration, BaseMethodDeclarationSyntax member)
    {
        var parameters = string.Join(", ", member.ParameterList.Parameters.Select(parameter => parameter.ToString()));

        return member switch
        {
            MethodDeclarationSyntax method => $"{typeDeclaration.Identifier.Text}.{method.Identifier.Text}{method.TypeParameterList}({parameters})",
            ConstructorDeclarationSyntax => $"{typeDeclaration.Identifier.Text}({parameters})",
            _ => throw new InvalidOperationException($"Unsupported member type '{member.GetType().Name}' for function display name rendering.")
        };
    }

    public static string FormatShortName(TypeDeclarationSyntax typeDeclaration, BaseMethodDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => $"{method.Identifier.Text}{method.TypeParameterList}",
            ConstructorDeclarationSyntax => typeDeclaration.Identifier.Text,
            _ => throw new InvalidOperationException($"Unsupported member type '{member.GetType().Name}' for function short name rendering.")
        };
    }

    public static string FormatOverloadName(TypeDeclarationSyntax typeDeclaration, BaseMethodDeclarationSyntax member)
    {
        var parameters = string.Join(", ", member.ParameterList.Parameters.Select(FormatParameterType));

        return member switch
        {
            MethodDeclarationSyntax method => $"{method.Identifier.Text}{method.TypeParameterList}({parameters})",
            ConstructorDeclarationSyntax => $"{typeDeclaration.Identifier.Text}({parameters})",
            _ => throw new InvalidOperationException($"Unsupported member type '{member.GetType().Name}' for overload heading rendering.")
        };
    }

    public static string FormatComplete(BaseMethodDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => NormalizeSignature(
                method
                    .WithBody(null)
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)),
            ConstructorDeclarationSyntax constructor => NormalizeSignature(
                constructor
                    .WithBody(null)
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)),
            _ => throw new InvalidOperationException($"Unsupported member type '{member.GetType().Name}' for function signature rendering.")
        };
    }

    private static string FormatParameterType(ParameterSyntax parameter)
    {
        var modifiers = string.Join(" ", parameter.Modifiers.Select(modifier => modifier.Text));
        var type = parameter.Type?.ToString() ?? "object";
        return string.IsNullOrWhiteSpace(modifiers) ? type : $"{modifiers} {type}";
    }

    private static string NormalizeSignature(MemberDeclarationSyntax member)
    {
        return Regex.Replace(
                member
                    .WithoutLeadingTrivia()
                    .WithoutTrailingTrivia()
                    .NormalizeWhitespace()
                    .ToFullString(),
                "\\s+",
                " ")
            .Trim();
    }
}
