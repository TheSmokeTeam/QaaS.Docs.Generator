using System.Text.RegularExpressions;

namespace QaaS.Docs.Generator;

internal static class MarkdownTableCellFormatter
{
    private static readonly Regex BareHttpUrl = new(
        @"\bhttps?://[^\s<|)]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public static string Format(string value)
    {
        var normalized = value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);

        return BareHttpUrl.Replace(
            normalized,
            match =>
                IsAlreadyMarkdownDelimited(normalized, match) ? match.Value : $"`{match.Value}`"
        );
    }

    private static bool IsAlreadyMarkdownDelimited(string value, Match match)
    {
        return IsInsideInlineCode(value, match.Index)
            || IsInsideAutolink(value, match.Index)
            || IsMarkdownLinkDestination(value, match.Index);
    }

    private static bool IsInsideInlineCode(string value, int index)
    {
        var backtickCount = 0;
        for (var i = 0; i < index; i++)
        {
            if (value[i] == '`')
            {
                backtickCount++;
            }
        }

        return backtickCount % 2 == 1;
    }

    private static bool IsInsideAutolink(string value, int index)
    {
        return index > 0 && value[index - 1] == '<';
    }

    private static bool IsMarkdownLinkDestination(string value, int index)
    {
        return index > 1 && value[index - 1] == '(' && value[index - 2] == ']';
    }
}
