using QaaS.Docs.Tools.Infrastructure;
using QaaS.Docs.Tools.Models;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Rewrites generated hook overview pages into their final rendered form using the maintained prose catalog.
/// </summary>
internal sealed class UpdateHookOverviewsCommand : ICommandHandler
{
    private static readonly IReadOnlyDictionary<string, string> BaseDirectories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["assertions"] = Path.Combine("docs", "assertions", "availableAssertions"),
            ["generators"] = Path.Combine("docs", "generators", "availableGenerators"),
            ["probes"] = Path.Combine("docs", "probes", "availableProbes"),
            ["processors"] = Path.Combine("docs", "processors", "availableProcessors")
        };

    public async Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments)
    {
        var check = arguments.HasFlag("--check");
        var catalog = await HookOverviewCatalog.LoadAsync(context.ResourcesRoot);

        foreach (var entry in catalog.Entries)
        {
            var relativePath = GetRelativePath(entry);
            var fullPath = Path.Combine(context.DocsRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"Warning: skipping hook overview enrichment for missing page: {fullPath}");
                continue;
            }

            var summary = await GetOverviewSummaryAsync(fullPath);
            var content = RenderOverview(entry, summary);
            await SetOrCheckMarkdownAsync(fullPath, content, check);
        }

        return 0;
    }

    private static string GetRelativePath(HookOverviewEntry entry)
    {
        if (!BaseDirectories.TryGetValue(entry.Kind, out var baseDirectory))
        {
            throw new InvalidOperationException($"Unsupported hook kind '{entry.Kind}'.");
        }

        return Path.Combine(baseDirectory, entry.Name, "overview.md");
    }

    private static async Task<string> GetOverviewSummaryAsync(string path)
    {
        var content = Utf8File.NormalizeLineEndings(await Utf8File.ReadAllTextAsync(path)).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Hook overview file is empty: {path}");
        }

        var lines = content.Split('\n').ToList();
        if (lines.Count != 0 && lines[0].StartsWith("<!-- generated hash:", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        TrimBlankEdges(lines);

        if (lines.Count != 0 && lines[0].StartsWith("# ", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        TrimBlankEdges(lines);

        lines = lines
            .Where(line => !line.StartsWith("> Logical group: ", StringComparison.Ordinal))
            .ToList();

        var body = string.Join("\n", lines).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException($"Hook overview file does not contain a summary body: {path}");
        }

        var headingIndex = body.IndexOf("\n## ", StringComparison.Ordinal);
        return headingIndex >= 0
            ? body[..headingIndex].Trim()
            : body;
    }

    private static void TrimBlankEdges(List<string> lines)
    {
        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count != 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private static string RenderOverview(HookOverviewEntry entry, string summary)
    {
        return string.Join(
            "\n",
            [
                $"# {entry.Name}",
                string.Empty,
                summary.Trim(),
                string.Empty,
                "## What It Does",
                string.Empty,
                entry.WhatItDoes.Trim(),
                string.Empty,
                "## YAML Example",
                string.Empty,
                "```yaml",
                entry.YamlSnippet.Trim(),
                "```",
                string.Empty,
                "## What This Configuration Does",
                string.Empty,
                entry.ConfigExplanation.Trim(),
                string.Empty
            ]);
    }

    private static async Task SetOrCheckMarkdownAsync(string path, string content, bool check)
    {
        var expected = Utf8File.NormalizeLineEndings(content);
        var current = File.Exists(path)
            ? Utf8File.NormalizeLineEndings(await Utf8File.ReadAllTextAsync(path))
            : null;

        if (check)
        {
            if (!string.Equals(expected, current, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Hook overview docs are out of date: {path}");
            }

            return;
        }

        await Utf8File.WriteAllTextAsync(path, expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }
}
