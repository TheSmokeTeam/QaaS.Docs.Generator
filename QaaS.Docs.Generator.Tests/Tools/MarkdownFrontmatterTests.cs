using NUnit.Framework;
using ToolsMarkdownFrontmatter = QaaS.Docs.Tools.Infrastructure.MarkdownFrontmatter;

namespace QaaS.Docs.Generator.Tests.Tools;

[TestFixture]
public sealed class MarkdownFrontmatterTests
{
    [Test]
    public void ApplyExistingOrDefault_WhenExistingPageHasFrontmatter_PreservesIt()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"qaas-docs-tools-tests-{Guid.NewGuid():N}");
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
            """);

        var content = ToolsMarkdownFrontmatter.ApplyExistingOrDefault(
            pagePath,
            "probes/availableProbes/EmptyS3Bucket/overview.md",
            "# EmptyS3Bucket\n\nGenerated body.");

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
        var tempRoot = Path.Combine(Path.GetTempPath(), $"qaas-docs-tools-tests-{Guid.NewGuid():N}");
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
            """);

        var content = ToolsMarkdownFrontmatter
            .ApplyExistingOrDefault(
                pagePath,
                "probes/availableProbes/EmptyS3Bucket/overview.md",
                """
                # EmptyS3Bucket

                <!-- Verified-against: QaaS.Common.Probes\S3\EmptyS3Bucket.cs -->

                Generated body.
                """)
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
            """);

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
            """);

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.StartWith("# Title"));
            Assert.That(body, Does.Not.Contain("Verified-against"));
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
