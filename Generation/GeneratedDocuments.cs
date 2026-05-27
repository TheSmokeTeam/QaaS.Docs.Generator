namespace QaaS.Docs.Generator;

internal sealed record GeneratedDocument(string RelativePath, string Content);

internal static class GeneratedDocumentLineEndings
{
    public const string Canonical = "\r\n";

    public static string Normalize(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Canonical, StringComparison.Ordinal);
    }
}

internal sealed class GeneratedDocumentWriter
{
    private readonly string _docsRoot;
    private readonly bool _dryRun;

    private GeneratedDocumentWriter(string docsRoot, bool dryRun)
    {
        _docsRoot = docsRoot;
        _dryRun = dryRun;
    }

    public static GeneratedDocumentWriter Create(string docsRoot) => new(docsRoot, dryRun: false);

    public static GeneratedDocumentWriter CreateDryRun(string docsRoot) => new(docsRoot, dryRun: true);

    public List<string> Write(IEnumerable<GeneratedDocument> documents)
    {
        var failures = new List<string>();

        foreach (var document in documents)
        {
            var normalizedContent = GeneratedDocumentLineEndings.Normalize(document.Content);
            if (!normalizedContent.EndsWith(GeneratedDocumentLineEndings.Canonical, StringComparison.Ordinal))
            {
                normalizedContent += GeneratedDocumentLineEndings.Canonical;
            }

            var fullPath = Path.Combine(_docsRoot, "docs", document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            normalizedContent = MarkdownFrontmatter.ApplyExistingOrDefault(
                fullPath,
                document.RelativePath,
                normalizedContent);
            normalizedContent = MarkdownVerificationMarkers.ApplyExisting(fullPath, normalizedContent);

            if (_dryRun)
            {
                if (!File.Exists(fullPath))
                {
                    failures.Add($"Missing generated file: {fullPath}");
                    continue;
                }

                var current = GeneratedDocumentLineEndings.Normalize(File.ReadAllText(fullPath));
                if (!string.Equals(current, normalizedContent, StringComparison.Ordinal))
                {
                    failures.Add($"Generated file is out of date: {fullPath}");
                }

                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, normalizedContent);
        }

        return failures;
    }
}

internal static class MarkdownVerificationMarkers
{
    private const string MarkerPrefix = "<!-- Verified-against:";

    public static string ApplyExisting(string fullPath, string generatedContent)
    {
        var normalizedContent = GeneratedDocumentLineEndings.Normalize(generatedContent);
        var markers = new List<string>();

        if (File.Exists(fullPath))
        {
            var existingContent = GeneratedDocumentLineEndings.Normalize(File.ReadAllText(fullPath));
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
        var lines = GeneratedDocumentLineEndings.Normalize(content)
            .Split(GeneratedDocumentLineEndings.Canonical);
        return string.Join(
            GeneratedDocumentLineEndings.Canonical,
            lines.Where(line => !IsMarkerLine(line)));
    }

    private static IEnumerable<string> ExtractMarkers(string content)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in GeneratedDocumentLineEndings.Normalize(content).Split(GeneratedDocumentLineEndings.Canonical))
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
        var markerBlock = string.Join(GeneratedDocumentLineEndings.Canonical, markers);
        var normalized = GeneratedDocumentLineEndings.Normalize(content);
        if (normalized.StartsWith("---" + GeneratedDocumentLineEndings.Canonical, StringComparison.Ordinal))
        {
            var frontmatterEnd = normalized.IndexOf(
                GeneratedDocumentLineEndings.Canonical + "---" + GeneratedDocumentLineEndings.Canonical,
                GeneratedDocumentLineEndings.Canonical.Length,
                StringComparison.Ordinal);
            if (frontmatterEnd >= 0)
            {
                var splitIndex = frontmatterEnd + GeneratedDocumentLineEndings.Canonical.Length + "---".Length;
                var frontmatter = normalized[..splitIndex].TrimEnd('\r', '\n');
                var body = normalized[splitIndex..].TrimStart('\r', '\n');
                return string.Join(
                    GeneratedDocumentLineEndings.Canonical,
                    [
                        frontmatter,
                        markerBlock,
                        string.Empty,
                        body
                    ]);
            }
        }

        return string.Join(
            GeneratedDocumentLineEndings.Canonical,
            [
                markerBlock,
                string.Empty,
                normalized.TrimStart('\r', '\n')
            ]);
    }
}

internal static class MarkdownFrontmatter
{
    public static string ApplyExistingOrDefault(string fullPath, string relativePath, string generatedContent)
    {
        var normalizedContent = GeneratedDocumentLineEndings.Normalize(generatedContent);
        if (TryExtract(normalizedContent, out _))
        {
            return normalizedContent;
        }

        if (File.Exists(fullPath))
        {
            var existingContent = GeneratedDocumentLineEndings.Normalize(File.ReadAllText(fullPath));
            if (TryExtract(existingContent, out var existingFrontmatter))
            {
                return Combine(existingFrontmatter, normalizedContent);
            }
        }

        return Combine(CreateDefault(relativePath, normalizedContent), normalizedContent);
    }

    private static bool TryExtract(string content, out string frontmatter)
    {
        frontmatter = string.Empty;
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var endIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return false;
        }

        frontmatter = GeneratedDocumentLineEndings.Normalize(normalized[..(endIndex + "\n---\n".Length)].TrimEnd('\n'));
        return true;
    }

    private static string Combine(string frontmatter, string body)
    {
        var normalizedFrontmatter = GeneratedDocumentLineEndings.Normalize(frontmatter).TrimEnd('\r', '\n');
        var normalizedBody = GeneratedDocumentLineEndings.Normalize(body).TrimStart('\r', '\n');
        return $"{normalizedFrontmatter}{GeneratedDocumentLineEndings.Canonical}{GeneratedDocumentLineEndings.Canonical}{normalizedBody}";
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
            GeneratedDocumentLineEndings.Canonical,
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
        foreach (var line in GeneratedDocumentLineEndings.Normalize(content).Split(GeneratedDocumentLineEndings.Canonical))
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

internal static class GeneratedDocumentHasher
{
    public static string WithHeader(string body, params IEnumerable<string> sources)
    {
        return GeneratedDocumentLineEndings.Normalize(body);
    }
}
