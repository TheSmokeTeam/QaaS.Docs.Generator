using QaaS.Docs.Tools.Commands;
using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools;

/// <summary>
/// Hosts the documented docs-maintenance CLI that replaced the repository-owned PowerShell scripts.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Dispatches the requested docs-maintenance command.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var commandName = args[0];
        var commandArguments = CommandArguments.Parse(args.Skip(1));
        var context = DocsToolContext.Create(commandArguments);

        try
        {
            return commandName switch
            {
                "generate-reference-docs" => await new GenerateReferenceDocsCommand().ExecuteAsync(
                    context,
                    commandArguments
                ),
                "refresh-cli-snapshots" => await new RefreshCliSnapshotsCommand().ExecuteAsync(
                    context,
                    commandArguments
                ),
                "sync-schema-assets" => await new SyncSchemaAssetsCommand().ExecuteAsync(
                    context,
                    commandArguments
                ),
                "update-hook-overviews" => await new UpdateHookOverviewsCommand().ExecuteAsync(
                    context,
                    commandArguments
                ),
                "validate-hook-examples" => await new ValidateHookExamplesCommand().ExecuteAsync(
                    context,
                    commandArguments
                ),
                "--help" or "-h" or "help" => PrintHelp(commandArguments),
                _ => PrintUnknownCommand(commandName),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
    }

    /// <summary>
    /// Reports an unknown command and prints the global usage text.
    /// </summary>
    private static int PrintUnknownCommand(string commandName)
    {
        Console.Error.WriteLine($"Unknown command '{commandName}'.");
        PrintUsage();
        return 1;
    }

    /// <summary>
    /// Prints command-specific help when requested, otherwise prints the top-level usage text.
    /// </summary>
    private static int PrintHelp(CommandArguments arguments)
    {
        if (arguments.TryGetSingleValue("--command", out var commandName))
        {
            PrintCommandUsage(commandName);
            return 0;
        }

        PrintUsage();
        return 0;
    }

    /// <summary>
    /// Prints the top-level command list with a one-line description for each operation.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine(
            "Usage: dotnet run --project tools/QaaS.Docs.Generator/QaaS.Docs.Tools/QaaS.Docs.Tools.csproj -- <command> [options]"
        );
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine(
            "  generate-reference-docs  Regenerate or validate the full deterministic docs tree."
        );
        Console.WriteLine(
            "  refresh-cli-snapshots    Rebuild the committed Runner and Mocker CLI snapshot files."
        );
        Console.WriteLine(
            "  sync-schema-assets       Mirror stable schema download assets into docs/assets."
        );
        Console.WriteLine(
            "  update-hook-overviews    Enrich hook overview pages with What It Does and YAML sections."
        );
        Console.WriteLine(
            "  validate-hook-examples   Build local hosts and template every documented hook example."
        );
        Console.WriteLine();
        Console.WriteLine("Use 'help --command <name>' to print per-command options.");
    }

    /// <summary>
    /// Prints the options accepted by a single command.
    /// </summary>
    private static void PrintCommandUsage(string commandName)
    {
        var commonPathOptions = string.Join(
            Environment.NewLine,
            [
                "  --docs-root <path>",
                "  --mirror-root <path>",
                "  --runner-root <path>",
                "  --mocker-root <path>",
                "  --framework-root <path>",
                "  --assertions-root <path>",
                "  --generators-root <path>",
                "  --probes-root <path>",
                "  --processors-root <path>",
            ]
        );

        switch (commandName)
        {
            case "generate-reference-docs":
                Console.WriteLine("generate-reference-docs");
                Console.WriteLine(commonPathOptions);
                Console.WriteLine("  --skip-cli-snapshot-refresh");
                Console.WriteLine("  --check");
                Console.WriteLine("  --build-site");
                return;
            case "refresh-cli-snapshots":
                Console.WriteLine("refresh-cli-snapshots");
                Console.WriteLine(
                    string.Join(
                        Environment.NewLine,
                        [
                            "  --docs-root <path>",
                            "  --runner-root <path>",
                            "  --mocker-root <path>",
                            "  --framework-root <path>",
                        ]
                    )
                );
                return;
            case "sync-schema-assets":
                Console.WriteLine("sync-schema-assets");
                Console.WriteLine(
                    string.Join(
                        Environment.NewLine,
                        ["  --docs-root <path>", "  --mirror-root <path>", "  --check"]
                    )
                );
                return;
            case "update-hook-overviews":
                Console.WriteLine("update-hook-overviews");
                Console.WriteLine(
                    string.Join(Environment.NewLine, ["  --docs-root <path>", "  --check"])
                );
                return;
            case "validate-hook-examples":
                Console.WriteLine("validate-hook-examples");
                Console.WriteLine(commonPathOptions);
                Console.WriteLine("  --skip-build");
                return;
            default:
                Console.Error.WriteLine($"Unknown command '{commandName}'.");
                return;
        }
    }
}
