using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NJsonSchema;
using QaaS.Docs.Generator.Cli;
using QaaS.Docs.Generator.Functions;

namespace QaaS.Docs.Generator.Hooks;

internal sealed class HookReferenceRenderer
{
    private const string GeneratedCatalogStart = "<!-- generated hook catalog start -->";
    private const string GeneratedCatalogEnd = "<!-- generated hook catalog end -->";

    private static readonly IReadOnlyDictionary<string, HookKindSpec> KindSpecs =
        new Dictionary<string, HookKindSpec>(StringComparer.Ordinal)
        {
            ["assertion"] = new("assertions/availableAssertions", "assertions/index.md", "Assertions", "QaaS.Common.Assertions"),
            ["generator"] = new("generators/availableGenerators", "generators/index.md", "Generators", "QaaS.Common.Generators"),
            ["probe"] = new("probes/availableProbes", "probes/index.md", "Probes", "QaaS.Common.Probes"),
            ["processor"] = new("processors/availableProcessors", "processors/index.md", "Processors", "QaaS.Common.Processors")
        };

    public async Task<IReadOnlyList<GeneratedDocument>> RenderAsync(string docsRoot, string mirrorRoot)
    {
        var workspaceRoot = ResolveWorkspaceRoot(docsRoot, mirrorRoot);
        var catalog = await LoadCatalogAsync(mirrorRoot);
        var documents = new List<GeneratedDocument>();

        foreach (var kind in KindSpecs.Keys.OrderBy(candidate => candidate, StringComparer.Ordinal))
        {
            var spec = KindSpecs[kind];
            var groupedHooks = new List<HookIndexEntry>();
            var hooksRoot = Path.Combine(
                docsRoot,
                "docs",
                spec.DocsRoot.Replace('/', Path.DirectorySeparatorChar));

            var sourceRoot = Path.Combine(workspaceRoot, spec.RepositoryDirectory);
            var docsSlugs = EnumerateDocsSlugs(hooksRoot, catalog, kind);
            foreach (var docsSlug in docsSlugs)
            {
                var hookDirectory = Path.Combine(hooksRoot, docsSlug);
                var overviewPath = Path.Combine(hookDirectory, "overview.md");
                var existingOverviewBody = await LoadExistingOverviewBodyAsync(overviewPath);
                var documentation = await LoadHookDocumentationAsync(sourceRoot, docsSlug);
                var summary = documentation.Summary;
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = existingOverviewBody;
                }

                if (string.IsNullOrWhiteSpace(summary))
                {
                    throw new InvalidOperationException(
                        $"Hook '{docsSlug}' in '{kind}' is missing a public XML summary in {sourceRoot}.");
                }

                var customOverviewContent = ExtractCustomOverviewContent(existingOverviewBody, summary);
                var placement = documentation.Placement ?? InferPlacement(kind, docsSlug);
                groupedHooks.Add(new HookIndexEntry(docsSlug, summary, placement.Group, placement.Subgroup));

                documents.Add(new GeneratedDocument(
                    $"{spec.DocsRoot}/{docsSlug}/overview.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderOverviewPage(docsSlug, summary, customOverviewContent),
                        [kind, docsSlug, "overview", placement.Group, placement.Subgroup])));

                if (!catalog.TryGetValue($"{kind}|{docsSlug}", out var hookCatalogEntry))
                {
                    if (string.Equals(kind, "processor", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Could not find a hook catalog entry for documented hook '{docsSlug}' in '{kind}'.");
                }

                var hookSchema = await LoadHookSchemaAsync(mirrorRoot, hookCatalogEntry);
                documents.Add(new GeneratedDocument(
                    $"{spec.DocsRoot}/{docsSlug}/configuration/tableView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderTableView(docsSlug, hookSchema),
                        [hookCatalogEntry.FamilyId, docsSlug, "table-view"])));
                documents.Add(new GeneratedDocument(
                    $"{spec.DocsRoot}/{docsSlug}/configuration/yamlView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderYamlView(docsSlug, hookSchema),
                        [hookCatalogEntry.FamilyId, docsSlug, "yaml-view"])));
            }

