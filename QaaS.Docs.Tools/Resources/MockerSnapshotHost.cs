using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using CommandLine;
using QaaS.Mocker.Options;

_ = typeof(QaaS.Framework.Executions.Options.LoggerOptions);

var outputPath = GetOutputPath(args);

var commandTypes = new[]
{
    typeof(RunOptions),
    typeof(TemplateOptions)
};

var commands = commandTypes.Select(BuildCommand).ToArray();
var overviewHelpText = CaptureOutput(() => QaaS.Mocker.Bootstrap.New(Array.Empty<string>()), "Mocker overview");
ValidateOverviewHelpText(overviewHelpText, commandTypes);

var catalog = new MockerCliCatalog(
    DateTimeOffset.UtcNow,
    overviewHelpText,
    commands);

File.WriteAllText(
    outputPath,
    JsonSerializer.Serialize(catalog, new JsonSerializerOptions
    {
        WriteIndented = true
    }) + Environment.NewLine);

MockerCliCommand BuildCommand(Type commandType)
{
    var verb = commandType.GetCustomAttribute<VerbAttribute>() ??
               throw new InvalidOperationException($"Missing VerbAttribute on {commandType.FullName}");

    var positionals = DescribeValues(commandType).ToArray();
    var options = DescribeOptions(commandType).ToArray();
    var helpText = CaptureOutput(() => QaaS.Mocker.Bootstrap.New([verb.Name, "--help"]), $"{verb.Name} command");

    ValidateCommandHelpText(helpText, verb.Name, positionals, options);

    return new MockerCliCommand(
        verb.Name,
        NormalizeInlineHelpText(verb.HelpText, $"{verb.Name} description") ?? string.Empty,
        helpText,
        commandType.FullName ?? commandType.Name,
        positionals,
        options);
}

IEnumerable<MockerCliValue> DescribeValues(Type commandType)
{
    var instance = Activator.CreateInstance(commandType);
    foreach (var property in GetCommandProperties(commandType))
    {
        var attribute = property.GetCustomAttribute<ValueAttribute>();
        if (attribute is null)
        {
            continue;
        }

        var valueType = ToFriendlyTypeName(property.PropertyType);
        ValidateFriendlyTypeName(valueType, $"{commandType.FullName}.{property.Name}");

        yield return new MockerCliValue(
            attribute.Index,
            property.Name,
            valueType,
            attribute.Required || property.GetCustomAttribute<RequiredAttribute>() is not null,
            FormatDefaultValue(attribute.Default, property.GetValue(instance)),
            NormalizeInlineHelpText(attribute.HelpText, $"{commandType.FullName}.{property.Name}") ?? string.Empty,
            property.DeclaringType?.FullName ?? commandType.FullName ?? commandType.Name,
            property.DeclaringType != commandType);
    }
}

IEnumerable<MockerCliOption> DescribeOptions(Type commandType)
{
    var instance = Activator.CreateInstance(commandType);
    foreach (var property in GetCommandProperties(commandType))
    {
        var attribute = property.GetCustomAttribute<OptionAttribute>();
        if (attribute is null)
        {
            continue;
        }

        var valueType = ToFriendlyTypeName(property.PropertyType);
        ValidateFriendlyTypeName(valueType, $"{commandType.FullName}.{property.Name}");

        yield return new MockerCliOption(
            property.Name,
            GetShortName(attribute),
            attribute.LongName ?? string.Empty,
            valueType,
            attribute.Required || property.GetCustomAttribute<RequiredAttribute>() is not null,
            FormatDefaultValue(attribute.Default, property.GetValue(instance)),
            NormalizeInlineHelpText(attribute.HelpText, $"{commandType.FullName}.{property.Name}") ?? string.Empty,
            property.DeclaringType?.FullName ?? commandType.FullName ?? commandType.Name,
            property.DeclaringType != commandType);
    }
}

static IEnumerable<PropertyInfo> GetCommandProperties(Type commandType)
{
    var stack = new Stack<Type>();
    for (var current = commandType; current is not null && current != typeof(object); current = current.BaseType)
    {
        stack.Push(current);
    }

    while (stack.Count != 0)
    {
        var type = stack.Pop();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (property.GetIndexParameters().Length == 0)
            {
                yield return property;
            }
        }
    }
}

