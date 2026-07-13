using System.Globalization;
using NJsonSchema;

namespace QaaS.Docs.Generator.Schema;

internal static class SchemaYamlRenderer
{
    public static IReadOnlyList<string> Render(
        string propertyName,
        JsonSchema schema,
        Func<string, bool>? includeProperty = null
    )
    {
        var lines = new List<string>();
        RenderCore(lines, 0, propertyName, schema, includeProperty);
        return lines;
    }

    private static void RenderCore(
        ICollection<string> lines,
        int indentLevel,
        string propertyName,
        JsonSchema schema,
        Func<string, bool>? includeProperty,
        string? linePrefix = null
    )
    {
        var indent = Indent(indentLevel);
        var propertyLine = $"{indent}{linePrefix}{propertyName}:";

        if (
            schema.Type.HasFlag(JsonObjectType.Array)
            && TryGetItemSchema(schema, out var itemSchema)
        )
        {
            var itemIndentLevel = indentLevel + (linePrefix is null ? 1 : 2);
            if (itemSchema.ActualSchema.Type.HasFlag(JsonObjectType.Object))
            {
                lines.Add(propertyLine);
                var children = OrderProperties(itemSchema.ActualSchema, includeProperty).ToList();
                if (children.Count == 0)
                {
                    lines.Add($"{Indent(itemIndentLevel)}- {{}}");
                    return;
                }

                RenderCore(
                    lines,
                    itemIndentLevel,
                    children[0].Key,
                    children[0].Value.ActualSchema,
                    includeProperty,
                    "- "
                );
                foreach (var child in children.Skip(1))
                {
                    RenderCore(
                        lines,
                        itemIndentLevel + 1,
                        child.Key,
                        child.Value.ActualSchema,
                        includeProperty
                    );
                }
            }
            else if (schema.MinItems > 0)
            {
                lines.Add(propertyLine);
                lines.Add(
                    $"{Indent(itemIndentLevel)}- {RenderSampleScalar(itemSchema.ActualSchema)}"
                );
            }
            else
            {
                lines.Add($"{propertyLine} []");
            }

            return;
        }

        if (schema.Type.HasFlag(JsonObjectType.Object) && schema.Properties.Count != 0)
        {
            lines.Add(propertyLine);
            var childIndentLevel = indentLevel + (linePrefix is null ? 1 : 2);
            foreach (var child in OrderProperties(schema, includeProperty))
            {
                RenderCore(
                    lines,
                    childIndentLevel,
                    child.Key,
                    child.Value.ActualSchema,
                    includeProperty
                );
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

        if (schema.Default is not null)
        {
            return FormatYamlScalar(schema.Default);
        }

        if (HasType(schema, JsonObjectType.Boolean))
        {
            return "True";
        }

        if (HasType(schema, JsonObjectType.Integer))
        {
            return FormatIntegerMinimum(schema, "0");
        }

        if (HasType(schema, JsonObjectType.Number))
        {
            return "1.0";
        }

        if (HasType(schema, JsonObjectType.Array))
        {
            return "[]";
        }

        if (HasType(schema, JsonObjectType.Object))
        {
            return "{}";
        }

        return "'value'";
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

    private static string FormatIntegerMinimum(JsonSchema schema, string fallback)
    {
        var minimum = GetCandidateSchemas(schema)
            .Select(candidate => candidate.Minimum)
            .FirstOrDefault(value => value is not null);
        if (minimum is null)
        {
            return fallback;
        }

        return decimal.Ceiling(minimum.Value).ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatYamlScalar(object value)
    {
        return value switch
        {
            bool boolean => boolean ? "True" : "False",
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(
                value,
                CultureInfo.InvariantCulture
            ) ?? "1",
            float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture)
                ?? "1.0",
            _ => QuoteYamlString(value.ToString() ?? "value"),
        };
    }

    private static string QuoteYamlString(string value)
    {
        var escaped = value.Replace("'", "''", StringComparison.Ordinal);
        return $"'{escaped}'";
    }

    private static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> OrderProperties(
        JsonSchema schema,
        Func<string, bool>? includeProperty
    )
    {
        return schema
            .Properties.Where(property => includeProperty?.Invoke(property.Key) ?? true)
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

    private static string Indent(int indentLevel)
    {
        return new string(' ', indentLevel * 2);
    }
}
