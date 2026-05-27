using System.Text;

namespace QaaS.Docs.Generator.Cli;

internal sealed class CliReferenceRenderer
{
    public IReadOnlyList<GeneratedDocument> RenderRunner(RunnerCliCatalog catalog)
    {
        var commandRoot = "qaas/userInterfaces/runner/commands";
        var documents = new List<GeneratedDocument>
        {
            new(
                $"{commandRoot}/commands.md",
                GeneratedDocumentHasher.WithHeader(RenderRunnerOverview(catalog), ["Runner", "cli-overview"]))
        };

        documents.AddRange(catalog.Commands
            .Select(command => new GeneratedDocument(
                $"{commandRoot}/{command.Name}.md",
                GeneratedDocumentHasher.WithHeader(RenderRunnerCommand(command), ["Runner", command.Name, "cli-command"]))));

        return documents;
    }

    public IReadOnlyList<GeneratedDocument> RenderMocker(MockerCliCatalog catalog)
    {
        var commandRoot = "mocker/userInterfaces/mocker/commands";
        var documents = new List<GeneratedDocument>
        {
            new(
                $"{commandRoot}/commands.md",
                GeneratedDocumentHasher.WithHeader(RenderMockerOverview(catalog), ["Mocker", "cli-overview"]))
        };

        documents.AddRange(catalog.Commands
            .Select(command => new GeneratedDocument(
                $"{commandRoot}/{command.Name}.md",
                GeneratedDocumentHasher.WithHeader(RenderMockerCommand(command), ["Mocker", command.Name, "cli-command"]))));

        return documents;
    }

