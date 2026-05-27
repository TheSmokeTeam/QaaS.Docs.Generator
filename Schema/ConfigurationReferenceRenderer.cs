using System.Globalization;
using System.Text;
using NJsonSchema;

namespace QaaS.Docs.Generator.Schema;

internal sealed class ConfigurationReferenceRenderer
{
    private static readonly SessionTypeReference[] RunnerSessionTypeReferences =
    [
        new("Publishers", "publishers", "Publishers"),
        new("Consumers", "consumers", "Consumers"),
        new("Collectors", "collectors", "Collectors"),
        new("Transactions", "transactions", "Transactions"),
        new("Probes", "probes", "Probes"),
        new("MockerCommands", "mockerCommands", "Mocker Commands"),
    ];

    public IReadOnlyList<GeneratedDocument> RenderRunner(FamilySchemaDocs familyDocs)
    {
        return RenderFamily(
            familyDocs,
            "qaas/userInterfaces/runner/configurationSections",
            includeRunnerSessionTypeReferences: true
        );
    }

    public IReadOnlyList<GeneratedDocument> RenderMocker(FamilySchemaDocs familyDocs)
    {
        return RenderFamily(familyDocs, "mocker/userInterfaces/mocker/configurationSections");
    }

    private static IReadOnlyList<GeneratedDocument> RenderFamily(
        FamilySchemaDocs familyDocs,
        string rootPath,
        bool includeRunnerSessionTypeReferences = false
    )
    {
        var documents = new List<GeneratedDocument>();

        foreach (var section in familyDocs.Sections)
        {
            if (
                !familyDocs.Schema.Properties.TryGetValue(
                    section.TopLevelPropertyName,
                    out var property
                )
            )
            {
                continue;
            }

            var basePath = $"{rootPath}/{section.DocsSlug}";
            documents.Add(
                new GeneratedDocument(
                    $"{basePath}/configurations/tableView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderTableView(section, property),
                        [familyDocs.FamilyId, section.Id, "table-view"]
                    )
                )
            );
            documents.Add(
                new GeneratedDocument(
                    $"{basePath}/configurations/yamlView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderYamlView(section, property),
                        [familyDocs.FamilyId, section.Id, "yaml-view"]
                    )
                )
            );
        }

        if (includeRunnerSessionTypeReferences)
        {
            documents.AddRange(RenderRunnerSessionTypeReferences(familyDocs, rootPath));
        }

        return documents;
    }

    private static IReadOnlyList<GeneratedDocument> RenderRunnerSessionTypeReferences(
        FamilySchemaDocs familyDocs,
        string rootPath
    )
    {
        var documents = new List<GeneratedDocument>();
        var sessionsSection = familyDocs.Sections.FirstOrDefault(section =>
            string.Equals(section.TopLevelPropertyName, "Sessions", StringComparison.Ordinal)
        );
        if (
            sessionsSection is null
            || !familyDocs.Schema.Properties.TryGetValue(
                sessionsSection.TopLevelPropertyName,
                out var sessionsProperty
            )
            || !TryGetSessionItemSchema(sessionsProperty.ActualSchema, out var sessionItemSchema)
        )
        {
            return documents;
        }

        foreach (var typeReference in RunnerSessionTypeReferences)
        {
            if (
                !sessionItemSchema.Properties.TryGetValue(
                    typeReference.SchemaPropertyName,
                    out var property
                )
            )
            {
                continue;
            }

            var basePath = $"{rootPath}/sessions/types/{typeReference.DocsSlug}";
            documents.Add(
                new GeneratedDocument(
                    $"{basePath}-tableView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderTableView(
                            $"{typeReference.Title} Configurations Table View",
                            $"Sessions[].{typeReference.SchemaPropertyName}",
                            property
                        ),
                        [
                            familyDocs.FamilyId,
                            sessionsSection.Id,
                            typeReference.SchemaPropertyName,
                            "table-view",
                        ]
                    )
                )
            );
            documents.Add(
                new GeneratedDocument(
                    $"{basePath}-yamlView.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderYamlView(
                            $"{typeReference.Title} Configurations Yaml View",
                            typeReference.SchemaPropertyName,
                            property
                        ),
                        [
                            familyDocs.FamilyId,
                            sessionsSection.Id,
                            typeReference.SchemaPropertyName,
                            "yaml-view",
                        ]
                    )
                )
            );
        }

        return documents;
    }

    private static string RenderTableView(SchemaSection section, JsonSchemaProperty property)
    {
        return RenderTableView(
            $"{section.Title} Configurations Table View",
            section.TopLevelPropertyName,
            property
        );
    }

    private static string RenderTableView(
        string title,
        string rootPath,
        JsonSchemaProperty property
    )
    {
        var rows = new List<TableRow>();
        SchemaTraversal.Traverse(rootPath, property.ActualSchema, required: false, rows);

        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine("| Property Path | Type | Required | Default | Description |");
        builder.AppendLine("| ------------- | ---- | -------- | ------- | ----------- |");
        foreach (var row in rows)
        {
            builder.AppendLine(
                $"| `{row.Path}` | `{row.Type}` | {row.Required} | {Escape(row.DefaultValue)} | {Escape(row.Description)} |"
            );
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderYamlView(SchemaSection section, JsonSchemaProperty property)
    {
        return RenderYamlView(
            $"{section.Title} Configurations Yaml View",
            section.TopLevelPropertyName,
            property
        );
    }

    private static string RenderYamlView(
        string title,
        string rootPropertyName,
        JsonSchemaProperty property
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine("```yaml");
        foreach (var line in SchemaTraversal.RenderYaml(rootPropertyName, property.ActualSchema))
        {
            builder.AppendLine(line);
        }
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    private static string Escape(string value)
    {
        return MarkdownTableCellFormatter.Format(value);
    }

    private static bool TryGetSessionItemSchema(JsonSchema schema, out JsonSchema itemSchema)
    {
        if (schema.Item is not null)
        {
            itemSchema = schema.Item.ActualSchema;
            return true;
        }

        if (schema.Items.Count != 0)
        {
            itemSchema = schema.Items.First().ActualSchema;
            return true;
        }

        itemSchema = schema;
        return false;
    }

    private sealed record TableRow(
        string Path,
        string Type,
        string Required,
        string DefaultValue,
        string Description
    );

    private sealed record SessionTypeReference(
        string SchemaPropertyName,
        string DocsSlug,
        string Title
    );

    private static class SchemaTraversal
    {
        private const string Yes = "&#10004";
        private const string No = "&#10006";

        public static void Traverse(
            string path,
            JsonSchema schema,
            bool required,
            IList<TableRow> rows
        )
        {
            rows.Add(
                new TableRow(
                    path,
                    DescribeType(schema),
                    required ? Yes : No,
                    schema.Default?.ToString() ?? string.Empty,
                    schema.Description ?? string.Empty
                )
            );

            if (
                schema.Type.HasFlag(JsonObjectType.Array)
                && TryGetItemSchema(schema, out var itemSchema)
            )
            {
                if (itemSchema.ActualSchema.Type.HasFlag(JsonObjectType.Object))
                {
                    Traverse(path + "[]", itemSchema.ActualSchema, required: false, rows);
                }
                else
                {
                    rows.Add(
                        new TableRow(
                            path + "[]",
                            DescribeType(itemSchema.ActualSchema),
                            No,
                            itemSchema.Default?.ToString() ?? string.Empty,
                            itemSchema.Description ?? string.Empty
                        )
                    );
                }

                return;
            }

            if (!schema.Type.HasFlag(JsonObjectType.Object) || schema.Properties.Count == 0)
            {
                return;
            }

            foreach (var child in OrderProperties(schema))
            {
                Traverse(
                    $"{path}.{child.Key}",
                    child.Value.ActualSchema,
                    schema.RequiredProperties.Contains(child.Key),
                    rows
                );
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
            string? linePrefix = null
        )
        {
            var indent = new string(' ', indentLevel * 2);
            var propertyLine = $"{indent}{linePrefix}{propertyName}:";

            if (
                schema.Type.HasFlag(JsonObjectType.Array)
                && TryGetItemSchema(schema, out var itemSchema)
            )
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
                        "- "
                    );

                    foreach (var child in children.Skip(1))
                    {
                        RenderYamlCore(lines, indentLevel + 2, child.Key, child.Value.ActualSchema);
                    }
                }
                else
                {
                    lines.Add(propertyLine);
                    lines.Add($"{indent}  - {RenderSampleScalar(itemSchema.ActualSchema)}");
                }

                return;
            }

            if (schema.Type.HasFlag(JsonObjectType.Object) && schema.Properties.Count != 0)
            {
                lines.Add(propertyLine);
                var childIndentLevel = indentLevel + (linePrefix is null ? 1 : 2);
                foreach (var child in OrderProperties(schema))
                {
                    RenderYamlCore(lines, childIndentLevel, child.Key, child.Value.ActualSchema);
                }

                return;
            }

            lines.Add($"{propertyLine} {RenderSampleScalar(schema)}");
        }

        private static string RenderSampleScalar(JsonSchema schema)
        {
            foreach (var candidate in GetCandidateSchemas(schema))
            {
                var enumValue = candidate.Enumeration.FirstOrDefault(value => value is not null);
                if (enumValue is not null)
                {
                    return FormatYamlScalar(enumValue);
                }
            }

            if (HasPlaceholderPattern(schema) && HasType(schema, JsonObjectType.String))
            {
                return "\"${value}\"";
            }

            if (schema.Default is not null)
            {
                return FormatYamlScalar(schema.Default);
            }

            if (HasType(schema, JsonObjectType.Boolean))
            {
                return "true";
            }

            if (HasType(schema, JsonObjectType.Integer))
            {
                return "1";
            }

            if (HasType(schema, JsonObjectType.Number))
            {
                return "1.0";
            }

            if (HasType(schema, JsonObjectType.String))
            {
                return "\"value\"";
            }

            if (HasType(schema, JsonObjectType.Array))
            {
                return "[]";
            }

            if (HasType(schema, JsonObjectType.Object))
            {
                return "{}";
            }

            return "\"value\"";
        }

        private static bool HasType(JsonSchema schema, JsonObjectType type)
        {
            return GetCandidateSchemas(schema).Any(candidate => candidate.Type.HasFlag(type));
        }

        private static IEnumerable<JsonSchema> GetCandidateSchemas(JsonSchema schema)
        {
            yield return schema;

            foreach (var candidate in schema.AnyOf.Concat(schema.OneOf).Concat(schema.AllOf))
            {
                yield return candidate.ActualSchema;
            }
        }

        private static bool HasPlaceholderPattern(JsonSchema schema)
        {
            return GetCandidateSchemas(schema)
                .Any(candidate =>
                    string.Equals(candidate.Pattern, @"\$\{.*\}", StringComparison.Ordinal)
                );
        }

        private static string FormatYamlScalar(object value)
        {
            return value switch
            {
                bool boolean => boolean ? "true" : "false",
                byte or sbyte or short or ushort or int or uint or long or ulong =>
                    Convert.ToString(value, CultureInfo.InvariantCulture) ?? "1",
                float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? "1.0",
                _ => QuoteYamlString(value.ToString() ?? "value"),
            };
        }

        private static string QuoteYamlString(string value)
        {
            var escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            return $"\"{escaped}\"";
        }

        private static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> OrderProperties(
            JsonSchema schema
        )
        {
            return schema
                .Properties.OrderBy(property => Category(property.Value.ActualSchema))
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
                var anyOfTypes = schema
                    .AnyOf.SelectMany(GetTypeNames)
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
            return schema
                .Type.ToString()
                .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(type =>
                    !string.Equals(type, nameof(JsonObjectType.None), StringComparison.Ordinal)
                )
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
                _ => typeName,
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
