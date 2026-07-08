using System.Text.RegularExpressions;

namespace QaaS.Docs.Generator.Functions;

internal static class FrameworkSerializerSectionPreserver
{
    private const string ExtensionMethodsPath = "framework/functions/extension-methods.md";

    private static readonly PreservedSection[] Sections =
    [
        new("Deserializer", "Running communication data"),
        new("Serialization type", "Serilog"),
        new("Serializer", "Serilog"),
    ];

    public static IReadOnlyList<GeneratedDocument> Apply(
        string docsRoot,
        IReadOnlyList<GeneratedDocument> documents
    )
    {
        var existingPath = Path.Combine(
            docsRoot,
            "docs",
            ExtensionMethodsPath.Replace('/', Path.DirectorySeparatorChar)
        );
        if (!File.Exists(existingPath))
        {
            return documents;
        }

        var existingContent = GeneratedDocumentLineEndings.Normalize(
            File.ReadAllText(existingPath)
        );
        var output = new List<GeneratedDocument>(documents.Count);
        foreach (var document in documents)
        {
            if (
                !string.Equals(
                    document.RelativePath,
                    ExtensionMethodsPath,
                    StringComparison.Ordinal
                )
            )
            {
                output.Add(document);
                continue;
            }

            output.Add(
                document with
                {
                    Content = PreserveSections(document.Content, existingContent),
                }
            );
        }

        return output;
    }

    private static string PreserveSections(string generatedContent, string existingContent)
    {
        var content = GeneratedDocumentLineEndings.Normalize(generatedContent);
        foreach (var section in Sections)
        {
            if (ContainsSection(content, section.Title))
            {
                continue;
            }

            var existingSection = ExtractSection(existingContent, section.Title);
            if (string.IsNullOrWhiteSpace(existingSection))
            {
                continue;
            }

            content = InsertSection(content, existingSection, section.InsertBeforeTitle);
        }

        return content.TrimEnd('\r', '\n');
    }

    private static bool ContainsSection(string content, string title)
    {
        return SectionHeadingRegex(title).IsMatch(content);
    }

    private static string? ExtractSection(string content, string title)
    {
        var match = SectionBlockRegex(title).Match(content);
        return match.Success ? match.Value.Trim('\r', '\n') : null;
    }

    private static string InsertSection(
        string content,
        string sectionContent,
        string insertBeforeTitle
    )
    {
        var insertionPoint = SectionHeadingRegex(insertBeforeTitle).Match(content);
        var normalizedSection = sectionContent.Trim('\r', '\n');
        if (insertionPoint.Success)
        {
            return content.Insert(
                insertionPoint.Index,
                normalizedSection
                    + GeneratedDocumentLineEndings.Canonical
                    + GeneratedDocumentLineEndings.Canonical
            );
        }

        var extensionMethodsEnd = Regex.Match(
            content,
            @"(?ms)^##\s+Extension Methods(?:\s+\{:[^}]+\})?\s*$.*?(?=^##\s+|\z)"
        );
        if (extensionMethodsEnd.Success)
        {
            return content.Insert(
                extensionMethodsEnd.Index + extensionMethodsEnd.Length,
                GeneratedDocumentLineEndings.Canonical
                    + GeneratedDocumentLineEndings.Canonical
                    + normalizedSection
            );
        }

        return content.TrimEnd('\r', '\n')
            + GeneratedDocumentLineEndings.Canonical
            + GeneratedDocumentLineEndings.Canonical
            + normalizedSection
            + GeneratedDocumentLineEndings.Canonical;
    }

    private static Regex SectionHeadingRegex(string title)
    {
        return new(
            $@"(?m)^###\s+{Regex.Escape(title)}(?:\s+\{{:[^}}]+\}})?\s*$",
            RegexOptions.CultureInvariant
        );
    }

    private static Regex SectionBlockRegex(string title)
    {
        return new(
            $@"(?ms)^###\s+{Regex.Escape(title)}(?:\s+\{{:[^}}]+\}})?\s*$.*?(?=^###\s+|^##\s+|\z)",
            RegexOptions.CultureInvariant
        );
    }

    private sealed record PreservedSection(string Title, string InsertBeforeTitle);
}
