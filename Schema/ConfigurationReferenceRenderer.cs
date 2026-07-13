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
                        WithSchemaVerificationMarkers(
                            familyDocs,
                            RenderTableView(section, property)
                        ),
                        [familyDocs.FamilyId, section.Id, "table-view"]
                    )
                )
            );
            documents.Add(
                new GeneratedDocument(
                    $"{basePath}/configurations/yamlView.md",
                    GeneratedDocumentHasher.WithHeader(
                        WithSchemaVerificationMarkers(
                            familyDocs,
                            RenderYamlView(section, property)
                        ),
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
                        WithSchemaVerificationMarkers(
                            familyDocs,
                            RenderTableView(
                                $"{typeReference.Title} Configurations Table View",
                                $"Sessions[].{typeReference.SchemaPropertyName}",
                                property,
                                $"{typeReference.DocsSlug}-yamlView.md"
                            )
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
                        WithSchemaVerificationMarkers(
                            familyDocs,
                            RenderYamlView(
                                $"{typeReference.Title} Configurations Yaml View",
                                typeReference.SchemaPropertyName,
                                property,
                                $"{typeReference.DocsSlug}-tableView.md"
                            )
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

    private static string WithSchemaVerificationMarkers(
        FamilySchemaDocs familyDocs,
        string generatedContent
    )
    {
        var familySchemaRoot = $"QaaS.PackageMirror\\schemas\\{familyDocs.FamilyId}\\latest";
        return string.Join(
            Environment.NewLine,
            [
                $"<!-- Verified-against: {familySchemaRoot}\\docs-manifest.json -->",
                $"<!-- Verified-against: {familySchemaRoot}\\schema.json -->",
                string.Empty,
                generatedContent,
            ]
        );
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
        JsonSchemaProperty property,
        string yamlLink = "yamlView.md"
    )
    {
        var rows = new List<TableRow>();
        SchemaTraversal.Traverse(rootPath, property.ActualSchema, required: false, rows);

        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine(
            "> TL;DR — Use this generated field table to check property paths, types, required status, defaults, and descriptions."
        );
        builder.AppendLine();
        builder.AppendLine("## When to use");
        builder.AppendLine();
        builder.AppendLine(
            "Use this page when you need the exact field path or value type for a configuration section before editing YAML."
        );
        builder.AppendLine();
        builder.AppendLine("## YAML configuration");
        builder.AppendLine();
        builder.AppendLine(
            "The table below mirrors the schema used by the YAML scaffold page. Nested rows use dotted paths and `[]` for list items."
        );
        builder.AppendLine();
        builder.AppendLine("| Property Path | Type | Required | Default | Description |");
        builder.AppendLine("| ------------- | ---- | -------- | ------- | ----------- |");
        foreach (var row in rows)
        {
            builder.AppendLine(
                $"| `{row.Path}` | `{row.Type}` | {row.Required} | {FormatDefault(row.DefaultValue)} | {Escape(row.Description)} |"
            );
        }

        builder.AppendLine();
        builder.AppendLine("## Edge cases");
        builder.AppendLine();
        builder.AppendLine(
            "- Empty default cells mean the schema does not define a default value for that field."
        );
        builder.AppendLine(
            "- Required status applies to the immediate parent object shown by the property path."
        );
        builder.AppendLine();
        builder.AppendLine("## See also");
        builder.AppendLine();
        builder.AppendLine($"- [YAML scaffold]({yamlLink})");
        builder.AppendLine("- [Overview](../overview.md)");

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
        JsonSchemaProperty property,
        string tableLink = "tableView.md",
        bool includeOverviewLink = true
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine(
            "> TL;DR — Copy this schema-derived YAML scaffold, replace placeholder values, and use the table view for field descriptions."
        );
        builder.AppendLine();
        builder.AppendLine("## When to use");
        builder.AppendLine();
        builder.AppendLine(
            "Use this page when you need the generated YAML shape for this configuration section and want every emitted field in one block."
        );
        builder.AppendLine();
        builder.AppendLine("## YAML configuration");
        builder.AppendLine();
        builder.AppendLine(
            "The scaffold follows the generated schema order. String placeholders are quoted, optional lists render as `[]`, and numeric placeholders use schema minimums when they exist."
        );
        builder.AppendLine();
        builder.AppendLine("## Minimal example");
        builder.AppendLine();
        builder.AppendLine("```yaml");
        foreach (var line in SchemaTraversal.RenderYaml(rootPropertyName, property.ActualSchema))
        {
            builder.AppendLine(line);
        }
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Realistic example");
        builder.AppendLine();
        builder.AppendLine(
            "Start with the minimal scaffold, replace placeholder values with project values, and keep only the optional branches that this configuration needs."
        );
        builder.AppendLine();
        builder.AppendLine("## Edge cases");
        builder.AppendLine();
        builder.AppendLine(
            "- Optional arrays are emitted as `[]`; add entries only when the section needs that collection."
        );
        builder.AppendLine(
            "- Placeholder-style strings are quoted so YAML parsers keep them as scalar values."
        );
        builder.AppendLine();
        builder.AppendLine("## See also");
        builder.AppendLine();
        builder.AppendLine($"- [Configuration table]({tableLink})");
        if (includeOverviewLink)
        {
            builder.AppendLine("- [Overview](../overview.md)");
        }

        return builder.ToString().TrimEnd();
    }

    private static string Escape(string value)
    {
        return MarkdownTableCellFormatter.Format(value);
    }

    private static string FormatDefault(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : Escape(value);
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
            return SchemaYamlRenderer.Render(
                propertyName,
                schema,
                property => !IsAccidentalConfigurationAlias(property)
            );
        }

        private static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> OrderProperties(
            JsonSchema schema
        )
        {
            return schema
                .Properties.Where(property => !IsAccidentalConfigurationAlias(property.Key))
                .OrderBy(property => Category(property.Value.ActualSchema))
                .ThenByDescending(property => schema.RequiredProperties.Contains(property.Key))
                .ThenBy(property => property.Key, StringComparer.Ordinal);
        }

        private static bool IsAccidentalConfigurationAlias(string propertyName)
        {
            return string.Equals(propertyName, "Configuration", StringComparison.Ordinal);
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
