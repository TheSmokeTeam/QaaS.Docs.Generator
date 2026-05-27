namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Keeps generated markdown aligned with the qaas-docs v2 frontmatter contract.
/// </summary>
internal static class MarkdownFrontmatter
{
    public static string ApplyExistingOrDefault(string fullPath, string relativePath, string generatedContent)
    {
        var normalizedContent = Utf8File.NormalizeLineEndings(generatedContent);
        if (TryExtract(normalizedContent, out _))
        {
            return normalizedContent;
        }

        if (File.Exists(fullPath))
        {
            var existingContent = Utf8File.NormalizeLineEndings(File.ReadAllText(fullPath));
            if (TryExtract(existingContent, out var existingFrontmatter))
            {
                return Combine(existingFrontmatter, normalizedContent);
            }
        }

        return Combine(CreateDefault(relativePath, normalizedContent), normalizedContent);
    }

    public static string Remove(string content)
    {
        var normalized = Utf8File.NormalizeLineEndings(content);
        return TryExtract(normalized, out var frontmatter)
            ? normalized[frontmatter.Length..].TrimStart('\n')
            : normalized;
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