static string CaptureOutput(Func<object?> action, string context)
{
    var original = Console.Out;
    using var writer = new StringWriter();
    Console.SetOut(writer);
    try
    {
        var result = action();
        if (result is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    finally
    {
        Console.SetOut(original);
    }

    var normalized = NormalizeCapturedHelpText(writer.ToString());
    ValidateCapturedHelpText(normalized, context);
    return normalized;
}

static string NormalizeCapturedHelpText(string helpText)
{
    return string.Join(
        Environment.NewLine,
        TrimBlankLineList(RemoveHostBannerLines(helpText).ToList())
            .Select(line => line.TrimEnd()));
}

static string? NormalizeInlineHelpText(string? helpText, string context)
{
    if (string.IsNullOrWhiteSpace(helpText))
    {
        return null;
    }

    var normalized = string.Join(
        Environment.NewLine,
        TrimBlankLineList(SplitLines(helpText))
            .Select(line => line.TrimEnd()));

    ValidateInlineHelpText(normalized, context);
    return normalized;
}

static IEnumerable<string> RemoveHostBannerLines(string helpText)
{
    var rawLines = helpText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    for (var index = 0; index < rawLines.Length; index++)
    {
        var currentLine = rawLines[index];
        if ((currentLine.Contains("SnapshotHost", StringComparison.Ordinal) ||
             currentLine.Contains("CliExport", StringComparison.Ordinal)) &&
            index + 1 < rawLines.Length &&
            rawLines[index + 1].TrimStart().StartsWith("Copyright", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            continue;
        }

        yield return currentLine;
    }
}

static List<string> SplitLines(string helpText)
{
    return helpText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
}

static IReadOnlyList<string> TrimBlankLineList(List<string> lines)
{
    while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[0]))
    {
        lines.RemoveAt(0);
    }

    while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[^1]))
    {
        lines.RemoveAt(lines.Count - 1);
    }

    return lines;
}

static string? FormatDefaultValue(object? attributeDefaultValue, object? instanceValue)
{
    var effectiveValue = attributeDefaultValue ?? instanceValue;
    return effectiveValue switch
    {
        null => null,
        string text => text,
        bool booleanValue => booleanValue ? "True" : "False",
        IEnumerable<string> strings => strings.Any() ? string.Join(", ", strings) : "[]",
        IEnumerable enumerable when effectiveValue is not string => FormatEnumerableDefaultValue(enumerable),
        _ => effectiveValue.ToString()
    };
}

static string FormatEnumerableDefaultValue(IEnumerable enumerable)
{
    var values = enumerable.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray();
    return values.Length == 0 ? "[]" : string.Join(", ", values);
}

static string? GetShortName(OptionAttribute attribute)
{
    return string.IsNullOrWhiteSpace(attribute.ShortName) ? null : attribute.ShortName;
}

static string ToFriendlyTypeName(Type type)
{
    if (type == typeof(string))
    {
        return "string";
    }

    if (type == typeof(bool))
    {
        return "bool";
    }

    if (type == typeof(byte))
    {
        return "byte";
    }

    if (type == typeof(short))
    {
        return "short";
    }

    if (type == typeof(int))
    {
        return "int";
    }

    if (type == typeof(long))
    {
        return "long";
    }

    if (type == typeof(float))
    {
        return "float";
    }

    if (type == typeof(double))
    {
        return "double";
    }

    if (type == typeof(decimal))
    {
        return "decimal";
    }

    if (type == typeof(object))
    {
        return "object";
    }

    var nullableType = Nullable.GetUnderlyingType(type);
    if (nullableType is not null)
    {
        return $"{ToFriendlyTypeName(nullableType)}?";
    }

    if (type.IsArray)
    {
        return $"{ToFriendlyTypeName(type.GetElementType()!)}[]";
    }

    if (type.IsGenericType)
    {
        var genericTypeName = type.Name;
        var tickIndex = genericTypeName.IndexOf('`');
        if (tickIndex >= 0)
        {
            genericTypeName = genericTypeName[..tickIndex];
        }

        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(ToFriendlyTypeName));
        return $"{genericTypeName}<{genericArguments}>";
    }

    return type.Name;
}

