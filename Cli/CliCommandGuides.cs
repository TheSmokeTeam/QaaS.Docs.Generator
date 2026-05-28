namespace QaaS.Docs.Generator.Cli;

internal static class CliCommandGuides
{
    public static string RunnerOverviewSummary(string commandName) =>
        commandName switch
        {
            "run" =>
                "Run the full QaaS flow: sessions, assertions, reporting, and optional Allure serving.",
            "act" => "Execute only the session phase and persist SessionData for later inspection.",
            "assert" => "Evaluate only the assertion phase against existing SessionData.",
            "template" => "Render the fully resolved Runner configuration without executing it.",
            "execute" => "Run a YAML-defined sequence of Runner commands.",
            _ => "Command-specific execution path.",
        };

    public static string MockerOverviewSummary(string commandName) =>
        commandName switch
        {
            "run" => "Start the configured mock servers and optional controller runtime.",
            "template" =>
                "Render the effective merged mocker configuration without starting the runtime.",
            _ => "Mocker command.",
        };

    public static string RunnerInvocation(string commandName) =>
        commandName switch
        {
            "execute" => "dotnet run <dotnet-parameters> -- execute <executable-file> [flags]",
            _ => $"dotnet run <dotnet-parameters> -- {commandName} <config-file> [flags]",
        };

    public static string MockerInvocation(string commandName) =>
        $"dotnet run <dotnet-parameters> -- {commandName} <config-file> [flags]";

    public static IEnumerable<string> RunnerUseWhen(string commandName) =>
        commandName switch
        {
            "run" =>
            [
                "You want the standard end-to-end execution path.",
                "You need to limit the run to specific cases, sessions, or assertions without editing the base YAML.",
                "You want to layer temporary overrides on top of a checked-in configuration.",
            ],
            "act" =>
            [
                "You want to capture fresh SessionData without running assertions yet.",
                "You are debugging session behavior and need the produced artifacts first.",
                "You plan to follow with `assert` against the same stored data.",
            ],
            "assert" =>
            [
                "You already have SessionData from a previous run and want to re-check assertions only.",
                "You are iterating on assertion logic and do not want to re-run the session phase.",
                "You want to reproduce an assertion failure against a stable stored data set.",
            ],
            "template" =>
            [
                "You want to see the effective YAML after files, folders, references, placeholders, and environment overrides are resolved.",
                "You are validating a complicated configuration merge before running a real test.",
            ],
            "execute" =>
            [
                "You want to orchestrate several Runner commands from a single YAML file.",
                "You need stable IDs for filtering, logging, and report correlation across a multi-command flow.",
            ],
            _ => [],
        };

    public static IEnumerable<string> MockerUseWhen(string commandName) =>
        commandName switch
        {
            "run" =>
            [
                "You want to boot the mock environment from a committed mocker YAML file.",
                "You need to layer temporary overrides before starting the servers.",
                "You want a local interactive host by combining the command with `--run-locally`.",
            ],
            "template" =>
            [
                "You want to inspect the final server, controller, and stub configuration before starting the runtime.",
                "You need to confirm how overwrite files, folders, arguments, and environment variables combine.",
            ],
            _ => [],
        };

    public static IEnumerable<CliCommandExample> RunnerExamples(string commandName) =>
        commandName switch
        {
            "run" =>
            [
                new("Run the default configuration file", "dotnet run -- run test.qaas.yaml"),
                new(
                    "Run only one case and one session",
                    "dotnet run -- run test.qaas.yaml -c cases -n happy-path -i Checkout"
                ),
                new(
                    "Run and open an Allure 3 style report folder",
                    "dotnet run -- run test.qaas.yaml -s allure-report",
                    "Pass bare `-s` to serve the default `allure-results` folder instead."
                ),
            ],
            "act" =>
            [
                new(
                    "Run sessions and store the produced session data",
                    "dotnet run -- act test.qaas.yaml"
                ),
                new(
                    "Capture only a focused subset of the test",
                    "dotnet run -- act test.qaas.yaml -c cases -n happy-path -i Checkout"
                ),
            ],
            "assert" =>
            [
                new(
                    "Assert against the stored session data for a configuration",
                    "dotnet run -- assert test.qaas.yaml"
                ),
                new(
                    "Assert and open the results folder",
                    "dotnet run -- assert test.qaas.yaml -s allure-results"
                ),
            ],
            "template" =>
            [
                new("Print the resolved configuration", "dotnet run -- template test.qaas.yaml"),
                new(
                    "Preview the merged config with one extra file and one override",
                    "dotnet run -- template test.qaas.yaml -w local.qaas.yaml -r MetaData:Environment=qa"
                ),
            ],
            "execute" =>
            [
                new(
                    "Execute every command in the YAML file",
                    "dotnet run -- execute executable.yaml"
                ),
                new(
                    "Execute only selected command IDs",
                    "dotnet run -- execute executable.yaml -c smoke assert-only"
                ),
                new(
                    "Execute the flow and open the report folder",
                    "dotnet run -- execute executable.yaml -s allure-report"
                ),
            ],
            _ => [],
        };

