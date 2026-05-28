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
            """
        );

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
    public void Write_WhenExistingGeneratedPageHasVerificationMarkers_PreservesThem()
    {
        var docsRoot = CreateTempDocsRoot();
        var pagePath = Path.Combine(
            docsRoot,
            "docs",
            "assertions",
            "availableAssertions",
            "DelayByAverage",
            "overview.md"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(
            pagePath,
            """
            ---
            id: assertions.available.delaybyaverage.overview
            type: reference
            status: stable
            since: 2.0.0
            last_verified: 2026-05-22
            applies_to: [assertions]
            keywords: [assertions, DelayByAverage]
            summary: "Existing summary."
            ---
            <!-- Verified-against: QaaS.Common.Assertions\QaaS.Common.Assertions\Delay\DelayByAverage.cs -->

            # Old
            """
        );

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([
                new GeneratedDocument(
                    "assertions/availableAssertions/DelayByAverage/overview.md",
                    "# DelayByAverage\n\nGenerated body."
                ),
            ]);

        var content = File.ReadAllText(pagePath);

        Assert.Multiple(() =>
        {
            Assert.That(
                content,
                Does.Contain(
                    "<!-- Verified-against: QaaS.Common.Assertions\\QaaS.Common.Assertions\\Delay\\DelayByAverage.cs -->"
                )
            );
            Assert.That(content, Does.Contain("# DelayByAverage"));
            Assert.That(content, Does.Not.Contain("# Old"));
        });
    }

    [Test]
    public void Write_WhenGeneratedBodyContainsExistingVerificationMarker_MovesSingleMarkerAfterFrontmatter()
    {
        var docsRoot = CreateTempDocsRoot();
        var pagePath = Path.Combine(
            docsRoot,
            "docs",
            "assertions",
            "availableAssertions",
            "DelayByAverage",
            "overview.md"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(
            pagePath,
            """
            ---
            id: assertions.available.delaybyaverage.overview
            type: reference
            ---
            <!-- Verified-against: QaaS.Common.Assertions\Delay\DelayByAverage.cs -->

            # Old
            """
        );

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([
                new GeneratedDocument(
                    "assertions/availableAssertions/DelayByAverage/overview.md",
                    """
                    # DelayByAverage

                    <!-- Verified-against: QaaS.Common.Assertions\Delay\DelayByAverage.cs -->

                    ## When to use

                    Generated body.
                    """
                ),
            ]);

        var content = File.ReadAllText(pagePath).Replace("\r\n", "\n", StringComparison.Ordinal);
        const string marker =
            "<!-- Verified-against: QaaS.Common.Assertions\\Delay\\DelayByAverage.cs -->";

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(content, marker), Is.EqualTo(1));
            Assert.That(content, Does.Contain($"---\n{marker}\n\n# DelayByAverage"));
            Assert.That(content, Does.Contain("## When to use"));
            Assert.That(content, Does.Not.Contain("# Old"));
        });
    }

    [Test]
    public void Write_WhenGeneratedPageIsNew_AddsDefaultFrontmatter()
    {
        var docsRoot = CreateTempDocsRoot();

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([
                new GeneratedDocument(
                    "framework/functions/new-page.md",
                    "# New Page\n\nGenerated body."
                ),
            ]);

        var content = File.ReadAllText(
            Path.Combine(docsRoot, "docs", "framework", "functions", "new-page.md")
        );

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith("---"));
            Assert.That(content, Does.Contain("id: framework.functions.new.page"));
            Assert.That(content, Does.Contain("applies_to: [framework]"));
            Assert.That(content, Does.Contain("summary: \"Reference page for New Page.\""));
        });
    }

    [Test]
    public void Write_WhenReferencePageIsMissingSkeleton_AddsTldrAndSeeAlso()
    {
        var docsRoot = CreateTempDocsRoot();

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([
                new GeneratedDocument(
                    "framework/functions/new-page.md",
                    "# New Page\n\n## Details\n\nGenerated body."
                ),
            ]);

        var content = File.ReadAllText(
                Path.Combine(docsRoot, "docs", "framework", "functions", "new-page.md")
            )
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(
                content,
                Does.Contain(
                    "# New Page\n\n> TL;DR — Reference page for New Page.\n\n## Details {: #details}"
                )
            );
            Assert.That(
                content,
                Does.EndWith(
                    "## See also {: #see-also}\n\nUse the surrounding documentation navigation to move between related generated reference pages.\n"
                )
            );
        });
    }

    [Test]
    public void Write_WhenCheckedHeadingsAreMissingAnchors_AddsStableExplicitAnchors()
    {
        var docsRoot = CreateTempDocsRoot();

        GeneratedDocumentWriter
            .Create(docsRoot)
            .Write([
                new GeneratedDocument(
                    "framework/functions/anchored-page.md",
                    """
                    # Anchored Page

                    ## Details

                    ### Details

                    ### `Run command`

                    ## Existing {: #custom-anchor}

                    ```markdown
                    ## Fenced heading
                    ```
                    """
                ),
            ]);

        var content = File.ReadAllText(
                Path.Combine(docsRoot, "docs", "framework", "functions", "anchored-page.md")
            )
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("## Details {: #details}"));
            Assert.That(content, Does.Contain("### Details {: #details-2}"));
            Assert.That(content, Does.Contain("### `Run command` {: #run-command}"));
            Assert.That(content, Does.Contain("## Existing {: #custom-anchor}"));
            Assert.That(content, Does.Contain("## Fenced heading\n"));
            Assert.That(content, Does.Not.Contain("## Fenced heading {: #fenced-heading}"));
        });
    }

    private static string CreateTempDocsRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-generator-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path);
        return path;
    }

    private static int CountOccurrences(string content, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
