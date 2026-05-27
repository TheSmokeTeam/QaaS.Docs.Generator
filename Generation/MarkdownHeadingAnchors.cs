using System.Text;

namespace QaaS.Docs.Generator;

internal static class MarkdownHeadingAnchors
{
    public static string Apply(string content)
    {
        var lines = GeneratedDocumentLineEndings
            .Normalize(content)
            .Split(GeneratedDocumentLineEndings.Canonical);
        var seenAnchors = new Dictionary<string, int>(StringComparer.Ordinal);
        var inFence = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmedStart = line.TrimStart();
            if (
                trimmedStart.StartsWith("```", StringComparison.Ordinal)
                || trimmedStart.StartsWith("~~~", StringComparison.Ordinal)
            )
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || !TryParseCheckedHeading(line, out var hashes, out var headingText))
            {
                continue;
            }

            if (TryGetExplicitAnchor(headingText, out var existingAnchor))
            {
                RegisterAnchor(seenAnchors, existingAnchor);
                continue;
            }

            var anchor = CreateUniqueAnchor(Slugify(headingText), seenAnchors);
            lines[index] = $"{hashes} {headingText.TrimEnd()} {{: #{anchor}}}";
        }

        return string.Join(GeneratedDocumentLineEndings.Canonical, lines);
    }

    private static bool TryParseCheckedHeading(
        string line,
        out string hashes,
        out string headingText
    )
    {
        hashes = string.Empty;
        headingText = string.Empty;

        if (
            !line.StartsWith("## ", StringComparison.Ordinal)
            && !line.StartsWith("### ", StringComparison.Ordinal)
        )
        {
            return false;
        }

        if (line.StartsWith("####", StringComparison.Ordinal))
        {
            return false;
        }

        var hashCount = line[2] == '#' ? 3 : 2;
        hashes = line[..hashCount];
        headingText = line[(hashCount + 1)..];
        return !string.IsNullOrWhiteSpace(headingText);
    }

    private static bool TryGetExplicitAnchor(string headingText, out string anchor)
    {
        anchor = string.Empty;
        var trimmed = headingText.TrimEnd();
        if (!trimmed.EndsWith('}'))
        {
            return false;
        }

        var openIndex = trimmed.LastIndexOf('{');
        if (openIndex < 0)
        {
            return false;
        }

        var attribute = trimmed[openIndex..];
        var hashIndex = attribute.IndexOf('#');
        if (hashIndex < 0)
        {
            return false;
        }

        var closeIndex = attribute.IndexOf('}', hashIndex + 1);
        if (closeIndex < 0)
        {
            return false;
        }

        anchor = attribute[(hashIndex + 1)..closeIndex].Trim();
        return anchor.Length != 0 && anchor.All(IsAnchorCharacter);
    }

    private static void RegisterAnchor(IDictionary<string, int> seenAnchors, string anchor)
    {
        if (!seenAnchors.TryAdd(anchor, 1))
        {
            seenAnchors[anchor]++;
        }
    }

    private static string CreateUniqueAnchor(
        string preferredAnchor,
        IDictionary<string, int> seenAnchors
    )
    {
        var baseAnchor = string.IsNullOrWhiteSpace(preferredAnchor) ? "section" : preferredAnchor;
        if (seenAnchors.TryAdd(baseAnchor, 1))
        {
            return baseAnchor;
        }

        var next = seenAnchors[baseAnchor] + 1;
        seenAnchors[baseAnchor] = next;
        return $"{baseAnchor}-{next}";
    }

    private static string Slugify(string headingText)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in headingText)
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsAnchorCharacter(char character)
    {
        return IsAsciiLetterOrDigit(character) || character is '-' or '_';
    }
}