    private static string RenderRunnerOverview(RunnerCliCatalog catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Commands");
        builder.AppendLine();
        builder.AppendLine("QaaS Runner commands all start from the same host process and then branch into focused execution modes depending on whether you want to inspect configuration, run sessions, evaluate assertions, or orchestrate multiple commands.");
        builder.AppendLine();
        builder.AppendLine("## Invocation Pattern");
        builder.AppendLine();
        AppendCodeBlock(builder, "bash", "dotnet run <dotnet-parameters> -- <command> [command-values] [command-flags]");
        builder.AppendLine();
        builder.AppendLine("## Available Commands");
        builder.AppendLine();
        builder.AppendLine("| Command | Description | Best For |");
        builder.AppendLine("| ------- | ----------- | -------- |");
        foreach (var command in catalog.Commands.OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"| [`{command.Name}`](./{command.Name}.md) | {Escape(command.Description)} | {Escape(CliCommandGuides.RunnerOverviewSummary(command.Name))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Common Flags");
        builder.AppendLine();
        builder.AppendLine("| Category | Flag | Default | Type | Description |");
        builder.AppendLine("| -------- | ---- | ------- | ---- | ----------- |");
        foreach (var option in GetCommonRunnerOptions(catalog))
        {
            builder.AppendLine($"| {CliCommandGuides.RunnerFlagCategory(option)} | `{FormatFlag(option.ShortName, option.LongName)}` | {Escape(option.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(option.ValueType)}` | {Escape(option.HelpText ?? string.Empty)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Working Style");
        builder.AppendLine();
        builder.AppendLine("- Use `template` to inspect the resolved YAML before you execute anything.");
        builder.AppendLine("- Use `run` for the standard end-to-end path.");
        builder.AppendLine("- Use `act` followed by `assert` when you want to split data capture from assertion evaluation.");
        builder.AppendLine("- Use `execute` when the workflow itself should be declared as YAML.");

        builder.AppendLine();
        builder.AppendLine("## Raw CLI Help");
        builder.AppendLine();
        AppendCodeBlock(builder, "text", NormalizeHelpText(catalog.OverviewHelpText));
        return builder.ToString().TrimEnd();
    }

    private static string RenderMockerOverview(MockerCliCatalog catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Commands");
        builder.AppendLine();
        builder.AppendLine("QaaS Mocker exposes a compact command surface: one command to start the runtime and one command to render the effective configuration without starting it.");
        builder.AppendLine();
        builder.AppendLine("## Invocation Pattern");
        builder.AppendLine();
        AppendCodeBlock(builder, "bash", "dotnet run <dotnet-parameters> -- <command> [command-values] [command-flags]");
        builder.AppendLine();
        builder.AppendLine("## Available Commands");
        builder.AppendLine();
        builder.AppendLine("| Command | Description | Best For |");
        builder.AppendLine("| ------- | ----------- | -------- |");
        foreach (var command in catalog.Commands.OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"| [`{command.Name}`](./{command.Name}.md) | {Escape(command.Description)} | {Escape(CliCommandGuides.MockerOverviewSummary(command.Name))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Common Flags");
        builder.AppendLine();
        builder.AppendLine("| Category | Flag | Default | Type | Description |");
        builder.AppendLine("| -------- | ---- | ------- | ---- | ----------- |");
        foreach (var option in GetCommonMockerOptions(catalog))
        {
            builder.AppendLine($"| {CliCommandGuides.MockerFlagCategory(option)} | `{FormatFlag(option.ShortName, option.LongName)}` | {Escape(option.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(option.ValueType)}` | {Escape(option.HelpText)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Working Style");
        builder.AppendLine();
        builder.AppendLine("- Use `template` when you want to verify the final merged configuration before you boot the runtime.");
        builder.AppendLine("- Use `run` when you are ready to start the configured mock servers and optional controller process.");

        builder.AppendLine();
        builder.AppendLine("## Raw CLI Help");
        builder.AppendLine();
        AppendCodeBlock(builder, "text", NormalizeHelpText(catalog.OverviewHelpText));
        return builder.ToString().TrimEnd();
    }

    private static string RenderRunnerCommand(RunnerCliCommand command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {command.Name}");
        builder.AppendLine();
        builder.AppendLine(command.Description);
        builder.AppendLine();
        builder.AppendLine("## Invocation");
        builder.AppendLine();
        AppendCodeBlock(builder, "bash", CliCommandGuides.RunnerInvocation(command.Name));
        AppendBullets(builder, "Use When", CliCommandGuides.RunnerUseWhen(command.Name));

        if (command.Positionals.Count != 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Positional Arguments");
            builder.AppendLine();
            builder.AppendLine("| Position | Property | Source Type | Required | Default | Value Type | Description |");
            builder.AppendLine("| -------- | -------- | ----------- | -------- | ------- | ---------- | ----------- |");
            foreach (var positional in command.Positionals.OrderBy(argument => argument.Position))
            {
                builder.AppendLine(
                    $"| `{positional.Position}` | `{positional.PropertyName}` | `{TypeDisplayFormatter.FormatSourceType(positional.SourceOptionType)}` | {YesNo(positional.Required)} | {Escape(positional.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(positional.ValueType)}` | {Escape(positional.HelpText ?? string.Empty)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Flags");
        builder.AppendLine();
        builder.AppendLine("| Category | Flag | Inherited | Required | Default | Value Type | Description |");
        builder.AppendLine("| -------- | ---- | --------- | -------- | ------- | ---------- | ----------- |");
        foreach (var option in command.Options.OrderBy(argument => argument.LongName, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| {CliCommandGuides.RunnerFlagCategory(option)} | `{FormatFlag(option.ShortName, option.LongName)}` | {YesNo(option.IsInherited)} | {YesNo(option.Required)} | {Escape(option.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(option.ValueType)}` | {Escape(option.HelpText ?? string.Empty)} |");
        }

        foreach (var section in CliCommandGuides.RunnerSections(command.Name))
        {
            builder.AppendLine();
            builder.AppendLine($"## {section.Title}");
            builder.AppendLine();
            builder.AppendLine(section.Content);
        }

        AppendExamples(builder, CliCommandGuides.RunnerExamples(command.Name));

        builder.AppendLine();
        builder.AppendLine("## Raw CLI Help");
        builder.AppendLine();
        AppendCodeBlock(builder, "text", NormalizeHelpText(command.HelpText));
        return builder.ToString().TrimEnd();
    }

    private static string RenderMockerCommand(MockerCliCommand command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {command.Name}");
        builder.AppendLine();
        builder.AppendLine(command.Description);
        builder.AppendLine();
        builder.AppendLine("## Invocation");
        builder.AppendLine();
        AppendCodeBlock(builder, "bash", CliCommandGuides.MockerInvocation(command.Name));
        AppendBullets(builder, "Use When", CliCommandGuides.MockerUseWhen(command.Name));

        if (command.Positionals.Count != 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Positional Arguments");
            builder.AppendLine();
            builder.AppendLine("| Position | Property | Source Type | Inherited | Required | Default | Value Type | Description |");
            builder.AppendLine("| -------- | -------- | ----------- | --------- | -------- | ------- | ---------- | ----------- |");
            foreach (var positional in command.Positionals.OrderBy(argument => argument.Index))
            {
                builder.AppendLine(
                    $"| `{positional.Index}` | `{positional.PropertyName}` | `{TypeDisplayFormatter.FormatSourceType(positional.SourceOptionType)}` | {YesNo(positional.IsInherited)} | {YesNo(positional.Required)} | {Escape(positional.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(positional.ValueType)}` | {Escape(positional.HelpText)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Flags");
        builder.AppendLine();
        builder.AppendLine("| Category | Flag | Inherited | Required | Default | Value Type | Description |");
        builder.AppendLine("| -------- | ---- | --------- | -------- | ------- | ---------- | ----------- |");
        foreach (var option in command.Options.OrderBy(argument => argument.LongName, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| {CliCommandGuides.MockerFlagCategory(option)} | `{FormatFlag(option.ShortName, option.LongName)}` | {YesNo(option.IsInherited)} | {YesNo(option.Required)} | {Escape(option.DefaultValue ?? string.Empty)} | `{TypeDisplayFormatter.FormatValueType(option.ValueType)}` | {Escape(option.HelpText)} |");
        }

        foreach (var section in CliCommandGuides.MockerSections(command.Name))
        {
            builder.AppendLine();
            builder.AppendLine($"## {section.Title}");
            builder.AppendLine();
            builder.AppendLine(section.Content);
        }

        AppendExamples(builder, CliCommandGuides.MockerExamples(command.Name));

        builder.AppendLine();
        builder.AppendLine("## Raw CLI Help");
        builder.AppendLine();
        AppendCodeBlock(builder, "text", NormalizeHelpText(command.HelpText));
        return builder.ToString().TrimEnd();
    }

    private static void AppendBullets(StringBuilder builder, string title, IEnumerable<string> bullets)
    {
        var values = bullets.ToArray();
        if (values.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var bullet in values)
        {
            builder.AppendLine($"- {bullet}");
        }
    }

    private static void AppendExamples(StringBuilder builder, IEnumerable<CliCommandExample> examples)
    {
        var values = examples.ToArray();
        if (values.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("## Examples");
        builder.AppendLine();
        foreach (var example in values)
        {
            builder.AppendLine($"### {example.Title}");
            builder.AppendLine();
            AppendCodeBlock(builder, example.Language, example.Code);
            if (!string.IsNullOrWhiteSpace(example.Note))
            {
                builder.AppendLine();
                builder.AppendLine(example.Note);
            }

            builder.AppendLine();
        }

        if (builder.Length >= Environment.NewLine.Length)
        {
            builder.Length -= Environment.NewLine.Length;
        }
    }

    private static void AppendCodeBlock(StringBuilder builder, string language, string content)
    {
        builder.AppendLine($"```{language}");
        builder.AppendLine(content.TrimEnd());
        builder.AppendLine("```");
    }

    private static IReadOnlyList<RunnerCliArgument> GetCommonRunnerOptions(RunnerCliCatalog catalog)
    {
        return catalog.Commands
            .Select(command => command.Options.Select(option => option.LongName).ToHashSet(StringComparer.Ordinal))
            .Aggregate((left, right) =>
            {
                left.IntersectWith(right);
                return left;
            })
            .Let(commonOptions => catalog.Commands
                .SelectMany(command => command.Options)
                .Where(option => option.LongName is not null && commonOptions.Contains(option.LongName))
                .GroupBy(option => option.LongName, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(option => option.LongName, StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<MockerCliOption> GetCommonMockerOptions(MockerCliCatalog catalog)
    {
        return catalog.Commands
            .Select(command => command.Options.Select(option => option.LongName).ToHashSet(StringComparer.Ordinal))
            .Aggregate((left, right) =>
            {
                left.IntersectWith(right);
                return left;
            })
            .Let(commonOptions => catalog.Commands
                .SelectMany(command => command.Options)
                .Where(option => commonOptions.Contains(option.LongName))
                .GroupBy(option => option.LongName, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(option => option.LongName, StringComparer.Ordinal)
                .ToArray());
    }

    private static string FormatFlag(string? shortName, string? longName)
    {
        return shortName is null || string.IsNullOrWhiteSpace(shortName)
            ? $"--{longName}"
            : $"-{shortName}`, `--{longName}";
    }

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string Escape(string value)
    {
        return MarkdownTableCellFormatter.Format(value);
    }

    private static string NormalizeHelpText(string helpText)
    {
        var rawLines = helpText
            .TrimEnd()
            .Split(["\r\n", "\n"], StringSplitOptions.None);

        var lines = new List<string>();
        for (var index = 0; index < rawLines.Length; index++)
        {
            var line = rawLines[index];
            if ((line.Contains("CliExport", StringComparison.Ordinal) ||
                 line.Contains("SnapshotHost", StringComparison.Ordinal)) &&
                index + 1 < rawLines.Length &&
                rawLines[index + 1].StartsWith("Copyright", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            lines.Add(line);
        }

        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        return string.Join(Environment.NewLine, lines);
    }
}

internal static class CliReferenceRendererExtensions
{
    public static TResult Let<TValue, TResult>(this TValue value, Func<TValue, TResult> selector) => selector(value);
}