            if (groupedHooks.Count != 0)
            {
                var indexFullPath = Path.Combine(
                    docsRoot,
                    "docs",
                    spec.IndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var existingIndexContent = File.Exists(indexFullPath)
                    ? await File.ReadAllTextAsync(indexFullPath)
                    : $"# {spec.DisplayTitle}";

                documents.Add(new GeneratedDocument(
                    spec.IndexRelativePath,
                    GeneratedDocumentHasher.WithHeader(
                        RenderIndexPage(kind, spec, existingIndexContent, groupedHooks),
                        [kind, "index"])));
            }
        }

        return documents;
    }

    private static string ResolveWorkspaceRoot(string docsRoot, string mirrorRoot)
    {
        foreach (var candidate in EnumerateWorkspaceRootCandidates(docsRoot, mirrorRoot))
        {
            if (KindSpecs.Values.All(spec => Directory.Exists(Path.Combine(candidate, spec.RepositoryDirectory))))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve a workspace root containing {string.Join(", ", KindSpecs.Values.Select(spec => spec.RepositoryDirectory))} from docs root '{docsRoot}' and mirror root '{mirrorRoot}'.");
    }

    private static IEnumerable<string> EnumerateWorkspaceRootCandidates(string docsRoot, string mirrorRoot)
    {
        static IEnumerable<string> Expand(string path)
        {
            var current = new DirectoryInfo(path);
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }

        return Expand(docsRoot)
            .Concat(Expand(mirrorRoot))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, HookCatalogEntry>> LoadCatalogAsync(string mirrorRoot)
    {
        var entries = new Dictionary<string, HookCatalogEntry>(StringComparer.Ordinal);

        foreach (var familyId in new[] { "runner-family", "mocker-family" })
        {
            var catalogPath = Path.Combine(mirrorRoot, "schemas", familyId, "latest", "hook-catalog.json");
            await using var stream = File.OpenRead(catalogPath);
            var catalog = await JsonSerializer.DeserializeAsync<HookCatalogFile>(stream, JsonDefaults.Options)
                          ?? throw new InvalidOperationException($"Could not deserialize hook catalog from {catalogPath}.");

            foreach (var hookType in catalog.HookTypes)
            {
                if (!KindSpecs.ContainsKey(hookType.HookKind))
                {
                    continue;
                }

                var docsSlug = NormalizeDocsSlug(string.IsNullOrWhiteSpace(hookType.DocsSlug) ? hookType.Title : hookType.DocsSlug);
                if (string.IsNullOrWhiteSpace(docsSlug))
                {
                    continue;
                }

                entries[$"{hookType.HookKind}|{docsSlug}"] =
                    new HookCatalogEntry(hookType.HookKind, docsSlug, familyId, hookType.ConfigurationSchemaJsonPointer);
            }
        }

        return entries;
    }

    private static async Task<HookDocumentation> LoadHookDocumentationAsync(string sourceRoot, string docsSlug)
    {
        var sourceFile = FindHookSourceFile(sourceRoot, docsSlug);
        if (sourceFile is null)
        {
            return new HookDocumentation(null, null);
        }

        var text = await File.ReadAllTextAsync(sourceFile);
        return ParseHookDocumentation(text, sourceFile, docsSlug);
    }

    private static IReadOnlyList<string> EnumerateDocsSlugs(
        string hooksRoot,
        IReadOnlyDictionary<string, HookCatalogEntry> catalog,
        string kind)
    {
        var catalogSlugs = catalog.Values
            .Where(entry => string.Equals(entry.Kind, kind, StringComparison.Ordinal))
            .Select(entry => entry.DocsSlug);
        var existingSlugs = Directory.Exists(hooksRoot)
            ? Directory.EnumerateDirectories(hooksRoot)
                .Select(Path.GetFileName)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Cast<string>()
            : [];

        return catalogSlugs
            .Concat(existingSlugs)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .ToList();
    }

    internal static HookDocumentation ParseHookDocumentation(string sourceText, string sourceFile, string docsSlug)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: sourceFile);
        var root = syntaxTree.GetRoot();
        var candidates = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(candidate => string.Equals(candidate.Identifier.Text, docsSlug, StringComparison.Ordinal))
            .Select(candidate => (Type: candidate, Documentation: DocumentationCommentParser.Parse(candidate)))
            .ToList();

        if (candidates.Count == 0)
        {
            return new HookDocumentation(null, null);
        }

        var bestCandidate = candidates
            .OrderByDescending(candidate => candidate.Documentation.Placement is not null)
            .ThenByDescending(candidate => !string.IsNullOrWhiteSpace(candidate.Documentation.Summary))
            .ThenBy(candidate => candidate.Type.TypeParameterList?.Parameters.Count ?? 0)
            .First();

        var summary = string.IsNullOrWhiteSpace(bestCandidate.Documentation.Summary)
            ? null
            : bestCandidate.Documentation.Summary;
        return new HookDocumentation(summary, bestCandidate.Documentation.Placement);
    }

    private static string? FindHookSourceFile(string sourceRoot, string docsSlug)
    {
        return Directory.EnumerateFiles(sourceRoot, $"{docsSlug}.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static async Task<string?> LoadExistingOverviewBodyAsync(string overviewPath)
    {
        if (!File.Exists(overviewPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(overviewPath);
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var lines = normalized.Split('\n').ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        if (lines[0].StartsWith("<!-- generated hash:", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count != 0 && lines[0].StartsWith("# ", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        const string generatedOverviewFooter = "_This overview is generated automatically from the hook source summary._";
        if (lines.Count != 0 && string.Equals(lines[^1], generatedOverviewFooter, StringComparison.Ordinal))
        {
            lines.RemoveAt(lines.Count - 1);

            while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        lines = lines
            .Where(line => !line.StartsWith("> Logical group:", StringComparison.Ordinal))
            .ToList();

        return lines.Count == 0
            ? null
            : string.Join(GeneratedDocumentLineEndings.Canonical, lines).Trim();
    }

    private static bool IsIgnoredPath(string sourceRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Tests", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Test", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "TestResults", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HookSchemaDocument> LoadHookSchemaAsync(string mirrorRoot, HookCatalogEntry hookCatalogEntry)
    {
        var schemaPath = Path.Combine(mirrorRoot, "schemas", hookCatalogEntry.FamilyId, "latest", "schema.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(schemaPath))
                   ?? throw new InvalidOperationException($"Could not parse schema json from {schemaPath}.");

        var schemaNode = ResolveJsonPointer(root, hookCatalogEntry.ConfigurationSchemaJsonPointer);
        var rootName = GetLeafPointerSegment(hookCatalogEntry.ConfigurationSchemaJsonPointer);
        var parentNode = ResolveJsonPointer(root, GetParentPointer(hookCatalogEntry.ConfigurationSchemaJsonPointer));
        var required = parentNode["required"] is JsonArray requiredArray &&
                       requiredArray.Any(value => string.Equals(value?.GetValue<string>(), rootName, StringComparison.Ordinal));

        var schema = await JsonSchema.FromJsonAsync(schemaNode.ToJsonString());
        return new HookSchemaDocument(rootName, required, schema);
    }

    private static JsonNode ResolveJsonPointer(JsonNode root, string pointer)
    {
        var current = root;
        foreach (var segment in GetPointerSegments(pointer))
        {
            current = current switch
            {
                JsonObject jsonObject when jsonObject.TryGetPropertyValue(segment, out var child) && child is not null => child,
                JsonArray jsonArray when int.TryParse(segment, out var index) && jsonArray[index] is not null => jsonArray[index]!,
                _ => throw new InvalidOperationException($"Could not resolve JSON pointer '{pointer}'.")
            };
        }

        return current;
    }

    private static IEnumerable<string> GetPointerSegments(string pointer)
    {
        if (string.IsNullOrWhiteSpace(pointer) || string.Equals(pointer, "#", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        return pointer
            .TrimStart('#')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
    }

    private static string GetParentPointer(string pointer)
    {
        var segments = GetPointerSegments(pointer).ToList();
        if (segments.Count <= 1)
        {
            return "#";
        }

        return "#/" + string.Join("/", segments.Take(segments.Count - 1));
    }

    private static string GetLeafPointerSegment(string pointer)
    {
        return GetPointerSegments(pointer).Last();
    }

    private static string NormalizeDocsSlug(string value)
    {
        var trimmed = value.Trim();
        var genericMarkerIndex = trimmed.IndexOf('`', StringComparison.Ordinal);
        return genericMarkerIndex >= 0 ? trimmed[..genericMarkerIndex] : trimmed;
    }

    private static string? ExtractCustomOverviewContent(string? existingOverviewBody, string summary)
    {
        if (string.IsNullOrWhiteSpace(existingOverviewBody))
        {
            return null;
        }

        var normalizedBody = existingOverviewBody
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
        var normalizedSummary = summary
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        if (string.Equals(normalizedBody, normalizedSummary, StringComparison.Ordinal))
        {
            return null;
        }

        if (normalizedBody.StartsWith(normalizedSummary, StringComparison.Ordinal))
        {
            var remainder = normalizedBody[normalizedSummary.Length..].Trim();
            return string.IsNullOrWhiteSpace(remainder) ? null : remainder;
        }

        var firstHeadingIndex = normalizedBody.IndexOf("\n## ", StringComparison.Ordinal);
        if (firstHeadingIndex >= 0)
        {
            return normalizedBody[(firstHeadingIndex + 1)..].Trim();
        }

        return normalizedBody.StartsWith("## ", StringComparison.Ordinal)
            ? normalizedBody
            : null;
    }

    private static DocsPlacement InferPlacement(string kind, string docsSlug)
    {
        return new DocsPlacement("Other", docsSlug);
    }

    internal static string RenderOverviewPage(
        string title,
        string summary,
        string? customOverviewContent)
    {
        var lines = new List<string>
        {
            $"# {title}",
            string.Empty,
            summary
        };

        if (!string.IsNullOrWhiteSpace(customOverviewContent))
        {
            lines.Add(string.Empty);
            lines.Add(customOverviewContent);
        }

        return string.Join(GeneratedDocumentLineEndings.Canonical, lines);
    }

    private static string RenderIndexPage(
        string kind,
        HookKindSpec spec,
        string existingContent,
        IReadOnlyList<HookIndexEntry> entries)
    {
        var relativeHooksRoot = spec.DocsRoot.Split('/').Last();
        var builder = new StringBuilder();
        builder.AppendLine(StripGeneratedCatalog(existingContent).TrimEnd());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(GeneratedCatalogStart);
        builder.AppendLine("## Available Hooks");
        builder.AppendLine();
        builder.AppendLine("The built-in hooks below are grouped by usage area so it is easier to shortlist the right hook before drilling into configuration details.");

        foreach (var group in entries
                     .GroupBy(entry => entry.Group, StringComparer.Ordinal)
                     .OrderBy(group => GroupOrder(kind, group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine($"### {group.Key}");
            builder.AppendLine();

            foreach (var entry in group
                         .OrderBy(candidate => candidate.Subgroup, StringComparer.Ordinal)
                         .ThenBy(candidate => candidate.DocsSlug, StringComparer.Ordinal))
            {
                builder.AppendLine(
                    $"- [{entry.DocsSlug}]({relativeHooksRoot}/{entry.DocsSlug}/overview.md): {LeadParagraph(entry.Summary)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(GeneratedCatalogEnd);
        return builder.ToString().TrimEnd();
    }

    private static string StripGeneratedCatalog(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd();
        var startIndex = normalized.IndexOf(GeneratedCatalogStart, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return normalized;
        }

        var endIndex = normalized.IndexOf(GeneratedCatalogEnd, startIndex, StringComparison.Ordinal);
        return endIndex < 0
            ? normalized[..startIndex].TrimEnd()
            : normalized[..startIndex].TrimEnd();
    }

    private static string LeadParagraph(string summary)
    {
        return summary
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)[0]
            .Replace('\n', ' ')
            .Trim();
    }

    private static int GroupOrder(string kind, string group)
    {
        string[] orderedGroups = kind switch
        {
            "assertion" => ["Latency", "Hermeticity", "Content validation", "Contract validation", "Transport metadata"],
            "generator" => ["External sources", "Existing data sources", "Structured payloads"],
            "probe" => ["RabbitMQ administration", "Redis maintenance", "Databases", "SQL maintenance", "Cluster orchestration"],
            "processor" => ["Static responses", "Request-derived responses", "Transformations", "Data-driven responses", "Error responses"],
            _ => []
        };

        var index = Array.FindIndex(orderedGroups, candidate => string.Equals(candidate, group, StringComparison.Ordinal));
        return index >= 0 ? index : orderedGroups.Length;
    }

    private static string RenderTableView(string title, HookSchemaDocument hookSchema)
    {
        var rows = new List<TableRow>();
        SchemaTraversal.Traverse(hookSchema.RootName, hookSchema.Schema, hookSchema.Required, rows);

        var builder = new StringBuilder();
        builder.AppendLine($"# {title} Configurations Table View");
        builder.AppendLine();
        builder.AppendLine("| Property Path | Type | Required | Default | Description |");
        builder.AppendLine("| ------------- | ---- | -------- | ------- | ----------- |");
        foreach (var row in rows)
        {
            builder.AppendLine($"| `{row.Path}` | `{row.Type}` | {row.Required} | {Escape(row.DefaultValue)} | {Escape(row.Description)} |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderYamlView(string title, HookSchemaDocument hookSchema)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title} Configurations Yaml View");
        builder.AppendLine();
        builder.AppendLine("```yaml");
        foreach (var line in SchemaTraversal.RenderYaml(hookSchema.RootName, hookSchema.Schema))
        {
            builder.AppendLine(line);
        }
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);
    }

    private sealed record HookKindSpec(string DocsRoot, string IndexRelativePath, string DisplayTitle, string RepositoryDirectory);

    private sealed record HookCatalogFile(IReadOnlyList<HookCatalogHookType> HookTypes);

    private sealed record HookCatalogHookType(
        string HookKind,
        string Title,
        string DocsSlug,
        string ConfigurationSchemaJsonPointer);

    private sealed record HookCatalogEntry(
        string Kind,
        string DocsSlug,
        string FamilyId,
        string ConfigurationSchemaJsonPointer);

    internal sealed record HookDocumentation(string? Summary, DocsPlacement? Placement);

    private sealed record HookIndexEntry(string DocsSlug, string Summary, string Group, string Subgroup);

    private sealed record HookSchemaDocument(string RootName, bool Required, JsonSchema Schema);

    private sealed record TableRow(string Path, string Type, string Required, string DefaultValue, string Description);

    private static class SchemaTraversal
    {
        private const string Yes = "&#10004";
        private const string No = "&#10006";

        public static void Traverse(string path, JsonSchema schema, bool required, IList<TableRow> rows)
        {
            rows.Add(new TableRow(path, DescribeType(schema), required ? Yes : No, schema.Default?.ToString() ?? string.Empty, schema.Description ?? string.Empty));

            if (schema.Type.HasFlag(JsonObjectType.Array) && TryGetItemSchema(schema, out var itemSchema))
            {
                if (itemSchema.ActualSchema.Type.HasFlag(JsonObjectType.Object))
                {
                    Traverse(path + "[]", itemSchema.ActualSchema, required: false, rows);
                }
                else
                {
                    rows.Add(new TableRow(path + "[]", DescribeType(itemSchema.ActualSchema), No, itemSchema.Default?.ToString() ?? string.Empty, itemSchema.Description ?? string.Empty));
                }

                return;
            }

            if (!schema.Type.HasFlag(JsonObjectType.Object) || schema.Properties.Count == 0)
            {
                return;
            }

            foreach (var child in OrderProperties(schema))
            {
                Traverse($"{path}.{child.Key}", child.Value.ActualSchema, schema.RequiredProperties.Contains(child.Key), rows);
            }
        }

        public static IReadOnlyList<string> RenderYaml(string propertyName, JsonSchema schema)
        {
            var lines = new List<string>();
            RenderYamlCore(lines, 0, propertyName, schema);
            return lines;
        }

        private static void RenderYamlCore(
            ICollection<string> lines,
            int indentLevel,
            string propertyName,
            JsonSchema schema,
            string? linePrefix = null)
        {
            var indent = new string(' ', indentLevel * 2);
            var propertyLine = $"{indent}{linePrefix}{propertyName}:";

            if (schema.Type.HasFlag(JsonObjectType.Array) && TryGetItemSchema(schema, out var itemSchema))
            {
                if (itemSchema.ActualSchema.Type.HasFlag(JsonObjectType.Object))
                {
                    lines.Add(propertyLine);
                    var children = OrderProperties(itemSchema.ActualSchema).ToList();
                    if (children.Count == 0)
                    {
                        lines.Add($"{indent}  -");
                        return;
                    }

                    RenderYamlCore(
                        lines,
                        indentLevel + 1,
                        children[0].Key,
                        children[0].Value.ActualSchema,
                        "- ");

                    foreach (var child in children.Skip(1))
                    {
                        RenderYamlCore(lines, indentLevel + 2, child.Key, child.Value.ActualSchema);
                    }
                }
                else
                {
                    lines.Add($"{indent}{linePrefix}{propertyName}: []");
                }

                return;
            }

            if (schema.Type.HasFlag(JsonObjectType.Object) && schema.Properties.Count != 0)
            {
                lines.Add(propertyLine);
                foreach (var child in OrderProperties(schema))
                {
                    RenderYamlCore(lines, indentLevel + 1, child.Key, child.Value.ActualSchema);
                }

                return;
            }

            lines.Add(propertyLine);
        }

        private static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> OrderProperties(JsonSchema schema)
        {
            return schema.Properties
                .OrderBy(property => Category(property.Value.ActualSchema))
                .ThenByDescending(property => schema.RequiredProperties.Contains(property.Key))
                .ThenBy(property => property.Key, StringComparer.Ordinal);
        }

        private static int Category(JsonSchema schema)
        {
            if (schema.Type.HasFlag(JsonObjectType.Array))
            {
                return 1;
            }

            if (schema.Type.HasFlag(JsonObjectType.Object) && schema.Properties.Count != 0)
            {
                return 2;
            }

            return 0;
        }

        private static string DescribeType(JsonSchema schema)
        {
            if (schema.Enumeration.Count != 0)
            {
                return $"one of [{string.Join(" / ", schema.Enumeration.Select(value => value?.ToString() ?? "null"))}]";
            }

            if (schema.Type == JsonObjectType.None && schema.AnyOf.Count != 0)
            {
                var anyOfTypes = schema.AnyOf
                    .SelectMany(GetTypeNames)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                return anyOfTypes.Count != 0
                    ? JoinFriendlyTypes(anyOfTypes)
                    : "one of multiple supported shapes";
            }

            var normalized = GetTypeNames(schema).ToList();
            return normalized.Count != 0 ? JoinFriendlyTypes(normalized) : "object";
        }

        private static IEnumerable<string> GetTypeNames(JsonSchema schema)
        {
            return schema.Type.ToString()
                .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(type => !string.Equals(type, nameof(JsonObjectType.None), StringComparison.Ordinal))
                .Select(ToFriendlyTypeName)
                .OrderBy(type => string.Equals(type, "null", StringComparison.Ordinal) ? 1 : 0)
                .ThenBy(type => type, StringComparer.Ordinal);
        }

        private static string ToFriendlyTypeName(string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "array" => "list",
                "boolean" => "true/false",
                "file" => "file",
                "integer" => "integer",
                "null" => "null",
                "number" => "number",
                "object" => "object",
                "string" => "string",
                _ => typeName
            };
        }

        private static string JoinFriendlyTypes(IReadOnlyList<string> typeNames)
        {
            return string.Join(" or ", typeNames);
        }

        private static bool TryGetItemSchema(JsonSchema schema, out JsonSchema itemSchema)
        {
            if (schema.Item is not null)
            {
                itemSchema = schema.Item;
                return true;
            }

            if (schema.Items.Count != 0)
            {
                itemSchema = schema.Items.First();
                return true;
            }

            itemSchema = schema;
            return false;
        }
    }
}
