using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QaaS.Docs.Generator.Functions;

namespace QaaS.Docs.Generator.Hooks;

internal sealed class HookOverviewRenderer
{
    private static readonly IReadOnlyDictionary<string, HookKindSpec> KindSpecs =
        new Dictionary<string, HookKindSpec>(StringComparer.Ordinal)
        {
            ["assertion"] = new("assertions/availableAssertions", "QaaS.Common.Assertions"),
            ["generator"] = new("generators/availableGenerators", "QaaS.Common.Generators"),
            ["probe"] = new("probes/availableProbes", "QaaS.Common.Probes"),
            ["processor"] = new("processors/availableProcessors", "QaaS.Common.Processors")
        };

    public async Task<IReadOnlyList<GeneratedDocument>> RenderAsync(string docsRoot, string mirrorRoot)
    {
        var workspaceRoot = ResolveWorkspaceRoot(docsRoot, mirrorRoot);
        var documents = new List<GeneratedDocument>();

        foreach (var kind in KindSpecs.Keys.OrderBy(candidate => candidate, StringComparer.Ordinal))
        {
            var spec = KindSpecs[kind];
            var hooksRoot = Path.Combine(
                docsRoot,
                "docs",
                spec.DocsRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(hooksRoot))
            {
                continue;
            }

            var sourceRoot = Path.Combine(workspaceRoot, spec.RepositoryDirectory);
            foreach (var hookDirectory in Directory.EnumerateDirectories(hooksRoot)
                         .OrderBy(candidate => candidate, StringComparer.Ordinal))
            {
                var docsSlug = Path.GetFileName(hookDirectory);
                var summary = await LoadHookSummaryAsync(sourceRoot, docsSlug);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    throw new InvalidOperationException(
                        $"Hook '{docsSlug}' in '{kind}' is missing a public XML summary in {sourceRoot}.");
                }

                documents.Add(new GeneratedDocument(
                    $"{spec.DocsRoot}/{docsSlug}/overview.md",
                    GeneratedDocumentHasher.WithHeader(
                        RenderOverviewPage(docsSlug, summary),
                        [kind, docsSlug, "overview"])));
            }
        }

        return documents;
    }

    private static string ResolveWorkspaceRoot(string docsRoot, string mirrorRoot)
    {
        foreach (var candidate in EnumerateWorkspaceRootCandidates(docsRoot, mirrorRoot))
        {
            if (KindSpecs.Values.All(spec => Directory.Exists(Path.Combine(candidate, spec.RepositoryDirectory))))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve a workspace root containing {string.Join(", ", KindSpecs.Values.Select(spec => spec.RepositoryDirectory))} from docs root '{docsRoot}' and mirror root '{mirrorRoot}'.");
    }

    private static IEnumerable<string> EnumerateWorkspaceRootCandidates(string docsRoot, string mirrorRoot)
    {
        static IEnumerable<string> Expand(string path)
        {
            var current = new DirectoryInfo(path);
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }

        return Expand(docsRoot)
            .Concat(Expand(mirrorRoot))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<string?> LoadHookSummaryAsync(string sourceRoot, string docsSlug)
    {
        var sourceFile = FindHookSourceFile(sourceRoot, docsSlug);
        if (sourceFile is null)
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(sourceFile);
        var syntaxTree = CSharpSyntaxTree.ParseText(text, path: sourceFile);
        var root = await syntaxTree.GetRootAsync();
        var typeDeclaration = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(candidate => string.Equals(candidate.Identifier.Text, docsSlug, StringComparison.Ordinal));
        if (typeDeclaration is null)
        {
            return null;
        }

        var summary = DocumentationCommentParser.Parse(typeDeclaration).Summary;
        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    private static string? FindHookSourceFile(string sourceRoot, string docsSlug)
    {
        return Directory.EnumerateFiles(sourceRoot, $"{docsSlug}.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsIgnoredPath(string sourceRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Tests", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Test", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "TestResults", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderOverviewPage(string title, string summary)
    {
        return string.Join(
            GeneratedDocumentLineEndings.Canonical,
            [
                $"# {title}",
                string.Empty,
                summary,
                string.Empty,
                "_This overview is generated automatically from the hook source summary._"
            ]);
    }

    private sealed record HookKindSpec(string DocsRoot, string RepositoryDirectory);
}
