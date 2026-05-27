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
}
