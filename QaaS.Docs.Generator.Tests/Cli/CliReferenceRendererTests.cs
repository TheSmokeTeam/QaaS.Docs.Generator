using NUnit.Framework;
using QaaS.Docs.Generator.Cli;

namespace QaaS.Docs.Generator.Tests.Cli;

[TestFixture]
public class CliReferenceRendererTests
{
    [Test]
    public void RenderRunnerCommand_IncludesInvocationCategoriesAndExamples()
    {
        var command = new RunnerCliCommand(
            Name: "run",
            Description: "Run a qaas test according to the given configurations.",
            OptionType: "QaaS.Runner.Options.RunOptions",
            HelpText: "Usage:\n dotnet run -- run test.qaas.yaml",
            Positionals:
            [
                new RunnerCliArgument("value", "ConfigurationFile", "QaaS.Runner.Options.BaseOptions", "string", true, true, "test.qaas.yaml", null, null, 0, "Path to a qaas yaml configuration file to use with the command.")
            ],
            Options:
            [
                new RunnerCliArgument("option", "OverwriteArguments", "QaaS.Runner.Options.BaseOptions", "IList<string>", true, false, "[]", "r", "overwrite-arguments", null, "Overwrite values."),
                new RunnerCliArgument("option", "LoggerLevel", "QaaS.Framework.Executions.Options.LoggerOptions", "LogEventLevel?", true, false, null, "l", "logger-level", null, "Logger level.")
            ]);

        var content = new CliReferenceRenderer().RenderRunner(new RunnerCliCatalog("overview", [command]))
            .Single(document => document.RelativePath.EndsWith("/run.md", StringComparison.Ordinal))
            .Content;

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("## Invocation"));
            Assert.That(content, Does.Contain("dotnet run <dotnet-parameters> -- run <config-file> [flags]"));
            Assert.That(content, Does.Contain("| Category | Flag | Inherited | Required | Default | Value Type | Description |"));
            Assert.That(content, Does.Contain("| Configuration | `-r`, `--overwrite-arguments` |"));
            Assert.That(content, Does.Contain("| Logging | `-l`, `--logger-level` |"));
            Assert.That(content, Does.Contain("## Examples"));
        });
    }

    [Test]
    public void RenderMockerOverview_LinksCommandsAndShowsCommonFlags()
    {
        var run = new MockerCliCommand(
            Name: "run",
            Description: "Start the configured mock servers and optional controller runtime.",
            HelpText: "Usage:\n dotnet run -- run mocker.qaas.yaml",
            SourceOptionType: "QaaS.Mocker.Options.RunOptions",
            Positionals: [],
            Options:
            [
                new MockerCliOption("OverwriteFiles", "w", "overwrite-files", "IList<string>", false, "[]", "Overwrite files.", "QaaS.Mocker.Options.MockerOptions", true),
                new MockerCliOption("LoggerLevel", "l", "logger-level", "LogEventLevel?", false, null, "Logger level.", "QaaS.Framework.Executions.Options.LoggerOptions", true)
            ]);

        var template = new MockerCliCommand(
            Name: "template",
            Description: "Render the effective merged configuration after file, folder, argument, and environment overrides.",
            HelpText: "Usage:\n dotnet run -- template mocker.qaas.yaml",
            SourceOptionType: "QaaS.Mocker.Options.TemplateOptions",
            Positionals: [],
            Options:
            [
                new MockerCliOption("OverwriteFiles", "w", "overwrite-files", "IList<string>", false, "[]", "Overwrite files.", "QaaS.Mocker.Options.MockerOptions", true),
                new MockerCliOption("LoggerLevel", "l", "logger-level", "LogEventLevel?", false, null, "Logger level.", "QaaS.Framework.Executions.Options.LoggerOptions", true)
            ]);

        var content = new CliReferenceRenderer().RenderMocker(new MockerCliCatalog(DateTimeOffset.UnixEpoch, "overview", [run, template]))
            .Single(document => document.RelativePath.EndsWith("/commands.md", StringComparison.Ordinal))
            .Content;

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("| [`run`](./run.md) |"));
            Assert.That(content, Does.Contain("| [`template`](./template.md) |"));
            Assert.That(content, Does.Contain("## Common Flags"));
            Assert.That(content, Does.Contain("| Configuration | `-w`, `--overwrite-files` |"));
            Assert.That(content, Does.Contain("| Logging | `-l`, `--logger-level` |"));
        });
    }
}
