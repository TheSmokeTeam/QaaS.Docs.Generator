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
    private static readonly IReadOnlyDictionary<string, HookKindSpec> KindSpecs =
        new Dictionary<string, HookKindSpec>(StringComparer.Ordinal)
        {
            ["assertion"] = new("assertions/availableAssertions", "QaaS.Common.Assertions"),
            ["generator"] = new("generators/availableGenerators", "QaaS.Common.Generators"),
            ["probe"] = new("probes/availableProbes", "QaaS.Common.Probes"),
            ["processor"] = new("processors/availableProcessors", "QaaS.Common.Processors")
        };

    public async Task<IReadOnlyList<GeneratedDocument>> RenderAsync(string docsRoot, string mirrorRoot)
    {
        var workspaceRoot = ResolveWorkspaceRoot(docsRoot, mirrorRoot);
        var catalog = await LoadCatalogAsync(mirrorRoot);
        var documents = new List<GeneratedDocument>();

        foreach (var kind in KindSpecs.Keys.OrderBy(candidate => candidate, StringComparer.Ordinal))
        {
            var spec = KindSpecs[kind];
            var hooksRoot = Path.Combine(
                docsRoot,
                "docs",
                spec.DocsRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(hooksRoot))
            {
                continue;
            }

            var sourceRoot = Path.Combine(workspaceRoot, spec.RepositoryDirectory);
            foreach (var hookDirectory in Directory.EnumerateDirectories(hooksRoot)
                         .OrderBy(candidate => candidate, StringComparer.Ordinal))
            {
                var docsSlug = Path.GetFileName(hookDirectory);
                var summary = await LoadHookSummaryAsync(sourceRoot, docsSlug);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    throw new InvalidOperationException(
                        $"Hook '{docsSlug}' in '{kind}' is missing a public XML summary in {sourceRoot}.");
                }

                documents.Add(new GeneratedDocument(
                    $"{spec.DocsRoot}/{docsSlug}/overview.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderOverviewPage(docsSlug, summary),
                        [kind, docsSlug, "overview"])));

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

    private static async Task<string?> LoadHookSummaryAsync(string sourceRoot, string docsSlug)
    {
        var sourceFile = FindHookSourceFile(sourceRoot, docsSlug);
        if (sourceFile is null)
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(sourceFile);
        var syntaxTree = CSharpSyntaxTree.ParseText(text, path: sourceFile);
        var root = await syntaxTree.GetRootAsync();
        var typeDeclaration = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(candidate => string.Equals(candidate.Identifier.Text, docsSlug, StringComparison.Ordinal));
        if (typeDeclaration is null)
        {
            return null;
        }

        var summary = DocumentationCommentParser.Parse(typeDeclaration).Summary;
        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    private static string? FindHookSourceFile(string sourceRoot, string docsSlug)
    {
        return Directory.EnumerateFiles(sourceRoot, $"{docsSlug}.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
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

    private static string RenderOverviewPage(string title, string summary)
    {
        return string.Join(
            GeneratedDocumentLineEndings.Canonical,
            [
                $"# {title}",
                string.Empty,
                summary,
                string.Empty,
                "_This overview is generated automatically from the hook source summary._"
            ]);
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

    private sealed record HookKindSpec(string DocsRoot, string RepositoryDirectory);

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

        private static void RenderYamlCore(ICollection<string> lines, int indentLevel, string propertyName, JsonSchema schema)
        {
            var indent = new string(' ', indentLevel * 2);

            if (schema.Type.HasFlag(JsonObjectType.Array) && TryGetItemSchema(schema, out var itemSchema))
            {
                if (itemSchema.ActualSchema.Type.HasFlag(JsonObjectType.Object))
                {
                    lines.Add($"{indent}{propertyName}:");
                    lines.Add($"{indent}  -");
                    foreach (var child in OrderProperties(itemSchema.ActualSchema))
                    {
                        RenderYamlCore(lines, indentLevel + 2, child.Key, child.Value.ActualSchema);
                    }
                }
                else
                {
                    lines.Add($"{indent}{propertyName}: []");
                }

                return;
            }

            if (schema.Type.HasFlag(JsonObjectType.Object) && schema.Properties.Count != 0)
            {
                lines.Add($"{indent}{propertyName}:");
                foreach (var child in OrderProperties(schema))
                {
                    RenderYamlCore(lines, indentLevel + 1, child.Key, child.Value.ActualSchema);
                }

                return;
            }

            lines.Add($"{indent}{propertyName}:");
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
