using System.Text;

namespace QaaS.Docs.Tools.Infrastructure;

/// <summary>
/// Normalizes text IO so generated markdown stays byte-stable across Windows and CI environments.
/// </summary>
internal static class Utf8File
{
    /// <summary>
    /// Shared UTF-8 encoding instance without a BOM so generated markdown stays stable in git.
    /// </summary>
    public static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Reads a UTF-8 text file while tolerating another process holding the file open for reading.
    /// </summary>
    public static async Task<string> ReadAllTextAsync(string path)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Writes a UTF-8 text file without a BOM, creating the parent directory when needed.
    /// </summary>
    public static async Task WriteAllTextAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, Utf8NoBom);
    }

    /// <summary>
    /// Converts Windows and old-Mac line endings to LF so generated content can be compared byte-for-byte.
    /// </summary>
    public static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }
}