    public static IEnumerable<CliCommandExample> MockerExamples(string commandName) =>
        commandName switch
        {
            "run" =>
            [
                new(
                    "Start the mocker with the default configuration",
                    "dotnet run -- run mocker.qaas.yaml"
                ),
                new(
                    "Start the mocker with local overrides",
                    "dotnet run -- run mocker.qaas.yaml -w local-overrides.yaml -r Server:Port=8081"
                ),
                new(
                    "Run locally and keep the host interactive",
                    "dotnet run -- run mocker.qaas.yaml --run-locally"
                ),
            ],
            "template" =>
            [
                new(
                    "Print the resolved mocker configuration",
                    "dotnet run -- template mocker.qaas.yaml"
                ),
                new(
                    "Render the merged config into an output folder",
                    "dotnet run -- template mocker.qaas.yaml -w local-overrides.yaml -o rendered"
                ),
            ],
            _ => [],
        };

    public static IEnumerable<CliMarkdownSection> RunnerSections(string commandName) =>
        commandName switch
        {
            "execute" =>
            [
                new(
                    "Executable File Format",
                    """
                    The executable YAML contains QaaS commands, not `dotnet run` invocations:

                    ```yaml
                    Commands:
                      - Command: template test.qaas.yaml
                        Id: preview
                      - Command: run test.qaas.yaml
                        Id: smoke
                        Parallel: false
                    ```

                    `Id` is the stable identifier used by `--command-ids-to-run`, logs, and generated report output.
                    """
                ),
                new(
                    "Flag Notes",
                    """
                    ### `-c`, `--command-ids-to-run`

                    Use one or more IDs from the executable file when you only want a subset of the declared commands to run.

                    ### `-s`, `--serve-results`

                    The top-level `execute` flag decides whether results are served after the flow completes. Embedded `serve-results` flags inside the YAML commands do not take over.
                    """
                ),
            ],
            "run" =>
            [
                new(
                    "Flag Notes",
                    """
                    ### `-r`, `--overwrite-arguments`

                    ```text
                    -r MetaData:Environment=qa
                    ```

                    ### `-p`, `--push-references`

                    Use pushed references when a list placeholder in the loaded configuration should be expanded from another YAML file.
                    """
                ),
                new(
                    "Parallelism",
                    """
                    The `run` command has no parallelism flag. Configure parallel sends on publisher actions with `Sessions[].Publishers[].Parallel.Parallelism`; the schema requires `Parallelism` to be at least `1`.

                    ```yaml
                    DataSources:
                      - Name: Payloads
                        Generator: FromFileSystem
                        GeneratorConfiguration:
                          DataArrangeOrder: AsciiAsc
                          FileSystem: { Path: Fixtures/payloads }
                    Sessions:
                      - Name: PublishLoad
                        Publishers:
                          - Name: SendCheckoutEvents
                            DataSourceNames: [Payloads]
                            Parallel:
                              Parallelism: 4
                            RabbitMq:
                              Host: localhost
                    ```

                    The C# equivalent is `PublisherBuilder.WithParallelism(int)`.
                    """
                ),
            ],
            "act" or "assert" or "template" =>
            [
                new(
                    "Flag Notes",
                    """
                    ### `-r`, `--overwrite-arguments`

                    ```text
                    -r MetaData:Environment=qa
                    ```

                    ### `-p`, `--push-references`

                    Use pushed references when a list placeholder in the loaded configuration should be expanded from another YAML file.
                    """
                ),
            ],
            _ => [],
        };

    public static IEnumerable<CliMarkdownSection> MockerSections(string commandName) =>
        commandName switch
        {
            "run" =>
            [
                new(
                    "Flag Notes",
                    """
                    ### `-r`, `--overwrite-arguments`

                    ```text
                    -r Server:Port=8081
                    ```

                    ### `--run-locally`

                    Use this when you want the host process to keep the mock runtime attached to the current console session.
                    """
                ),
            ],
            "template" =>
            [
                new(
                    "Flag Notes",
                    """
                    ### `-o`, `--output-folder`

                    Use an output folder when you want the rendered configuration written to disk for review or diffing.
                    """
                ),
            ],
            _ => [],
        };

    public static string RunnerFlagCategory(RunnerCliArgument option)
    {
        if (option.SourceOptionType.Contains("LoggerOptions", StringComparison.Ordinal))
        {
            return "Logging";
        }

        return option.LongName switch
        {
            "with-files"
            or "with-folders"
            or "overwrite-arguments"
            or "push-references"
            or "no-env"
            or "resolve-cases-last" => "Configuration",
            "cases"
            or "cases-names"
            or "cases-names-ignore"
            or "cases-name-patterns-ignore"
            or "session-names"
            or "session-categories"
            or "assertion-names"
            or "assertion-categories"
            or "command-ids-to-run" => "Selection",
            "serve-results" or "empty-results-directory" or "empty-allure-directory" => "Results",
            "no-process-exit" => "Runtime",
            _ => "General",
        };
    }

    public static string MockerFlagCategory(MockerCliOption option)
    {
        if (option.SourceOptionType.Contains("LoggerOptions", StringComparison.Ordinal))
        {
            return "Logging";
        }

        return option.LongName switch
        {
            "overwrite-files" or "overwrite-folders" or "overwrite-arguments" or "no-env" =>
                "Configuration",
            "output-folder" => "Output",
            "run-locally" => "Runtime",
            _ => "General",
        };
    }
}

internal sealed record CliCommandExample(
    string Title,
    string Code,
    string? Note = null,
    string Language = "bash"
);

internal sealed record CliMarkdownSection(string Title, string Content);
