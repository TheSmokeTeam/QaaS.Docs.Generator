using System.Security.Cryptography;
using System.Text;

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

internal static class GeneratedDocumentHasher
{
    public static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(GeneratedDocumentLineEndings.Normalize(content)));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
    }

    public static string WithHeader(string body, params IEnumerable<string> sources)
    {
        var normalizedBody = GeneratedDocumentLineEndings.Normalize(body);
        var hash = Hash(normalizedBody);
        return $"<!-- generated hash:{hash} sources:{string.Join(", ", sources)} -->{GeneratedDocumentLineEndings.Canonical}{GeneratedDocumentLineEndings.Canonical}{normalizedBody}";
    }
}
