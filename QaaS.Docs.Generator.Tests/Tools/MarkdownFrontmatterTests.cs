using NUnit.Framework;
using ToolsMarkdownFrontmatter = QaaS.Docs.Tools.Infrastructure.MarkdownFrontmatter;

namespace QaaS.Docs.Generator.Tests.Tools;

[TestFixture]
public sealed class MarkdownFrontmatterTests
{
    [Test]
    public void ApplyExistingOrDefault_WhenExistingPageHasFrontmatter_PreservesIt()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRoot);
        var pagePath = Path.Combine(tempRoot, "overview.md");
        File.WriteAllText(
            pagePath,
            """
            ---
            id: probes.available.empty-s3-bucket.overview
            type: reference
            status: stable
            since: 2.0.0
            last_verified: 2026-05-22
            applies_to: [probes]
            keywords: [probes, EmptyS3Bucket]
            summary: "Existing summary."
            ---

            # Old
            """
        );

        var content = ToolsMarkdownFrontmatter.ApplyExistingOrDefault(
            pagePath,
            "probes/availableProbes/EmptyS3Bucket/overview.md",
            "# EmptyS3Bucket\n\nGenerated body."
        );

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith("---"));
            Assert.That(content, Does.Contain("id: probes.available.empty-s3-bucket.overview"));
            Assert.That(content, Does.Contain("# EmptyS3Bucket"));
            Assert.That(content, Does.Not.Contain("# Old"));
        });
    }

    [Test]
    public void ApplyExistingOrDefault_WhenExistingPageHasVerificationMarker_PreservesItAfterFrontmatter()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRoot);
        var pagePath = Path.Combine(tempRoot, "overview.md");
        File.WriteAllText(
            pagePath,
            """
            ---
            id: probes.available.empty-s3-bucket.overview
            type: reference
            ---
            <!-- Verified-against: QaaS.Common.Probes\S3\EmptyS3Bucket.cs -->

            # Old
            """
        );

        var content = ToolsMarkdownFrontmatter
            .ApplyExistingOrDefault(
                pagePath,
                "probes/availableProbes/EmptyS3Bucket/overview.md",
                """
                # EmptyS3Bucket

                <!-- Verified-against: QaaS.Common.Probes\S3\EmptyS3Bucket.cs -->

                Generated body.
                """
            )
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        const string marker = "<!-- Verified-against: QaaS.Common.Probes\\S3\\EmptyS3Bucket.cs -->";

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(content, marker), Is.EqualTo(1));
            Assert.That(content, Does.Contain($"---\n{marker}\n\n# EmptyS3Bucket"));
            Assert.That(content, Does.Not.Contain("# Old"));
        });
    }

    [Test]
    public void Remove_WhenPageHasFrontmatter_ReturnsBodyOnly()
    {
        var body = ToolsMarkdownFrontmatter.Remove(
            """
            ---
            id: sample
            type: reference
            ---

            # Title
            """
        );

        Assert.That(body, Does.StartWith("# Title"));
    }

    [Test]
    public void Remove_WhenPageHasVerificationMarker_RemovesMarkerWithFrontmatter()
    {
        var body = ToolsMarkdownFrontmatter.Remove(
            """
            ---
            id: sample
            type: reference
            ---
            <!-- Verified-against: Some\File.cs -->

            # Title
            """
        );

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.StartWith("# Title"));
            Assert.That(body, Does.Not.Contain("Verified-against"));
        });
    }

    [Test]
    public void ApplyExistingOrDefault_WhenReferencePageIsMissingSkeleton_AddsTldrAndSeeAlso()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRoot);
        var pagePath = Path.Combine(tempRoot, "overview.md");

        var content = ToolsMarkdownFrontmatter
            .ApplyExistingOrDefault(
                pagePath,
                "assertions/availableAssertions/DelayByAverage/overview.md",
                "# DelayByAverage\n\n## What It Does\n\nGenerated body."
            )
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(
                content,
                Does.Contain(
                    "# DelayByAverage\n\n> TL;DR: Reference page for DelayByAverage.\n\n## What It Does {: #what-it-does}"
                )
            );
            Assert.That(
                content,
                Does.Contain(
                    "\n## See also {: #see-also}\n\nUse the surrounding documentation navigation to move between related generated reference pages."
                )
            );
        });
    }

    [Test]
    public void ApplyExistingOrDefault_WhenCheckedHeadingsAreMissingAnchors_AddsStableExplicitAnchors()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRoot);
        var pagePath = Path.Combine(tempRoot, "overview.md");

        var content = ToolsMarkdownFrontmatter
            .ApplyExistingOrDefault(
                pagePath,
                "probes/availableProbes/EmptyS3Bucket/overview.md",
                """
                # EmptyS3Bucket

                ## Details

                ### Details

                ### `Run command`

                ## Existing {: #custom-anchor}

                ```markdown
                ## Fenced heading
                ```
                """
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

    [Test]
    public void Remove_WhenPageHasGeneratedSkeleton_RemovesTldrAndSeeAlso()
    {
        var body = ToolsMarkdownFrontmatter.Remove(
            """
            ---
            id: sample
            type: reference
            ---

            # Title

            > TL;DR: Generated reference page for Title.

            Summary body.

            ## What It Does

            Details.

            ## See also

            Use the surrounding documentation navigation to move between related generated reference pages.
            """
        );

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("Summary body."));
            Assert.That(body, Does.Contain("## What It Does"));
            Assert.That(body, Does.Not.Contain("TL;DR"));
            Assert.That(body, Does.Not.Contain("See also"));
        });
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
