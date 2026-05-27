using NUnit.Framework;

namespace QaaS.Docs.Generator.Tests.Generation;

[TestFixture]
public sealed class MarkdownTableCellFormatterTests
{
    [Test]
    public void Format_EscapesTableSeparatorsAndLineBreaks()
    {
        var formatted = MarkdownTableCellFormatter.Format("left|right\r\nnext");

        Assert.That(formatted, Is.EqualTo("left\\|right<br />next"));
    }

    [Test]
    public void Format_WrapsBareHttpUrlsInCodeSpans()
    {
        var formatted = MarkdownTableCellFormatter.Format("Default http://localhost:8080 endpoint");

        Assert.That(formatted, Is.EqualTo("Default `http://localhost:8080` endpoint"));
    }

    [TestCase("Default `http://localhost:8080` endpoint")]
    [TestCase("Default <http://localhost:8080> endpoint")]
    [TestCase("Default [endpoint](http://localhost:8080) value")]
    public void Format_DoesNotWrapAlreadyDelimitedUrls(string value)
    {
        Assert.That(MarkdownTableCellFormatter.Format(value), Is.EqualTo(value));
    }
}
