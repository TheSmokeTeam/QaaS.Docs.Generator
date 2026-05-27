using NUnit.Framework;

namespace QaaS.Docs.Generator.Tests.Generation;

[TestFixture]
public sealed class GeneratedDocumentWriterTests
{
    [Test]
    public void Write_WhenExistingGeneratedPageHasFrontmatter_PreservesIt()
    {
        var docsRoot = CreateTempDocsRoot();
        var pagePath = Path.Combine(docsRoot, "docs", "qaas", "functions", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(
            pagePath,
            """
            ---
            id: qaas.functions.index
            type: reference
            status: stable
            since: 2.0.0
            last_verified: 2026-05-22
            applies_to: [runner]
            keywords: [runner, functions]
            summary: "Existing summary."
            ---

            # Old
            """);

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([new GeneratedDocument("qaas/functions/index.md", "# New\n\nGenerated body.")]);

        var content = File.ReadAllText(pagePath);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith("---"));
            Assert.That(content, Does.Contain("id: qaas.functions.index"));
            Assert.That(content, Does.Contain("# New"));
            Assert.That(content, Does.Not.Contain("# Old"));
        });
    }

    [Test]
    public void Write_WhenGeneratedPageIsNew_AddsDefaultFrontmatter()
    {
        var docsRoot = CreateTempDocsRoot();

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([new GeneratedDocument("framework/functions/new-page.md", "# New Page\n\nGenerated body.")]);

        var content = File.ReadAllText(Path.Combine(docsRoot, "docs", "framework", "functions", "new-page.md"));

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith("---"));
            Assert.That(content, Does.Contain("id: framework.functions.new.page"));
            Assert.That(content, Does.Contain("applies_to: [framework]"));
            Assert.That(content, Does.Contain("summary: \"Reference page for New Page.\""));
        });
    }

    private static string CreateTempDocsRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qaas-docs-generator-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
