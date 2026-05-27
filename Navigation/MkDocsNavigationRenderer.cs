using System.Text;
using System.Text.RegularExpressions;
using QaaS.Docs.Generator;

namespace QaaS.Docs.Generator.Navigation;

/// <summary>
/// Rebuilds the generated portions of <c>mkdocs.yml</c> and the generated function
/// section pages so the published docs navigation reflects the grouped structure of
/// the rendered reference docs instead of a hand-maintained flat list.
/// </summary>
internal sealed class MkDocsNavigationRenderer
{
    private static readonly HookNavSpec[] HookSpecs =
    [
        new("hook-assertions", "assertions/index.md", "assertions", "Available Assertions", 6),
        new("hook-generators", "generators/index.md", "generators", "Available Generators", 6),
        new("hook-probes", "probes/index.md", "probes", "Available Probes", 6),
        new("hook-processors", "processors/index.md", "processors", "Available Processors", 6)
    ];

    private static readonly FunctionNavSpec[] FunctionSpecs =
    [
        new("runner-functions", "qaas/functions/index.md", 6),
        new("mocker-functions", "mocker/functions/index.md", 6),
        new("framework-functions", "framework/functions/index.md", 6)
    ];

    private static readonly Regex AvailableFunctionsHeadingRegex = new("^## Available Functions\\s*$", RegexOptions.Compiled);
    private static readonly Regex OverviewGroupHeadingRegex = new("^### (?<title>.+?)\\s*$", RegexOptions.Compiled);
    private static readonly Regex OverviewLinkRegex = new("^- \\[(?<title>.+?)\\]\\((?<path>.+?)\\)\\s*$", RegexOptions.Compiled);
    private static readonly Regex HookGroupHeadingRegex = new("^### (?<title>.+?)\\s*$", RegexOptions.Compiled);
    private static readonly Regex HookLinkRegex = new("^- \\[(?<title>.+?)\\]\\((?<path>.+?/overview\\.md)\\):", RegexOptions.Compiled);
    private static readonly Regex HeadingRegex = new("^(?<hashes>#{1,6}) (?<title>.+?)\\s*$", RegexOptions.Compiled);
    private static readonly Regex SlugCleanupRegex = new("[^a-z0-9]+", RegexOptions.Compiled);

