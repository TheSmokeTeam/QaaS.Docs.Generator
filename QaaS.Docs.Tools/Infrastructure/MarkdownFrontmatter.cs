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

        var finalContent = MarkdownReferenceSkeleton.Apply(
            MarkdownVerificationMarkers.ApplyExisting(fullPath, contentWithFrontmatter));
        return finalContent.EndsWith('\n') ? finalContent : finalContent + "\n";
    }

    public static string Remove(string content)
    {
        var normalized = Utf8File.NormalizeLineEndings(content);
        var body = TryExtract(normalized, out var frontmatter)
            ? normalized[frontmatter.Length..].TrimStart('\n')
            : normalized;
        return MarkdownReferenceSkeleton.Remove(MarkdownVerificationMarkers.Remove(body)).TrimStart('\n');
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

internal static class MarkdownReferenceSkeleton
{
    private const string TldrPrefix = "> TL;DR";
    private const string SeeAlsoHeading = "## See also";

    public static string Apply(string content)
    {
        var normalizedContent = Utf8File.NormalizeLineEndings(content);
        if (!TrySplitFrontmatter(normalizedContent, out var frontmatter, out var body) ||
            !IsReferenceFrontmatter(frontmatter))
        {
            return normalizedContent;
        }

        var normalizedBody = Utf8File.NormalizeLineEndings(body).TrimStart('\n');
        if (!HasH1(normalizedBody))
        {
            return normalizedContent;
        }

        if (!ContainsTldr(normalizedBody))
        {
            normalizedBody = InsertTldr(normalizedBody, ExtractSummary(frontmatter, normalizedBody));
        }

        if (!ContainsSeeAlso(normalizedBody))
        {
            normalizedBody = AppendSeeAlso(normalizedBody);
        }

        return string.Join(
            "\n",
            [
                frontmatter.TrimEnd('\n'),
                normalizedBody.TrimStart('\n')
            ]);
    }

    public static string Remove(string content)
    {
        var lines = Utf8File.NormalizeLineEndings(content)
            .Split('\n')
            .ToList();
        lines = lines
            .Where(line => !line.StartsWith(TldrPrefix, StringComparison.Ordinal))
            .ToList();

        var seeAlsoIndex = lines.FindIndex(line => IsSeeAlsoHeading(line));
        if (seeAlsoIndex >= 0)
        {
            lines.RemoveRange(seeAlsoIndex, lines.Count - seeAlsoIndex);
        }

        return string.Join("\n", lines).TrimEnd('\n');
    }

    private static bool TrySplitFrontmatter(string content, out string frontmatter, out string body)
    {
        frontmatter = string.Empty;
        body = content;
        var normalized = Utf8File.NormalizeLineEndings(content);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var frontmatterEnd = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (frontmatterEnd < 0)
        {
            return false;
        }

        var splitIndex = frontmatterEnd + "\n---".Length;
        frontmatter = normalized[..splitIndex];
        body = normalized[splitIndex..];
        return true;
    }

    private static bool IsReferenceFrontmatter(string frontmatter)
    {
        return frontmatter
            .Split('\n')
            .Any(line =>
            {
                var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !string.Equals(parts[0], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var value = parts[1].Trim(' ', '"', '\'');
                return string.Equals(value, "reference", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool HasH1(string body)
    {
        return body
            .Split('\n')
            .Any(line => line.StartsWith("# ", StringComparison.Ordinal));
    }

    private static bool ContainsTldr(string body)
    {
        return body
            .Split('\n')
            .Any(line => line.StartsWith(TldrPrefix, StringComparison.Ordinal));
    }

    private static bool ContainsSeeAlso(string body)
    {
        return body
            .Split('\n')
            .Any(IsSeeAlsoHeading);
    }

    private static bool IsSeeAlsoHeading(string line)
    {
        var trimmed = line.Trim();
        return string.Equals(trimmed, SeeAlsoHeading, StringComparison.Ordinal) ||
               trimmed.StartsWith(SeeAlsoHeading + " ", StringComparison.Ordinal);
    }

    private static string InsertTldr(string body, string summary)
    {
        var lines = Utf8File.NormalizeLineEndings(body)
            .Split('\n')
            .ToList();
        var titleIndex = lines.FindIndex(line => line.StartsWith("# ", StringComparison.Ordinal));
        if (titleIndex < 0)
        {
            return body;
        }

        var before = lines.Take(titleIndex + 1).ToList();
        var after = lines
            .Skip(titleIndex + 1)
            .SkipWhile(string.IsNullOrWhiteSpace)
            .ToList();

        return string.Join(
            "\n",
            before
                .Append(string.Empty)
                .Append($"> TL;DR: {summary}")
                .Append(string.Empty)
                .Concat(after));
    }

    private static string ExtractSummary(string frontmatter, string body)
    {
        foreach (var line in frontmatter.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("summary:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return NormalizeSummary(trimmed["summary:".Length..]);
        }

        var title = body
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..]
            .Trim();
        return string.IsNullOrWhiteSpace(title)
            ? "Generated reference page."
            : $"Generated reference page for {title}.";
    }

    private static string NormalizeSummary(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1];
        }

        trimmed = trimmed
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Generated reference page." : trimmed;
    }

    private static string AppendSeeAlso(string body)
    {
        return string.Join(
            "\n",
            [
                Utf8File.NormalizeLineEndings(body).TrimEnd('\n'),
                string.Empty,
                SeeAlsoHeading,
                string.Empty,
                "Use the surrounding documentation navigation to move between related generated reference pages."
            ]);
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
