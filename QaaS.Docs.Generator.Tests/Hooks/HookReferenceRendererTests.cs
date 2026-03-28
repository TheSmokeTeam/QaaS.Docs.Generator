using NUnit.Framework;
using QaaS.Docs.Generator.Hooks;

namespace QaaS.Docs.Generator.Tests.Hooks;

[TestFixture]
public class HookReferenceRendererTests
{
    [Test]
    public void ParseHookDocumentation_WhenConcreteHookExists_PrefersAnnotatedConcreteType()
    {
        const string source = """
            namespace Sample;

            /// <summary>
            /// Wrapper summary.
            /// </summary>
            public class EmptyRedisByChunks<TConfig>
            {
            }

            /// <summary>
            /// Concrete summary.
            /// </summary>
            /// <qaas-docs group="Redis maintenance" subgroup="Data cleanup" />
            public class EmptyRedisByChunks : EmptyRedisByChunks<object>
            {
            }
            """;

        var documentation = HookReferenceRenderer.ParseHookDocumentation(source, "EmptyRedisByChunks.cs", "EmptyRedisByChunks");

        Assert.Multiple(() =>
        {
            Assert.That(documentation.Summary, Is.EqualTo("Concrete summary."));
            Assert.That(documentation.Placement, Is.Not.Null);
            Assert.That(documentation.Placement!.Group, Is.EqualTo("Redis maintenance"));
            Assert.That(documentation.Placement.Subgroup, Is.EqualTo("Data cleanup"));
        });
    }

    [Test]
    public void RenderOverviewPage_DoesNotIncludeLogicalGroupCallout()
    {
        var content = HookReferenceRenderer.RenderOverviewPage(
            title: "HookName",
            summary: "Hook summary.",
            customOverviewContent: "## More\n\nDetails.");

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("Hook summary."));
            Assert.That(content, Does.Contain("## More"));
            Assert.That(content, Does.Not.Contain("Logical group"));
        });
    }
}