    public void Update(string docsRoot, bool check)
    {
        var mkDocsPath = Path.Combine(docsRoot, "mkdocs.yml");
        var currentContent = GeneratedDocumentLineEndings.Normalize(File.ReadAllText(mkDocsPath));
        var expectedContent = currentContent;

        foreach (var functionSpec in FunctionSpecs)
        {
            var functionGroups = ParseFunctionGroups(docsRoot, functionSpec);
            WriteFunctionSectionPages(docsRoot, functionGroups, check);
            expectedContent = ReplaceMarkedBlock(
                expectedContent,
                functionSpec.Key,
                RenderFunctionBlock(functionGroups, functionSpec));
        }

        foreach (var hookSpec in HookSpecs)
        {
            expectedContent = ReplaceMarkedBlock(
                expectedContent,
                hookSpec.Key,
                RenderHookBlock(docsRoot, hookSpec));
        }

        expectedContent = GeneratedDocumentLineEndings.Normalize(expectedContent);

        if (!expectedContent.EndsWith(GeneratedDocumentLineEndings.Canonical, StringComparison.Ordinal))
        {
            expectedContent += GeneratedDocumentLineEndings.Canonical;
        }

        if (check)
        {
            if (!string.Equals(currentContent, expectedContent, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("mkdocs.yml is out of date. Regenerate docs navigation.");
            }

            return;
        }

        File.WriteAllText(mkDocsPath, expectedContent);
    }

    private static string RenderHookBlock(string docsRoot, HookNavSpec spec)
    {
        var groups = ParseHookGroups(docsRoot, spec);
        var indent = Indent(spec.Indentation);
        var builder = new StringBuilder();

        builder.AppendLine($"{indent}# qaas-docs-generator start: {spec.Key}");
        builder.AppendLine($"{indent}- {spec.AvailableLabel}:");

        foreach (var group in groups)
        {
            builder.AppendLine($"{indent}  - {group.Title}:");

            foreach (var hook in group.Hooks)
            {
                builder.AppendLine($"{indent}    - {hook.Title}:");
                builder.AppendLine($"{indent}      - Overview: {hook.OverviewPath}");
                builder.AppendLine($"{indent}      - Table View: {hook.ConfigurationRoot}/configuration/tableView.md");
                builder.AppendLine($"{indent}      - YAML View: {hook.ConfigurationRoot}/configuration/yamlView.md");
            }
        }

        builder.Append($"{indent}# qaas-docs-generator end: {spec.Key}");
        return builder.ToString();
    }

    private static string RenderFunctionBlock(IReadOnlyList<FunctionGroup> groups, FunctionNavSpec spec)
    {
        var indent = Indent(spec.Indentation);
        var builder = new StringBuilder();

        builder.AppendLine($"{indent}# qaas-docs-generator start: {spec.Key}");

        foreach (var group in groups)
        {
            builder.AppendLine($"{indent}- {group.Title}:");

            // When a group has exactly one page whose title equals the group
            // title, the wrapper page level duplicates the group label. Flatten
            // the page into the group so the nav reads as a single section
            // rather than "Extension Methods / Extension Methods / Overview".
            var pageIndent = indent + "  ";
            var flattenSinglePage =
                group.Pages.Count == 1
                && string.Equals(group.Pages[0].Title, group.Title, StringComparison.Ordinal);

            foreach (var page in group.Pages)
            {
                if (page.ParsedPage.Sections.Count == 0)
                {
                    if (flattenSinglePage)
                    {
                        builder.AppendLine($"{pageIndent}- Overview: {page.RelativePath}");
                    }
                    else
                    {
                        builder.AppendLine($"{pageIndent}- {page.Title}: {page.RelativePath}");
                    }
                    continue;
                }

                int sectionIndentation;
                if (flattenSinglePage)
                {
                    builder.AppendLine($"{pageIndent}- Overview: {page.RelativePath}");
                    sectionIndentation = spec.Indentation + 2;
                }
                else
                {
                    builder.AppendLine($"{pageIndent}- {page.Title}:");
                    builder.AppendLine($"{pageIndent}  - Overview: {page.RelativePath}");
                    sectionIndentation = spec.Indentation + 4;
                }

                foreach (var section in page.ParsedPage.Sections)
                {
                    RenderSectionNav(builder, page, section, sectionIndentation, []);
                }
            }
        }

        builder.Append($"{indent}# qaas-docs-generator end: {spec.Key}");
        return builder.ToString();
    }

    private static void RenderSectionNav(
        StringBuilder builder,
        FunctionPage page,
        ParsedFunctionSection section,
        int indentation,
        IReadOnlyList<string> ancestors)
    {
        var indent = Indent(indentation);
        var sectionPath = GetSectionRelativePath(page.RelativePath, ancestors.Append(section.Slug).ToList());

        // Special-case: the "Extension Methods" overview page contains a top-
        // level "## Extension Methods" section that re-declares the page
        // label. MkDocs renders the resulting nav node as "Extension Methods /
        // Extension Methods / Overview" — collapse the inner label to
        // "Methods" so the nav reads as a single section. The on-page H2 is
        // left as-is.
        var sectionTitle =
            ancestors.Count == 0
            && string.Equals(section.Title, "Extension Methods", StringComparison.Ordinal)
            && string.Equals(page.Title, "Extension Methods", StringComparison.Ordinal)
                ? "Methods"
                : section.Title;

        if (section.Children.Count == 0)
        {
            builder.AppendLine($"{indent}- {sectionTitle}: {sectionPath}");
            return;
        }

        builder.AppendLine($"{indent}- {sectionTitle}:");
        builder.AppendLine($"{indent}  - Overview: {sectionPath}");

        foreach (var child in section.Children)
        {
            RenderSectionNav(builder, page, child, indentation + 2, ancestors.Append(section.Slug).ToList());
        }
    }

    private static IReadOnlyList<HookGroup> ParseHookGroups(string docsRoot, HookNavSpec spec)
    {
        var indexPath = Path.Combine(docsRoot, "docs", spec.IndexRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var groups = new List<HookGroup>();
        HookGroup? currentGroup = null;

        foreach (var line in ReadLines(indexPath))
        {
            var groupMatch = HookGroupHeadingRegex.Match(line);
            if (groupMatch.Success)
            {
                currentGroup = new HookGroup(groupMatch.Groups["title"].Value.Trim(), []);
                groups.Add(currentGroup);
                continue;
            }

            var hookMatch = HookLinkRegex.Match(line);
            if (!hookMatch.Success || currentGroup is null)
            {
                continue;
            }

            var hookTitle = hookMatch.Groups["title"].Value.Trim();
            var relativeOverviewPath = hookMatch.Groups["path"].Value.Trim().TrimStart('/');
            var fullOverviewPath = $"{spec.RootPath}/{relativeOverviewPath}";
            var configurationRoot = fullOverviewPath[..^"/overview.md".Length];
            currentGroup.Hooks.Add(new HookEntry(hookTitle, fullOverviewPath, configurationRoot));
        }

        if (groups.Count == 0)
        {
            throw new InvalidOperationException($"Could not parse grouped hook catalog from '{indexPath}'.");
        }

        return groups;
    }

    private static IReadOnlyList<FunctionGroup> ParseFunctionGroups(string docsRoot, FunctionNavSpec spec)
    {
        var overviewPath = Path.Combine(docsRoot, "docs", spec.OverviewRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var baseDirectory = Path.GetDirectoryName(spec.OverviewRelativePath)!.Replace('\\', '/');
        var groups = new List<FunctionGroup>();
        FunctionGroup? currentGroup = null;
        var insideAvailableFunctions = false;

        foreach (var line in ReadLines(overviewPath))
        {
            if (!insideAvailableFunctions)
            {
                insideAvailableFunctions = AvailableFunctionsHeadingRegex.IsMatch(line);
                continue;
            }

            var groupMatch = OverviewGroupHeadingRegex.Match(line);
            if (groupMatch.Success)
            {
                currentGroup = new FunctionGroup(groupMatch.Groups["title"].Value.Trim(), []);
                groups.Add(currentGroup);
                continue;
            }

            var pageMatch = OverviewLinkRegex.Match(line);
            if (!pageMatch.Success || currentGroup is null)
            {
                continue;
            }

            var title = pageMatch.Groups["title"].Value.Trim();
            var relativePath = pageMatch.Groups["path"].Value.Trim().TrimStart('/');
            var combinedPath = CombineDocsPath(baseDirectory, relativePath);
            var fullPagePath = Path.Combine(docsRoot, "docs", combinedPath.Replace('/', Path.DirectorySeparatorChar));
            currentGroup.Pages.Add(new FunctionPage(title, combinedPath, ParseFunctionPage(combinedPath, fullPagePath)));
        }

        if (groups.Count == 0)
        {
            throw new InvalidOperationException($"Could not parse grouped function overview from '{overviewPath}'.");
        }

        return groups;
    }

    private static ParsedFunctionPage ParseFunctionPage(string relativePath, string fullPath)
    {
        var lines = ReadLines(fullPath).ToList();
        var pageTitle = ParsePageTitle(lines) ?? Path.GetFileNameWithoutExtension(relativePath);
        var sectionCandidates = new List<SectionCandidate>();

        for (var index = 0; index < lines.Count; index++)
        {
            var match = HeadingRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var level = match.Groups["hashes"].Value.Length;
            if (level < 2)
            {
                continue;
            }

            var title = CleanHeading(match.Groups["title"].Value);
            if (string.IsNullOrWhiteSpace(title) || title.Contains('`', StringComparison.Ordinal))
            {
                continue;
            }

            sectionCandidates.Add(new SectionCandidate(title, level, index));
        }

        var sections = BuildSectionTree(sectionCandidates, lines.Count);
        return new ParsedFunctionPage(pageTitle, relativePath, lines, sections);
    }

    private static string? ParsePageTitle(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = HeadingRegex.Match(line);
            if (!match.Success || match.Groups["hashes"].Value.Length != 1)
            {
                continue;
            }

            return CleanHeading(match.Groups["title"].Value);
        }

        return null;
    }

    private static IReadOnlyList<ParsedFunctionSection> BuildSectionTree(
        IReadOnlyList<SectionCandidate> candidates,
        int totalLineCount)
    {
        var sections = new List<ParsedFunctionSection>();
        var createdNodes = new List<ParsedFunctionSection>();
        var stack = new Stack<ParsedFunctionSection>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var nextSiblingOrParent = candidates
                .Skip(index + 1)
                .FirstOrDefault(next => next.Level <= candidate.Level);
            var endLineExclusive = nextSiblingOrParent?.LineIndex ?? totalLineCount;

            var node = new ParsedFunctionSection(
                candidate.Title,
                Slugify(candidate.Title),
                candidate.Level,
                candidate.LineIndex,
                endLineExclusive,
                []);

            while (stack.Count != 0 && stack.Peek().Level >= node.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                sections.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
            createdNodes.Add(node);
        }

        return sections;
    }

    private static void WriteFunctionSectionPages(string docsRoot, IReadOnlyList<FunctionGroup> groups, bool check)
    {
        foreach (var group in groups)
        {
            foreach (var page in group.Pages)
            {
                foreach (var section in page.ParsedPage.Sections)
                {
                    WriteFunctionSectionPage(docsRoot, page, section, check, []);
                }
            }
        }
    }

    private static void WriteFunctionSectionPage(
        string docsRoot,
        FunctionPage page,
        ParsedFunctionSection section,
        bool check,
        IReadOnlyList<ParsedFunctionSection> ancestors)
    {
        var sectionSlugs = ancestors.Select(candidate => candidate.Slug).Append(section.Slug).ToList();
        var outputRelativePath = GetSectionRelativePath(page.RelativePath, sectionSlugs);
        var outputFullPath = Path.Combine(docsRoot, "docs", outputRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var expectedContent = RenderSectionPage(page.ParsedPage, section, ancestors, outputRelativePath);
        WriteOrCheckFile(outputFullPath, outputRelativePath, expectedContent, check);

        foreach (var child in section.Children)
        {
            WriteFunctionSectionPage(docsRoot, page, child, check, ancestors.Append(section).ToList());
        }
    }

    private static string RenderSectionPage(
        ParsedFunctionPage page,
        ParsedFunctionSection section,
        IReadOnlyList<ParsedFunctionSection> ancestors,
        string outputRelativePath)
    {
        var breadcrumb = ancestors.Select(candidate => candidate.Title).Append(section.Title).ToList();
        var title = $"{page.Title}: {string.Join(" / ", breadcrumb)}";
        var pageDirectory = Path.GetDirectoryName(outputRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var relativeLinkToParent = GetRelativeLink(pageDirectory, page.RelativePath);
        var bodyLines = page.Lines
            .Skip(section.StartLineIndex + 1)
            .Take(section.EndLineExclusive - section.StartLineIndex - 1)
            .ToList();

        NormalizeNestedHeadings(bodyLines, section.Level - 1);
        TrimBlankLines(bodyLines);

        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"This page mirrors the `{string.Join(" / ", breadcrumb)}` section from [{page.Title}]({relativeLinkToParent}).");

        if (bodyLines.Count != 0)
        {
            builder.AppendLine();
            builder.AppendLine(string.Join(GeneratedDocumentLineEndings.Canonical, bodyLines));
        }

        return builder.ToString().TrimEnd();
    }

    private static void NormalizeNestedHeadings(IList<string> lines, int headingReduction)
    {
        if (headingReduction <= 0)
        {
            return;
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var match = HeadingRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var currentLevel = match.Groups["hashes"].Value.Length;
            var normalizedLevel = Math.Max(2, currentLevel - headingReduction);
            lines[index] = $"{new string('#', normalizedLevel)} {match.Groups["title"].Value}";
        }
    }

    private static void TrimBlankLines(IList<string> lines)
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

    private static void WriteOrCheckFile(string fullPath, string relativePath, string content, bool check)
    {
        var normalizedContent = GeneratedDocumentLineEndings.Normalize(content);
        if (!normalizedContent.EndsWith(GeneratedDocumentLineEndings.Canonical, StringComparison.Ordinal))
        {
            normalizedContent += GeneratedDocumentLineEndings.Canonical;
        }

        normalizedContent = MarkdownFrontmatter.ApplyExistingOrDefault(fullPath, relativePath, normalizedContent);
        normalizedContent = MarkdownVerificationMarkers.ApplyExisting(fullPath, normalizedContent);

        if (check)
        {
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Missing generated file: {fullPath}");
            }

            var currentContent = GeneratedDocumentLineEndings.Normalize(File.ReadAllText(fullPath));
            if (!string.Equals(currentContent, normalizedContent, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Generated file is out of date: {fullPath}");
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, normalizedContent);
    }

    private static string GetSectionRelativePath(string pageRelativePath, IReadOnlyList<string> sectionSlugs)
    {
        var directory = Path.GetDirectoryName(pageRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(pageRelativePath);
        var sectionDirectory = string.IsNullOrWhiteSpace(directory)
            ? $"{fileName}-sections"
            : $"{directory}/{fileName}-sections";

        return $"{sectionDirectory}/{string.Join("/", sectionSlugs)}.md";
    }

    private static string GetRelativeLink(string fromDirectory, string targetRelativePath)
    {
        var baseDirectory = string.IsNullOrWhiteSpace(fromDirectory) ? "." : fromDirectory;
        var relative = Path.GetRelativePath(
                baseDirectory.Replace('/', Path.DirectorySeparatorChar),
                targetRelativePath.Replace('/', Path.DirectorySeparatorChar))
            .Replace('\\', '/');

        return string.IsNullOrWhiteSpace(relative) ? "." : relative;
    }

    private static string ReplaceMarkedBlock(string content, string key, string replacementBlock)
    {
        var startMarker = $"# qaas-docs-generator start: {key}";
        var endMarker = $"# qaas-docs-generator end: {key}";
        var pattern =
            $"(?ms)^\\s*{Regex.Escape(startMarker)}\\s*$.*?^\\s*{Regex.Escape(endMarker)}\\s*$";

        if (!Regex.IsMatch(content, pattern))
        {
            throw new InvalidOperationException($"Could not find the navigation markers for '{key}' in mkdocs.yml.");
        }

        return Regex.Replace(content, pattern, replacementBlock);
    }

    private static IReadOnlyList<string> ReadLines(string path)
    {
        return GeneratedDocumentLineEndings.Normalize(File.ReadAllText(path))
            .Split(GeneratedDocumentLineEndings.Canonical);
    }

    private static string CombineDocsPath(string baseDirectory, string relativePath)
    {
        var parts = (baseDirectory + "/" + relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new Stack<string>();

        foreach (var part in parts)
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (normalized.Count == 0)
                {
                    throw new InvalidOperationException($"Cannot resolve docs path '{baseDirectory}/{relativePath}'.");
                }

                normalized.Pop();
                continue;
            }

            normalized.Push(part);
        }

        return string.Join("/", normalized.Reverse());
    }

    private static string CleanHeading(string title)
    {
        return title
            .Replace("{ #", "{#", StringComparison.Ordinal)
            .Split("{#", StringSplitOptions.TrimEntries)[0]
            .Trim();
    }

    private static string Slugify(string value)
    {
        return SlugCleanupRegex.Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
    }

    private static string Indent(int size) => new(' ', size);

    private sealed record HookNavSpec(
        string Key,
        string IndexRelativePath,
        string RootPath,
        string AvailableLabel,
        int Indentation);

    private sealed record FunctionNavSpec(
        string Key,
        string OverviewRelativePath,
        int Indentation);

    private sealed record HookEntry(string Title, string OverviewPath, string ConfigurationRoot);

    private sealed record HookGroup(string Title, List<HookEntry> Hooks);

    private sealed record FunctionPage(string Title, string RelativePath, ParsedFunctionPage ParsedPage);

    private sealed record FunctionGroup(string Title, List<FunctionPage> Pages);

    private sealed record ParsedFunctionPage(
        string Title,
        string RelativePath,
        IReadOnlyList<string> Lines,
        IReadOnlyList<ParsedFunctionSection> Sections);

    private sealed record ParsedFunctionSection(
        string Title,
        string Slug,
        int Level,
        int StartLineIndex,
        int EndLineExclusive,
        List<ParsedFunctionSection> Children);

    private sealed record SectionCandidate(string Title, int Level, int LineIndex);
}
