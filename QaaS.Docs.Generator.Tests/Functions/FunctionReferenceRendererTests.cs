using NUnit.Framework;
using QaaS.Docs.Generator.Functions;

namespace QaaS.Docs.Generator.Tests.Functions;

[TestFixture]
public class FunctionReferenceRendererTests
{
    [Test]
    public void Render_WhenOverviewIsGenerated_UsesAvailableFunctionsSection()
    {
        var catalog = new FunctionCatalog(
        [
            new FunctionEntry(
                Product: "Runner",
                Group: "Builders",
                Subgroup: "Assertions",
                Kind: "function",
                DisplayName: "AssertionBuilder.Named(string name)",
                ShortName: "Named",
                OverloadName: "Named",
                Signature: "public AssertionBuilder Named(string name)",
                Summary: "Sets the assertion name.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner/Assertions/AssertionBuilder.cs",
                LineNumber: 10,
                DeclaringType: "AssertionBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true),
            new FunctionEntry(
                Product: "Runner",
                Group: "Extension Methods",
                Subgroup: "Session",
                Kind: "function",
                DisplayName: "SessionExtensions.Run(Session session)",
                ShortName: "Run",
                OverloadName: "Run",
                Signature: "public static void Run(this Session session)",
                Summary: "Runs the session.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner/Extensions/SessionExtensions.cs",
                LineNumber: 20,
                DeclaringType: "SessionExtensions",
                IsExtensionMethod: true,
                HasExplicitPlacement: false)
        ]);

        var documents = new FunctionReferenceRenderer().Render(catalog);
        var overviewDocument = documents.Single(document => document.RelativePath == "qaas/functions/index.md");

        Assert.Multiple(() =>
        {
            Assert.That(overviewDocument.Content, Does.Contain("## Available Functions"));
            Assert.That(overviewDocument.Content, Does.Contain("### Builders"));
            Assert.That(overviewDocument.Content, Does.Contain("### Extension Methods"));
            Assert.That(overviewDocument.Content, Does.Contain("- [Assertions](builders/assertions.md)"));
            Assert.That(overviewDocument.Content, Does.Contain("- [Extension Methods](extension-methods.md)"));
        });
    }
}
