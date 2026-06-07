using QaaS.Docs.Tools.Infrastructure;
using QaaS.Docs.Tools.Models;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Rewrites generated hook overview pages into their final rendered form using the maintained prose catalog.
/// </summary>
internal sealed class UpdateHookOverviewsCommand : ICommandHandler
{
    private static readonly IReadOnlyDictionary<string, string> BaseDirectories = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["assertions"] = Path.Combine("docs", "assertions", "availableAssertions"),
        ["generators"] = Path.Combine("docs", "generators", "availableGenerators"),
        ["probes"] = Path.Combine("docs", "probes", "availableProbes"),
        ["processors"] = Path.Combine("docs", "processors", "availableProcessors"),
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
                Console.Error.WriteLine(
                    $"Warning: skipping hook overview enrichment for missing page: {fullPath}"
                );
                continue;
            }

            var summary = await GetOverviewSummaryAsync(context.DocsRoot, relativePath, fullPath);
            var content = RenderOverview(entry, summary);
            await SetOrCheckMarkdownAsync(fullPath, relativePath, content, check);
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

    private static async Task<string> GetOverviewSummaryAsync(
        string docsRoot,
        string relativePath,
        string path
    )
    {
        var rawContent = Utf8File.NormalizeLineEndings(await Utf8File.ReadAllTextAsync(path));
        if (await TryGetTrackedOverviewTldrAsync(docsRoot, relativePath) is { } trackedTldr)
        {
            return trackedTldr;
        }

        if (IsEnrichedHookOverview(rawContent) && TryGetExistingTldr(rawContent, out var existingTldr))
        {
            return existingTldr;
        }

        var content = MarkdownFrontmatter
            .Remove(rawContent)
            .Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Hook overview file is empty: {path}");
        }

        var lines = content.Split('\n').ToList();
        if (
            lines.Count != 0
            && lines[0].StartsWith("<!-- generated hash:", StringComparison.Ordinal)
        )
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
            throw new InvalidOperationException(
                $"Hook overview file does not contain a summary body: {path}"
            );
        }

        var headingIndex = body.IndexOf("\n## ", StringComparison.Ordinal);
        return headingIndex >= 0 ? body[..headingIndex].Trim() : body;
    }

    private static async Task<string?> TryGetTrackedOverviewTldrAsync(
        string docsRoot,
        string relativePath
    )
    {
        var result = await ProcessRunner.RunAsync(
            "git",
            ["-C", docsRoot, "show", $"HEAD:{relativePath.Replace('\\', '/')}"],
            docsRoot,
            throwOnFailure: false
        );
        if (
            result.ExitCode != 0
            || string.IsNullOrWhiteSpace(result.StandardOutput)
            || !IsEnrichedHookOverview(result.StandardOutput)
            || !TryGetExistingTldr(result.StandardOutput, out var trackedTldr)
        )
        {
            return null;
        }

        return trackedTldr;
    }

    private static bool IsEnrichedHookOverview(string content)
    {
        var normalized = Utf8File.NormalizeLineEndings(content);
        return normalized.Contains("\n## YAML configuration", StringComparison.Ordinal)
            && normalized.Contains("\n## Minimal example", StringComparison.Ordinal)
            && normalized.Contains("\n## Realistic example", StringComparison.Ordinal);
    }

    private static bool TryGetExistingTldr(string content, out string summary)
    {
        const string tldrPrefix = "> TL;DR";
        summary = string.Empty;

        foreach (var line in Utf8File.NormalizeLineEndings(content).Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(tldrPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            summary = trimmed[tldrPrefix.Length..]
                .Trim()
                .TrimStart('—', '-', ':')
                .Trim();
            return !string.IsNullOrWhiteSpace(summary);
        }

        return false;
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
        var hookDisplayName = entry.Kind.ToLowerInvariant() switch
        {
            "assertions" => "Assertions",
            "generators" => "Generators",
            "probes" => "Probes",
            "processors" => "Processors",
            _ => "Hooks",
        };

        return string.Join(
            "\n",
            [
                $"# {entry.Name}",
                string.Empty,
                $"> TL;DR — {summary.Trim()}",
                string.Empty,
                "## When to use",
                string.Empty,
                entry.WhatItDoes.Trim(),
                string.Empty,
                "## YAML configuration",
                string.Empty,
                "Use the hook name in the matching runtime section, then place hook-specific fields under the configuration object shown in the examples below.",
                string.Empty,
                "## Minimal example",
                string.Empty,
                "```yaml",
                entry.YamlSnippet.Trim(),
                "```",
                string.Empty,
                "## Realistic example",
                string.Empty,
                entry.ConfigExplanation.Trim(),
                string.Empty,
                "## Edge cases",
                string.Empty,
                "- Missing required configuration keys fail schema validation before the hook runs.",
                "- Keep hook names and referenced session or data-source names aligned with the surrounding YAML.",
                string.Empty,
                "## See also",
                string.Empty,
                "- [Configuration table](configuration/tableView.md)",
                "- [YAML scaffold](configuration/yamlView.md)",
                $"- [{hookDisplayName}](../../index.md)",
                string.Empty,
            ]
        );
    }

    private static async Task SetOrCheckMarkdownAsync(
        string path,
        string relativePath,
        string content,
        bool check
    )
    {
        var expected = MarkdownFrontmatter.ApplyExistingOrDefault(
            path,
            relativePath,
            Utf8File.NormalizeLineEndings(content)
        );
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

        await Utf8File.WriteAllTextAsync(
            path,
            expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)
        );
    }
}
