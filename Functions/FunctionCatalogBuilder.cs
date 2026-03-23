using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QaaS.Docs.Generator.Functions;

internal sealed record FunctionCatalog(IReadOnlyList<FunctionEntry> Entries);

internal sealed record FunctionEntry(
    string Product,
    string Group,
    string Subgroup,
    string Kind,
    string DisplayName,
    string Signature,
    string Summary,
    string Remarks,
    string RelativePath,
    int LineNumber);

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
                    if (documentation.Placement is null)
                    {
                        continue;
                    }

                    var typeDeclaration = member.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                    if (typeDeclaration is null)
                    {
                        continue;
                    }

                    var lineNumber = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    entries.Add(new FunctionEntry(
                        product,
                        documentation.Placement.Group,
                        documentation.Placement.Subgroup,
                        GetMemberKind(member),
                        SignatureFormatter.FormatDisplayName(typeDeclaration, member),
                        SignatureFormatter.FormatComplete(member),
                        string.IsNullOrWhiteSpace(documentation.Summary)
                            ? "_No XML summary provided._"
                            : documentation.Summary,
                        documentation.Remarks,
                        Path.GetRelativePath(productRoot, filePath).Replace('\\', '/'),
                        lineNumber));
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
    public IReadOnlyList<GeneratedDocument> Render(FunctionCatalog catalog)
    {
        return FunctionCatalogBuilder.SupportedProducts
            .Select(product => new GeneratedDocument(
                GetOutputPath(product),
                GeneratedDocumentHasher.WithHeader(
                    RenderProductPage(
                        product,
                        catalog.Entries
                            .Where(entry => string.Equals(entry.Product, product, StringComparison.Ordinal))
                            .ToList()),
                    [product, "functions"])))
            .ToList();
    }

    private static string GetOutputPath(string product)
    {
        return product switch
        {
            "Runner" => "qaas/functions/index.md",
            "Mocker" => "mocker/functions/index.md",
            "Framework" => "framework/functions/index.md",
            _ => $"{product.ToLowerInvariant()}/functions/index.md"
        };
    }

    private static string RenderProductPage(string product, IReadOnlyList<FunctionEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {product} Functions");
        builder.AppendLine();
        builder.AppendLine("This page is generated from source-level `qaas-docs` annotations and the current source tree.");

        if (entries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No functions are currently annotated for this product.");
            return builder.ToString().TrimEnd();
        }

        foreach (var group in entries.GroupBy(entry => entry.Group, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine($"## {group.Key}");

            foreach (var subgroup in group.GroupBy(entry => entry.Subgroup, StringComparer.Ordinal))
            {
                builder.AppendLine();
                builder.AppendLine($"### {subgroup.Key}");

                foreach (var entry in subgroup)
                {
                    builder.AppendLine();
                    builder.AppendLine("<hr class=\"function-separator\" />");
                    builder.AppendLine();
                    builder.AppendLine($"#### `{entry.DisplayName}`");
                    builder.AppendLine();
                    builder.AppendLine($"**Location** `{entry.RelativePath}:{entry.LineNumber}`");
                    builder.AppendLine();
                    builder.AppendLine("**Complete Signature**");
                    builder.AppendLine("```csharp");
                    builder.AppendLine(entry.Signature);
                    builder.AppendLine("```");
                    builder.AppendLine();
                    builder.AppendLine("**Docstring**");
                    builder.AppendLine();
                    AppendMarkdownBlock(builder, entry.Summary);

                    if (!string.IsNullOrWhiteSpace(entry.Remarks))
                    {
                        builder.AppendLine();
                        AppendMarkdownBlock(builder, entry.Remarks);
                    }
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendMarkdownBlock(StringBuilder builder, string text)
    {
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.AppendLine(line);
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
