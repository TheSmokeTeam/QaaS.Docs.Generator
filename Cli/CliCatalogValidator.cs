namespace QaaS.Docs.Generator.Cli;

internal static class CliCatalogValidator
{
    private static readonly string[] DisallowedTypeFragments =
    [
        "Version=",
        "Culture=",
        "PublicKeyToken=",
        "[[",
        "]]",
        "`"
    ];

    public static RunnerCliCatalog Validate(RunnerCliCatalog catalog, string path)
    {
        ValidateCapturedHelpText(catalog.OverviewHelpText, $"{path} overview");

        foreach (var command in catalog.Commands)
        {
            ValidateRunnerCommand(command, path);
        }

        return catalog;
    }

    public static MockerCliCatalog Validate(MockerCliCatalog catalog, string path)
    {
        ValidateCapturedHelpText(catalog.OverviewHelpText, $"{path} overview");

        foreach (var command in catalog.Commands)
        {
            ValidateMockerCommand(command, path);
        }

        return catalog;
    }

    private static void ValidateRunnerCommand(RunnerCliCommand command, string path)
    {
        ValidateInlineText(command.Description, $"{path}:{command.Name} description");
        ValidateCapturedHelpText(command.HelpText, $"{path}:{command.Name} help output");

        foreach (var positional in command.Positionals)
        {
            ValidateFriendlyTypeName(positional.ValueType, $"{path}:{command.Name}:{positional.PropertyName} value type");
            ValidateOptionalInlineText(positional.HelpText, $"{path}:{command.Name}:{positional.PropertyName} help");
        }

        foreach (var option in command.Options)
        {
            ValidateFriendlyTypeName(option.ValueType, $"{path}:{command.Name}:{option.PropertyName} value type");
            ValidateOptionalInlineText(option.HelpText, $"{path}:{command.Name}:{option.PropertyName} help");
            ValidateShortName(option.ShortName, $"{path}:{command.Name}:{option.PropertyName} short name");

            if (string.IsNullOrWhiteSpace(option.LongName))
            {
                throw new InvalidOperationException($"{path}:{command.Name}:{option.PropertyName} is missing a long flag name.");
            }
        }
    }

    private static void ValidateMockerCommand(MockerCliCommand command, string path)
    {
        ValidateInlineText(command.Description, $"{path}:{command.Name} description");
        ValidateCapturedHelpText(command.HelpText, $"{path}:{command.Name} help output");

        foreach (var positional in command.Positionals)
        {
            ValidateFriendlyTypeName(positional.ValueType, $"{path}:{command.Name}:{positional.PropertyName} value type");
            ValidateInlineText(positional.HelpText, $"{path}:{command.Name}:{positional.PropertyName} help");
        }

        foreach (var option in command.Options)
        {
            ValidateFriendlyTypeName(option.ValueType, $"{path}:{command.Name}:{option.PropertyName} value type");
            ValidateInlineText(option.HelpText, $"{path}:{command.Name}:{option.PropertyName} help");
            ValidateShortName(option.ShortName, $"{path}:{command.Name}:{option.PropertyName} short name");

            if (string.IsNullOrWhiteSpace(option.LongName))
            {
                throw new InvalidOperationException($"{path}:{command.Name}:{option.PropertyName} is missing a long flag name.");
            }
        }
    }

    private static void ValidateFriendlyTypeName(string valueType, string context)
    {
        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new InvalidOperationException($"{context} is empty.");
        }

        if (DisallowedTypeFragments.Any(fragment => valueType.Contains(fragment, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{context} is not human-readable: `{valueType}`.");
        }
    }

    private static void ValidateCapturedHelpText(string value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{context} is empty.");
        }

        if (value != value.Trim())
        {
            throw new InvalidOperationException($"{context} contains leading or trailing whitespace.");
        }

        if (value.Contains("SnapshotHost", StringComparison.Ordinal) ||
            value.Contains("CliExport", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{context} still contains temporary host banner text.");
        }
    }

    private static void ValidateOptionalInlineText(string? value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ValidateInlineText(value, context);
    }

    private static void ValidateInlineText(string value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{context} is empty.");
        }

        if (value != value.Trim())
        {
            throw new InvalidOperationException($"{context} contains leading or trailing whitespace.");
        }

        if (value.EndsWith("\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{context} ends with a stray quote.");
        }

        if (value.Contains("SnapshotHost", StringComparison.Ordinal) ||
            value.Contains("CliExport", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{context} contains temporary host banner text.");
        }
    }

    private static void ValidateShortName(string? shortName, string context)
    {
        if (shortName is null)
        {
            return;
        }

        if (shortName.Length != 1)
        {
            throw new InvalidOperationException($"{context} must be a single character when present.");
        }
    }
}
