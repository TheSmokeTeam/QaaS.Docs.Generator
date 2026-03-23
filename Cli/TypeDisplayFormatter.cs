using System.Text;
using System.Text.RegularExpressions;

namespace QaaS.Docs.Generator.Cli;

internal static class TypeDisplayFormatter
{
    private static readonly Dictionary<string, string> PrimitiveAliases = new(StringComparer.Ordinal)
    {
        ["Boolean"] = "bool",
        ["Byte"] = "byte",
        ["Char"] = "char",
        ["Decimal"] = "decimal",
        ["Double"] = "double",
        ["Int16"] = "short",
        ["Int32"] = "int",
        ["Int64"] = "long",
        ["Object"] = "object",
        ["SByte"] = "sbyte",
        ["Single"] = "float",
        ["String"] = "string",
        ["UInt16"] = "ushort",
        ["UInt32"] = "uint",
        ["UInt64"] = "ulong"
    };

    private static readonly Regex PascalCaseWordMatcher = new(
        "[A-Z]+(?=$|[A-Z][a-z]|\\d)|[A-Z]?[a-z]+|\\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string FormatSourceType(string rawType)
    {
        var simplified = SimplifyTypeName(rawType);
        var sourceType = simplified.EndsWith("Options", StringComparison.Ordinal)
            ? simplified[..^"Options".Length] + " options"
            : simplified;

        return HumanizePascalCase(sourceType);
    }

    public static string FormatValueType(string rawType)
    {
        return FormatValueTypeCore(SimplifyTypeName(rawType));
    }

    private static string FormatValueTypeCore(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        if (TryStripNullable(typeName, out var nullableInner))
        {
            return $"{FormatValueTypeCore(nullableInner)} (optional)";
        }

        if (TrySplitGenericType(typeName, out var outerType, out var genericArguments))
        {
            var simplifiedOuter = StripNamespace(outerType);
            var formattedArguments = genericArguments.Select(FormatValueTypeCore).ToArray();

            if (IsCollectionType(simplifiedOuter) && formattedArguments.Length == 1)
            {
                return $"{formattedArguments[0]} list";
            }

            if (IsDictionaryType(simplifiedOuter) && formattedArguments.Length >= 2)
            {
                return $"dictionary of {formattedArguments[0]} to {formattedArguments[1]}";
            }

            return $"{HumanizePascalCase(simplifiedOuter)}<{string.Join(", ", formattedArguments)}>";
        }

        var simplified = StripNamespace(typeName);
        if (PrimitiveAliases.TryGetValue(simplified, out var alias))
        {
            return alias;
        }

        return simplified;
    }

    private static string SimplifyTypeName(string rawType)
    {
        var typeName = rawType.Trim();
        if (typeName.Length == 0)
        {
            return string.Empty;
        }

        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            return SimplifyTypeName(typeName[..^2]) + "[]";
        }

        if (TryStripNullable(typeName, out var nullableInner))
        {
            return SimplifyTypeName(nullableInner) + "?";
        }

        if (TrySplitGenericType(typeName, out var outerType, out var genericArguments))
        {
            var simplifiedOuter = StripNamespace(outerType);
            var simplifiedArguments = genericArguments.Select(SimplifyTypeName).ToArray();

            return $"{simplifiedOuter}<{string.Join(", ", simplifiedArguments)}>";
        }

        return StripNamespace(typeName);
    }

    private static string StripNamespace(string typeName)
    {
        var trimmed = typeName.Trim();
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static bool TryStripNullable(string typeName, out string innerType)
    {
        if (!typeName.EndsWith("?", StringComparison.Ordinal))
        {
            innerType = string.Empty;
            return false;
        }

        innerType = typeName[..^1];
        return true;
    }

    private static bool TrySplitGenericType(string typeName, out string outerType, out IReadOnlyList<string> arguments)
    {
        var genericStart = typeName.IndexOf('<');
        if (genericStart < 0 || !typeName.EndsWith(">", StringComparison.Ordinal))
        {
            outerType = string.Empty;
            arguments = Array.Empty<string>();
            return false;
        }

        outerType = typeName[..genericStart];
        var inner = typeName[(genericStart + 1)..^1];
        arguments = SplitGenericArguments(inner);
        return true;
    }

    private static IReadOnlyList<string> SplitGenericArguments(string innerType)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var character in innerType)
        {
            switch (character)
            {
                case '<':
                    depth++;
                    current.Append(character);
                    break;
                case '>':
                    depth--;
                    current.Append(character);
                    break;
                case ',' when depth == 0:
                    arguments.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        if (current.Length != 0)
        {
            arguments.Add(current.ToString().Trim());
        }

        return arguments;
    }

    private static bool IsCollectionType(string typeName)
    {
        return string.Equals(typeName, "IList", StringComparison.Ordinal) ||
               string.Equals(typeName, "IReadOnlyList", StringComparison.Ordinal) ||
               string.Equals(typeName, "ICollection", StringComparison.Ordinal) ||
               string.Equals(typeName, "IEnumerable", StringComparison.Ordinal) ||
               string.Equals(typeName, "List", StringComparison.Ordinal);
    }

    private static bool IsDictionaryType(string typeName)
    {
        return string.Equals(typeName, "Dictionary", StringComparison.Ordinal) ||
               string.Equals(typeName, "IDictionary", StringComparison.Ordinal) ||
               string.Equals(typeName, "IReadOnlyDictionary", StringComparison.Ordinal);
    }

    private static string HumanizePascalCase(string value)
    {
        var words = PascalCaseWordMatcher.Matches(value).Select(match => match.Value).ToArray();
        if (words.Length == 0)
        {
            return value;
        }

        return string.Join(
            " ",
            words.Select((word, index) =>
                index == 0 ? word : word.ToLowerInvariant()));
    }
}
