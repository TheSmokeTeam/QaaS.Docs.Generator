namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Keeps generated markdown aligned with the qaas-docs v2 frontmatter contract.
/// </summary>
internal static class MarkdownFrontmatter
{
    public static string ApplyExistingOrDefault(string fullPath, string relativePath, string generatedContent)
    {
        var normalizedContent = Utf8File.NormalizeLineEndings(generatedContent);
        string contentWithFrontmatter;
        if (TryExtract(normalizedContent, out _))
        {
            contentWithFrontmatter = normalizedContent;
        }
        else if (File.Exists(fullPath))
        {
            var existingContent = Utf8File.NormalizeLineEndings(File.ReadAllText(fullPath));
            if (TryExtract(existingContent, out var existingFrontmatter))
            {
                contentWithFrontmatter = Combine(existingFrontmatter, normalizedContent);
            }
            else
            {
                contentWithFrontmatter = Combine(CreateDefault(relativePath, normalizedContent), normalizedContent);
            }
        }
        else
        {
            contentWithFrontmatter = Combine(CreateDefault(relativePath, normalizedContent), normalizedContent);
        }

        return MarkdownVerificationMarkers.ApplyExisting(fullPath, contentWithFrontmatter);
    }

    public static string Remove(string content)
    {
        var normalized = Utf8File.NormalizeLineEndings(content);
        var body = TryExtract(normalized, out var frontmatter)
            ? normalized[frontmatter.Length..].TrimStart('\n')
            : normalized;
        return MarkdownVerificationMarkers.Remove(body).TrimStart('\n');
    }

    private static bool TryExtract(string content, out string frontmatter)
    {
        frontmatter = string.Empty;
        var normalized = Utf8File.NormalizeLineEndings(content);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var endIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return false;
        }

        frontmatter = normalized[..(endIndex + "\n---\n".Length)].TrimEnd('\n');
        return true;
    }

    private static string Combine(string frontmatter, string body)
    {
        var normalizedFrontmatter = Utf8File.NormalizeLineEndings(frontmatter).TrimEnd('\n');
        var normalizedBody = Utf8File.NormalizeLineEndings(body).TrimStart('\n');
        return $"{normalizedFrontmatter}\n\n{normalizedBody}";
    }

    private static string CreateDefault(string relativePath, string generatedContent)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        var title = ExtractTitle(generatedContent) ?? Path.GetFileNameWithoutExtension(normalizedPath);
        var appliesTo = normalizedPath.Split('/', 2)[0] switch
        {
            "assertions" => "assertions",
            "generators" => "generators",
            "probes" => "probes",
            "processors" => "processors",
            "mocker" => "mocker",
            "framework" => "framework",
            "qaas" => "runner",
            _ => "qaas"
        };
        var id = normalizedPath
            .Replace(".md", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('/', '.')
            .Replace('-', '.');

        return string.Join(
            "\n",
            [
                "---",
                $"id: {id}",
                "type: reference",
                "status: stable",
                "since: 2.0.0",
                "last_verified: 2026-05-27",
                $"applies_to: [{appliesTo}]",
                $"keywords: [{appliesTo}, reference]",
                $"summary: \"Reference page for {EscapeYaml(title)}.\"",
                "---"
            ]);
    }

    private static string? ExtractTitle(string content)
    {
        foreach (var line in Utf8File.NormalizeLineEndings(content).Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return null;
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

internal static class MarkdownVerificationMarkers
{
    private const string MarkerPrefix = "<!-- Verified-against:";

    public static string ApplyExisting(string fullPath, string generatedContent)
    {
        var normalizedContent = Utf8File.NormalizeLineEndings(generatedContent);
        var markers = new List<string>();

        if (File.Exists(fullPath))
        {
            var existingContent = Utf8File.NormalizeLineEndings(File.ReadAllText(fullPath));
            markers.AddRange(ExtractMarkers(existingContent));
        }

        markers.AddRange(ExtractMarkers(normalizedContent));
        markers = markers
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var body = Remove(normalizedContent);
        return markers.Count == 0 ? body : InsertAfterFrontmatter(body, markers);
    }

    public static string Remove(string content)
    {
        var lines = Utf8File.NormalizeLineEndings(content).Split('\n');
        return string.Join("\n", lines.Where(line => !IsMarkerLine(line)));
    }

    private static IEnumerable<string> ExtractMarkers(string content)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in Utf8File.NormalizeLineEndings(content).Split('\n'))
        {
            var trimmed = line.Trim();
            if (!IsMarkerLine(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static bool IsMarkerLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith(MarkerPrefix, StringComparison.Ordinal) &&
               trimmed.EndsWith("-->", StringComparison.Ordinal);
    }

    private static string InsertAfterFrontmatter(string content, IReadOnlyList<string> markers)
    {
        var markerBlock = string.Join("\n", markers);
        var normalized = Utf8File.NormalizeLineEndings(content);
        if (normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            var frontmatterEnd = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (frontmatterEnd >= 0)
            {
                var splitIndex = frontmatterEnd + "\n---".Length;
                var frontmatter = normalized[..splitIndex].TrimEnd('\n');
                var body = normalized[splitIndex..].TrimStart('\n');
                return string.Join(
                    "\n",
                    [
                        frontmatter,
                        markerBlock,
                        string.Empty,
                        body
                    ]);
            }
        }

        return string.Join(
            "\n",
            [
                markerBlock,
                string.Empty,
                normalized.TrimStart('\n')
            ]);
    }
}
