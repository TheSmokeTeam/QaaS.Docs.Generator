namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Resolves the repository and sibling workspace paths that the docs commands operate on.
/// </summary>
internal sealed record DocsToolContext(
    string DocsRoot,
    string WorkspaceRoot,
    string MirrorRoot,
    string RunnerRoot,
    string MockerRoot,
    string FrameworkRoot,
    string AssertionsRoot,
    string GeneratorsRoot,
    string ProbesRoot,
    string ProcessorsRoot,
    string ResourcesRoot)
{
    /// <summary>
    /// Resolves the docs repository root plus the sibling repository paths used by the docs commands.
    /// </summary>
    public static DocsToolContext Create(CommandArguments arguments)
    {
        var docsRoot = arguments.GetOptionalPath("--docs-root") ?? FindDocsRoot();
        var workspaceRoot = Path.GetFullPath(Path.Combine(docsRoot, ".."));
        var resourcesRoot = FindResourcesRoot();

        return new DocsToolContext(
            docsRoot,
            workspaceRoot,
            arguments.GetOptionalPath("--mirror-root") ?? Path.Combine(workspaceRoot, "QaaS.PackageMirror"),
            arguments.GetOptionalPath("--runner-root") ?? Path.Combine(workspaceRoot, "QaaS.Runner"),
            arguments.GetOptionalPath("--mocker-root") ?? Path.Combine(workspaceRoot, "QaaS.Mocker"),
            arguments.GetOptionalPath("--framework-root") ?? Path.Combine(workspaceRoot, "QaaS.Framework"),
            arguments.GetOptionalPath("--assertions-root") ?? Path.Combine(workspaceRoot, "QaaS.Common.Assertions"),
            arguments.GetOptionalPath("--generators-root") ?? Path.Combine(workspaceRoot, "QaaS.Common.Generators"),
            arguments.GetOptionalPath("--probes-root") ?? Path.Combine(workspaceRoot, "QaaS.Common.Probes"),
            arguments.GetOptionalPath("--processors-root") ?? Path.Combine(workspaceRoot, "QaaS.Common.Processors"),
            resourcesRoot);
    }

    /// <summary>
    /// Finds the qaas-docs repository root from the compiled tool output location.
    /// </summary>
    private static string FindDocsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "mkdocs.yml")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the qaas-docs repository root.");
    }

    /// <summary>
    /// Locates the embedded resource directory regardless of whether the tool is run from source or build output.
    /// </summary>
    private static string FindResourcesRoot()
    {
        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..")
        }
        .Select(path => Path.GetFullPath(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var searchRoot in searchRoots)
        {
            foreach (var candidate in EnumerateCandidateDirectories(searchRoot))
            {
                var resourceFile = Path.Combine(candidate, "hook-overviews.json");
                if (File.Exists(resourceFile))
                {
                    return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the QaaS.Docs.Tools resource catalog starting from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Enumerates the supported resource directory layouts used in source and build output.
    /// </summary>
    private static IEnumerable<string> EnumerateCandidateDirectories(string root)
    {
        yield return Path.Combine(root, "Resources");
        yield return Path.Combine(root, "QaaS.Docs.Tools", "Resources");
        yield return Path.Combine(root, "tools", "QaaS.Docs.Generator", "QaaS.Docs.Tools", "Resources");
    }
}
