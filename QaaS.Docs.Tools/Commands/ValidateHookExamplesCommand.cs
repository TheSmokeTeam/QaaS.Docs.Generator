using QaaS.Docs.Tools.Infrastructure;
using QaaS.Docs.Tools.Models;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Builds disposable Runner and Mocker hosts, injects the hook packages, and runs <c>template</c> against every
/// documented hook example.
/// </summary>
internal sealed class ValidateHookExamplesCommand : ICommandHandler
{
    public async Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments)
    {
        var skipBuild = arguments.HasFlag("--skip-build");
        var catalog = await HookOverviewCatalog.LoadAsync(context.ResourcesRoot);

        var runnerSolution = Path.Combine(context.RunnerRoot, "QaaS.Runner.sln");
        var mockerSolution = Path.Combine(context.MockerRoot, "QaaS.Mocker.sln");
        var assertionsProject = Path.Combine(
            context.AssertionsRoot,
            "QaaS.Common.Assertions",
            "QaaS.Common.Assertions.csproj"
        );
        var generatorsProject = Path.Combine(
            context.GeneratorsRoot,
            "QaaS.Common.Generators",
            "QaaS.Common.Generators.csproj"
        );
        var probesProject = Path.Combine(
            context.ProbesRoot,
            "QaaS.Common.Probes",
            "QaaS.Common.Probes.csproj"
        );
        var probesTestsProject = Path.Combine(
            context.ProbesRoot,
            "QaaS.Common.Probes.Tests",
            "QaaS.Common.Probes.Tests.csproj"
        );
        var processorsProject = Path.Combine(
            context.ProcessorsRoot,
            "QaaS.Common.Processors",
            "QaaS.Common.Processors.csproj"
        );
        var runnerHostProject = Path.Combine(
            context.RunnerRoot,
            "QaaS.Runner.E2ETests",
            "QaaS.Runner.E2ETests.csproj"
        );
        var mockerHostProject = Path.Combine(
            context.MockerRoot,
            "QaaS.Mocker.Example",
            "QaaS.Mocker.Example.csproj"
        );

        if (!skipBuild)
        {
            foreach (
                var target in new[]
                {
                    runnerSolution,
                    mockerSolution,
                    assertionsProject,
                    generatorsProject,
                    probesProject,
                    probesTestsProject,
                    processorsProject,
                }
            )
            {
                await ProcessRunner.RunAsync(
                    "dotnet",
                    ["build", target, "-c", "Release"],
                    Path.GetDirectoryName(target)!
                );
            }
        }

        var validationRoot = Path.Combine(
            context.WorkspaceRoot,
            "_tmp",
            "qaas-docs-hook-validation"
        );
        var runnerRuntimeDirectory = Path.Combine(validationRoot, "runner");
        var mockerRuntimeDirectory = Path.Combine(validationRoot, "mocker");

        await InitializeHostRuntimeAsync(
            GetProjectOutputDirectory(runnerHostProject),
            runnerRuntimeDirectory,
            [
                GetProjectBuildOutputDirectory(assertionsProject),
                GetProjectBuildOutputDirectory(generatorsProject),
                GetProjectBuildOutputDirectory(probesProject),
                GetProjectOutputDirectory(probesTestsProject),
            ]
        );
        await InitializeHostRuntimeAsync(
            GetProjectOutputDirectory(mockerHostProject),
            mockerRuntimeDirectory,
            [
                GetProjectBuildOutputDirectory(generatorsProject),
                GetProjectBuildOutputDirectory(processorsProject),
            ]
        );

        var runnerInvocation = GetExecutableInvocation(
            runnerRuntimeDirectory,
            "QaaS.Runner.E2ETests"
        );
        var mockerInvocation = GetExecutableInvocation(
            mockerRuntimeDirectory,
            "QaaS.Mocker.Example"
        );
        var failures = new List<string>();

        foreach (var entry in catalog.Entries)
        {
            try
            {
                if (string.Equals(entry.Runtime, "runner", StringComparison.OrdinalIgnoreCase))
                {
                    await InvokeHookValidationAsync(
                        entry,
                        runnerRuntimeDirectory,
                        runnerInvocation
                    );
                }
                else
                {
                    await InvokeHookValidationAsync(
                        entry,
                        mockerRuntimeDirectory,
                        mockerInvocation
                    );
                }

                Console.WriteLine($"Validated {entry.Kind}/{entry.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{entry.Kind}/{entry.Name}: {exception.Message}");
                Console.WriteLine($"Validation failed for {entry.Kind}/{entry.Name}");
            }
        }

        if (failures.Count != 0)
        {
            throw new InvalidOperationException(
                "Hook example validation failed:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, failures)
            );
        }

        Console.WriteLine($"Validated {catalog.Entries.Count} hook examples.");
        return 0;
    }

    private static string GetProjectOutputDirectory(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var runtimeConfig =
            Directory
                .EnumerateFiles(
                    Path.Combine(projectDirectory, "bin", "Release"),
                    $"{projectName}.runtimeconfig.json",
                    SearchOption.AllDirectories
                )
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Could not find Release output for {projectPath}"
            );

        return Path.GetDirectoryName(runtimeConfig)!;
    }

    private static string GetProjectBuildOutputDirectory(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var assembly =
            Directory
                .EnumerateFiles(
                    Path.Combine(projectDirectory, "bin", "Release"),
                    $"{assemblyName}.dll",
                    SearchOption.AllDirectories
                )
                .Where(path =>
                    path.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Could not find Release build output for {projectPath}"
            );

        return Path.GetDirectoryName(assembly)!;
    }

    private static async Task InitializeHostRuntimeAsync(
        string sourceDirectory,
        string destinationDirectory,
        IReadOnlyList<string> pluginPaths
    )
    {
        if (Directory.Exists(destinationDirectory))
        {
            Directory.Delete(destinationDirectory, recursive: true);
        }

        CopyDirectory(sourceDirectory, destinationDirectory);

        foreach (var pluginPath in pluginPaths)
        {
            if (Directory.Exists(pluginPath))
            {
                CopyDirectory(pluginPath, destinationDirectory);
                continue;
            }

            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(pluginPath));
            File.Copy(pluginPath, destinationPath, overwrite: true);
        }

        await Task.CompletedTask;
    }

    private static ExecutableInvocation GetExecutableInvocation(
        string outputDirectory,
        string projectName
    )
    {
        var exePath = Path.Combine(outputDirectory, $"{projectName}.exe");
        if (File.Exists(exePath))
        {
            return new ExecutableInvocation(exePath, []);
        }

        var dllPath = Path.Combine(outputDirectory, $"{projectName}.dll");
        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"Could not find executable output for {projectName} in {outputDirectory}"
            );
        }

        return new ExecutableInvocation("dotnet", [dllPath]);
    }

    private static async Task InvokeHookValidationAsync(
        HookOverviewEntry entry,
        string runtimeDirectory,
        ExecutableInvocation invocation
    )
    {
        var examplesDirectory = Path.Combine(runtimeDirectory, "examples", entry.Kind);
        Directory.CreateDirectory(examplesDirectory);
        var configFileName = $"{entry.Name}.qaas.yaml";
        var configPath = Path.Combine(examplesDirectory, configFileName);
        var relativeConfigPath =
            $".{Path.DirectorySeparatorChar}examples{Path.DirectorySeparatorChar}{entry.Kind}{Path.DirectorySeparatorChar}{configFileName}";
        var content = GetHookExampleContent(entry);
        await Utf8File.WriteAllTextAsync(configPath, content.TrimEnd() + Environment.NewLine);

        var arguments = invocation
            .PrefixArguments.Concat(["template", relativeConfigPath, "--no-env"])
            .ToList();
        if (string.Equals(entry.Runtime, "runner", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("--no-process-exit");
        }

        await ProcessRunner.RunAsync(invocation.FilePath, arguments, runtimeDirectory);
    }

    private static string GetHookExampleContent(HookOverviewEntry entry)
    {
        if (string.Equals(entry.Runtime, "runner", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                MetaData:
                  Team: Docs
                  System: HookValidation

                {{entry.YamlSnippet.Trim()}}
                """;
        }

        return entry.YamlSnippet.Trim();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(
                file,
                Path.Combine(destinationDirectory, Path.GetFileName(file)),
                overwrite: true
            );
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(
                directory,
                Path.Combine(destinationDirectory, Path.GetFileName(directory))
            );
        }
    }

    private sealed record ExecutableInvocation(
        string FilePath,
        IReadOnlyList<string> PrefixArguments
    );
}
