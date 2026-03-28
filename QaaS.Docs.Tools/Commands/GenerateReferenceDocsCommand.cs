using System.Text.RegularExpressions;
using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Regenerates or validates the deterministic Runner, Mocker, Framework, and hook reference pages plus mirrored
/// schema download assets.
/// </summary>
internal sealed class GenerateReferenceDocsCommand : ICommandHandler
{
    public async Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments)
    {
        var check = arguments.HasFlag("--check");
        var buildSite = arguments.HasFlag("--build-site");
        var skipCliSnapshotRefresh = arguments.HasFlag("--skip-cli-snapshot-refresh");

        if (!skipCliSnapshotRefresh)
        {
            await new RefreshCliSnapshotsCommand().ExecuteAsync(context, arguments);
        }

        foreach (var relativePath in new[]
                 {
                     Path.Combine("docs", "qaas", "functions", "configuration-as-code"),
                     Path.Combine("docs", "qaas", "functions", "getting-started"),
                     Path.Combine("docs", "qaas", "functions", "runtime"),
                     Path.Combine("docs", "mocker", "functions", "configuration-as-code"),
                     Path.Combine("docs", "mocker", "functions", "getting-started"),
                     Path.Combine("docs", "mocker", "functions", "runtime"),
                     Path.Combine("docs", "framework", "functions", "configuration"),
                     Path.Combine("docs", "framework", "functions", "framework-apis"),
                     Path.Combine("docs", "framework", "functions", "utilities"),
                     Path.Combine("docs", "hooks", "changeLog.md"),
                     Path.Combine("docs", "qaas", "changeLog.md"),
                     Path.Combine("docs", "mocker", "changeLog.md"),
                     Path.Combine("docs", "framework", "changeLog.md"),
                     Path.Combine("docs", "assertions", "changeLog.md"),
                     Path.Combine("docs", "generators", "changeLog.md"),
                     Path.Combine("docs", "probes", "changeLog.md"),
                     Path.Combine("docs", "processors", "changeLog.md")
                 })
        {
            RemoveOrCheckObsoleteGeneratedPath(Path.Combine(context.DocsRoot, relativePath), relativePath, check);
        }

        var generatorProject = Path.Combine(context.DocsRoot, "tools", "QaaS.Docs.Generator", "QaaS.Docs.Generator.csproj");
        if (!File.Exists(generatorProject))
        {
            throw new FileNotFoundException("QaaS.Docs.Generator is missing. Initialize the docs repo submodule before running this command.", generatorProject);
        }

        var generatorArguments = new List<string>
        {
            "run",
            "--project", generatorProject,
            "--configuration", "Release",
            "--",
            "--docs-root", context.DocsRoot,
            "--mirror-root", context.MirrorRoot,
            "--runner-root", context.RunnerRoot,
            "--mocker-root", context.MockerRoot,
            "--framework-root", context.FrameworkRoot
        };

        if (check)
        {
            generatorArguments.Add("--check");
        }

        await ProcessRunner.RunAsync("dotnet", generatorArguments, context.DocsRoot);

        await RestoreTrackedDocsPageIfSectionsMissingAsync(
            context.DocsRoot,
            Path.Combine("docs", "qaas", "functions", "index.md"),
            ["Builders", "Commands"],
            check);
        await RestoreTrackedDocsPageIfSectionsMissingAsync(
            context.DocsRoot,
            Path.Combine("docs", "mocker", "functions", "index.md"),
            ["Builders", "Commands"],
            check);

        await new UpdateHookOverviewsCommand().ExecuteAsync(context, arguments);
        await new SyncSchemaAssetsCommand().ExecuteAsync(context, arguments);

        if (buildSite)
        {
            await ProcessRunner.RunAsync("mkdocs", ["build"], context.DocsRoot);
        }

        return 0;
    }

    private static void RemoveOrCheckObsoleteGeneratedPath(string fullPath, string relativePath, bool check)
    {
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            return;
        }

        if (check)
        {
            throw new InvalidOperationException($"Obsolete generated docs path still exists: {relativePath}");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            return;
        }

        File.Delete(fullPath);
    }

    private static async Task RestoreTrackedDocsPageIfSectionsMissingAsync(
        string docsRoot,
        string relativePath,
        IReadOnlyList<string> requiredSections,
        bool check)
    {
        var fullPath = Path.Combine(docsRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        var currentContent = Utf8File.NormalizeLineEndings(await Utf8File.ReadAllTextAsync(fullPath));
        var missingSections = requiredSections
            .Where(section => !Regex.IsMatch(currentContent, $"(?m)^#{{2,3}} {Regex.Escape(section)}$"))
            .ToArray();
        if (missingSections.Length == 0)
        {
            return;
        }

        var gitRelativePath = relativePath.Replace('\\', '/');
        var trackedResult = await ProcessRunner.RunAsync(
            "git",
            ["-C", docsRoot, "show", $"HEAD:{gitRelativePath}"],
            docsRoot,
            throwOnFailure: false);
        if (trackedResult.ExitCode != 0 || string.IsNullOrWhiteSpace(trackedResult.StandardOutput))
        {
            throw new InvalidOperationException(
                $"Generated docs page '{relativePath}' is missing required sections ({string.Join(", ", missingSections)}) and no tracked fallback exists.");
        }

        var trackedContent = Utf8File.NormalizeLineEndings(trackedResult.StandardOutput);
        var trackedMissingSections = requiredSections
            .Where(section => !Regex.IsMatch(trackedContent, $"(?m)^#{{2,3}} {Regex.Escape(section)}$"))
            .ToArray();
        if (trackedMissingSections.Length != 0)
        {
            throw new InvalidOperationException(
                $"Tracked docs page '{relativePath}' is also missing required sections ({string.Join(", ", trackedMissingSections)}).");
        }

        if (check)
        {
            throw new InvalidOperationException($"Generated docs file is out of date: {relativePath}");
        }

        Console.Error.WriteLine(
            $"Warning: falling back to the tracked docs page for {relativePath} because the generated output is missing sections: {string.Join(", ", missingSections)}.");
        await Utf8File.WriteAllTextAsync(fullPath, trackedContent.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }
}
