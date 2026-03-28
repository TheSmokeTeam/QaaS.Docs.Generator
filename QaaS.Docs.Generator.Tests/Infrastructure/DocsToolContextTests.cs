using QaaS.Docs.Tools.Infrastructure;
using NUnit.Framework;

namespace QaaS.Docs.Generator.Tests.Infrastructure;

[TestFixture]
public sealed class DocsToolContextTests
{
    [Test]
    public void Create_ResolvesResourcesFromNestedDocsToolProject()
    {
        var repositoryRoot = FindGeneratorRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "..", "..");

        var context = DocsToolContext.Create(CommandArguments.Parse(["--docs-root", docsRoot]));

        Assert.That(context.DocsRoot, Is.EqualTo(Path.GetFullPath(docsRoot)));
        Assert.That(context.ResourcesRoot, Does.Not.Contain(Path.Combine("tools", "QaaS.Docs.Tools", "Resources")));
        Assert.That(
            File.Exists(Path.Combine(context.ResourcesRoot, "hook-overviews.json")),
            Is.True,
            "The relocated docs tool resources should be discoverable from the compiled tool output.");
    }

    private static string FindGeneratorRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QaaS.Docs.Generator.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the QaaS.Docs.Generator repository root.");
    }
}
