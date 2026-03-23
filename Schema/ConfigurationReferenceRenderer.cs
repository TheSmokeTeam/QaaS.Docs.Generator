using System.Text;
using NJsonSchema;

namespace QaaS.Docs.Generator.Schema;

internal sealed class ConfigurationReferenceRenderer
{
    public IReadOnlyList<GeneratedDocument> RenderRunner(FamilySchemaDocs familyDocs)
    {
        return RenderFamily(
            familyDocs,
            "qaas/userInterfaces/runner/configurationSections");
    }

    public IReadOnlyList<GeneratedDocument> RenderMocker(FamilySchemaDocs familyDocs)
    {
        return RenderFamily(
            familyDocs,
            "mocker/userInterfaces/mocker/configurationSections");
    }

    private static IReadOnlyList<GeneratedDocument> RenderFamily(
        FamilySchemaDocs familyDocs,
        string rootPath)
    {
        var documents = new List<GeneratedDocument>();

        foreach (var section in familyDocs.Sections)
        {
            if (!familyDocs.Schema.Properties.TryGetValue(section.TopLevelPropertyName, out var property))
            {
                continue;
            }

            var basePath = $"{rootPath}/{section.DocsSlug}";
            documents.Add(new GeneratedDocument(
                $"{basePath}/configurations/tableView.md",
                GeneratedDocumentHasher.WithHeader(RenderTableView(section, property), [familyDocs.FamilyId, section.Id, "table-view"])));
            documents.Add(new GeneratedDocument(
                $"{basePath}/configurations/yamlView.md",
                GeneratedDocumentHasher.WithHeader(RenderYamlView(section, property), [familyDocs.FamilyId, section.Id, "yaml-view"])));
        }

        return documents;
    }

    private static string RenderTableView(SchemaSection section, JsonSchemaProperty property)
    {
        var rows = new List<TableRow>();
        SchemaTraversal.Traverse(section.TopLevelPropertyName, property.ActualSchema, required: false, rows);

        var builder = new StringBuilder();
        builder.AppendLine($"# {section.Title} Configurations Table View");
        builder.AppendLine();
        builder.AppendLine("| Property Path | Type | Required | Default | Description |");
        builder.AppendLine("| ------------- | ---- | -------- | ------- | ----------- |");
        foreach (var row in rows)
        {
            builder.AppendLine($"| `{row.Path}` | `{row.Type}` | {row.Required} | {Escape(row.DefaultValue)} | {Escape(row.Description)} |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderYamlView(SchemaSection section, JsonSchemaProperty property)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {section.Title} Configurations Yaml View");
        builder.AppendLine();
        builder.AppendLine("```yaml");
        foreach (var line in SchemaTraversal.RenderYaml(section.TopLevelPropertyName, property.ActualSchema))
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