static void ValidateOverviewHelpText(string helpText, IEnumerable<Type> commandTypes)
{
    foreach (var commandType in commandTypes)
    {
        var verb = commandType.GetCustomAttribute<VerbAttribute>();
        if (verb is null)
        {
            continue;
        }

        if (!helpText.Contains(verb.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Overview help text does not mention the `{verb.Name}` command.");
        }
    }
}

static void ValidateCommandHelpText(
    string helpText,
    string commandName,
    IEnumerable<MockerCliValue> positionals,
    IEnumerable<MockerCliOption> options)
{
    foreach (var positional in positionals)
    {
        if (!helpText.Contains($"value pos. {positional.Index}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Help text for `{commandName}` is missing positional argument `{positional.PropertyName}`.");
        }
    }

    foreach (var option in options)
    {
        if (string.IsNullOrWhiteSpace(option.LongName))
        {
            throw new InvalidOperationException($"Option `{commandName}.{option.PropertyName}` is missing a long name.");
        }

        if (!helpText.Contains($"--{option.LongName}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Help text for `{commandName}` is missing the `--{option.LongName}` flag.");
        }
    }
}

static void ValidateCapturedHelpText(string helpText, string context)
{
    if (string.IsNullOrWhiteSpace(helpText))
    {
        throw new InvalidOperationException($"{context} help text is empty.");
    }

    if (helpText.Contains("SnapshotHost", StringComparison.Ordinal) ||
        helpText.Contains("CliExport", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{context} help text still contains host banner content.");
    }

    if (helpText != helpText.Trim())
    {
        throw new InvalidOperationException($"{context} help text contains leading or trailing whitespace.");
    }
}

static void ValidateInlineHelpText(string helpText, string context)
{
    if (helpText != helpText.Trim())
    {
        throw new InvalidOperationException($"{context} help text contains leading or trailing whitespace.");
    }

    if (helpText.EndsWith("\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{context} help text ends with a stray quote.");
    }

    if (helpText.Contains("SnapshotHost", StringComparison.Ordinal) ||
        helpText.Contains("CliExport", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{context} help text contains host banner content.");
    }
}

static void ValidateFriendlyTypeName(string typeName, string context)
{
    if (string.IsNullOrWhiteSpace(typeName))
    {
        throw new InvalidOperationException($"{context} type name is empty.");
    }

    var disallowedFragments = new[]
    {
        "Version=",
        "Culture=",
        "PublicKeyToken=",
        "[[",
        "]]",
        "`"
    };

    if (disallowedFragments.Any(fragment => typeName.Contains(fragment, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException($"{context} type name is not human-readable: `{typeName}`.");
    }
}

static string GetOutputPath(IReadOnlyList<string> args)
{
    if (args.Count == 2 && string.Equals(args[0], "--output", StringComparison.OrdinalIgnoreCase))
    {
        return Path.GetFullPath(args[1]);
    }

    throw new InvalidOperationException("Usage: --output <path>");
}

internal sealed record MockerCliCatalog(
    DateTimeOffset GeneratedAtUtc,
    string OverviewHelpText,
    IReadOnlyList<MockerCliCommand> Commands);

internal sealed record MockerCliCommand(
    string Name,
    string Description,
    string HelpText,
    string SourceOptionType,
    IReadOnlyList<MockerCliValue> Positionals,
    IReadOnlyList<MockerCliOption> Options);

internal sealed record MockerCliValue(
    int Index,
    string PropertyName,
    string ValueType,
    bool Required,
    string? DefaultValue,
    string HelpText,
    string SourceOptionType,
    bool IsInherited);

internal sealed record MockerCliOption(
    string PropertyName,
    string? ShortName,
    string LongName,
    string ValueType,
    bool Required,
    string? DefaultValue,
    string HelpText,
    string SourceOptionType,
    bool IsInherited);
