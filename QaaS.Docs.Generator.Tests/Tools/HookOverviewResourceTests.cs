using System.Text.RegularExpressions;
using NUnit.Framework;

namespace QaaS.Docs.Generator.Tests.Tools;

[TestFixture]
public class HookOverviewResourceTests
{
    private static readonly Regex ForbiddenWords = new(
        @"\b(AI|agents?|LLMs?|Claude|ChatGPT|copilot|ai_summary|models?|seamless|powerful|easy-to-use|robust|cutting-edge|simply|just|obviously)\b|TODO: implement|pseudocode|imagine this",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    [Test]
    public void HookOverviewResource_DoesNotContainDocsV2ForbiddenWording()
    {
        var repositoryRoot = FindRepositoryRoot();
        var resourcePath = Path.Combine(
            repositoryRoot,
            "QaaS.Docs.Tools",
            "Resources",
            "hook-overviews.json"
        );

        var content = File.ReadAllText(resourcePath);

        Assert.That(ForbiddenWords.Match(content).Success, Is.False);
    }

    private static string FindRepositoryRoot()
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

        throw new DirectoryNotFoundException(
            "Could not locate the QaaS.Docs.Generator repository root."
        );
    }
}
