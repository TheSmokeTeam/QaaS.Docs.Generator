using System.Text.RegularExpressions;
using NUnit.Framework;
using QaaS.Docs.Tools.Commands;
using QaaS.Docs.Tools.Infrastructure;

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

    [Test]
    public async Task UpdateHookOverviews_WhenRunAfterWrite_PassesCheckWithoutNestedTldr()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        var docsRoot = Path.Combine(tempRoot, "qaas-docs");
        var resourcesRoot = Path.Combine(tempRoot, "resources");
        var overviewPath = Path.Combine(
            docsRoot,
            "docs",
            "assertions",
            "availableAssertions",
            "DelayByAverage",
            "overview.md"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(overviewPath)!);
        Directory.CreateDirectory(resourcesRoot);

        File.WriteAllText(
            overviewPath,
            """
            ---
            id: assertions.available.delaybyaverage.overview
            type: reference
            status: stable
            since: 2.0.0
            last_verified: 2026-05-22
            applies_to: [assertions]
            keywords: [assertions, DelayByAverage, AssertionConfiguration]
            summary: "Checks for delay between input and output."
            ---
            <!-- Verified-against: QaaS.Common.Assertions\Delay\DelayByAverage.cs -->

            # DelayByAverage

            Checks for delay between input and output.
            """
        );
        File.WriteAllText(
            Path.Combine(resourcesRoot, "hook-overviews.json"),
            """
            [
              {
                "Kind": "assertions",
                "Name": "DelayByAverage",
                "Runtime": "runner",
                "WhatItDoes": "Measures latency between matching input and output streams.",
                "YamlSnippet": "Assertions:\n  - Name: DelayByAverageAssertion\n    Assertion: DelayByAverage",
                "ConfigExplanation": "This example checks whether the observed output delay stays inside the configured limit."
              }
            ]
            """
        );

        var context = new DocsToolContext(
            docsRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            resourcesRoot
        );
        var command = new UpdateHookOverviewsCommand();

        await command.ExecuteAsync(context, CommandArguments.Parse([]));
        await command.ExecuteAsync(context, CommandArguments.Parse(["--check"]));

        var content = File.ReadAllText(overviewPath);

        Assert.Multiple(() =>
        {
            Assert.That(
                content,
                Does.Contain("> TL;DR — Checks for delay between input and output.")
            );
            Assert.That(content, Does.Not.Contain("> TL;DR — ## When to use"));
        });
    }

    [Test]
    public async Task UpdateHookOverviews_WhenGeneratedStubHasTldr_UsesSummaryBody()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        var docsRoot = Path.Combine(tempRoot, "qaas-docs");
        var resourcesRoot = Path.Combine(tempRoot, "resources");
        var overviewPath = Path.Combine(
            docsRoot,
            "docs",
            "generators",
            "availableGenerators",
            "FromLettuceDataSources",
            "overview.md"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(overviewPath)!);
        Directory.CreateDirectory(resourcesRoot);

        File.WriteAllText(
            overviewPath,
            """
            ---
            id: generators.available.fromlettucedatasources.overview
            type: reference
            ---
            <!-- Verified-against: QaaS.Common.Generators\FromLettuceDataSources.cs -->

            # FromLettuceDataSources

            > TL;DR — Generates data from the enumerable of data sources it receives that is in `Lettuce` file format, presumes all items in the enumerable are deserialized into <see cref="SerializationType.Json"/>

            Generates data from the enumerable of data sources it receives that is in `Lettuce` file format, presumes all items in the enumerable are deserialized into Json
            """
        );
        File.WriteAllText(
            Path.Combine(resourcesRoot, "hook-overviews.json"),
            """
            [
              {
                "Kind": "generators",
                "Name": "FromLettuceDataSources",
                "Runtime": "runner",
                "WhatItDoes": "Reads Lettuce data source entries.",
                "YamlSnippet": "DataSources:\n  - Name: ReplayData\n    Generator: FromLettuceDataSources",
                "ConfigExplanation": "This example loads Lettuce-formatted data source entries."
              }
            ]
            """
        );

        var context = new DocsToolContext(
            docsRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            resourcesRoot
        );

        await new UpdateHookOverviewsCommand().ExecuteAsync(context, CommandArguments.Parse([]));

        var content = File.ReadAllText(overviewPath);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("deserialized into Json"));
            Assert.That(content, Does.Not.Contain("<see cref="));
        });
    }

    [Test]
    public async Task UpdateHookOverviews_WhenTrackedPageIsEnriched_PreservesTrackedTldr()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"qaas-docs-tools-tests-{Guid.NewGuid():N}"
        );
        var docsRoot = Path.Combine(tempRoot, "qaas-docs");
        var resourcesRoot = Path.Combine(tempRoot, "resources");
        var relativePath = Path.Combine(
            "docs",
            "generators",
            "availableGenerators",
            "FromLettuceDataSources",
            "overview.md"
        );
        var overviewPath = Path.Combine(docsRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(overviewPath)!);
        Directory.CreateDirectory(resourcesRoot);

        File.WriteAllText(
            overviewPath,
            """
            ---
            id: generators.available.fromlettucedatasources.overview
            type: reference
            ---
            <!-- Verified-against: QaaS.Common.Generators\FromLettuceDataSources.cs -->

            # FromLettuceDataSources

            > TL;DR — Generates data from the enumerable of data sources it receives that is in `Lettuce` file format, presumes all items in the enumerable are deserialized into Json

            ## When to use

            Existing enriched prose.

            ## YAML configuration

            Configuration prose.

            ## Minimal example

            ```yaml
            Generators: []
            ```

            ## Realistic example

            Example prose.
            """
        );
        await ProcessRunner.RunAsync("git", ["init"], docsRoot);
        await ProcessRunner.RunAsync("git", ["add", "."], docsRoot);
        await ProcessRunner.RunAsync(
            "git",
            [
                "-c",
                "user.name=QaaS Docs Test",
                "-c",
                "user.email=qaas-docs@example.test",
                "commit",
                "-m",
                "seed",
            ],
            docsRoot
        );

        File.WriteAllText(
            overviewPath,
            """
            ---
            id: generators.available.fromlettucedatasources.overview
            type: reference
            ---
            <!-- Verified-against: QaaS.Common.Generators\FromLettuceDataSources.cs -->

            # FromLettuceDataSources

            > TL;DR — Generates data from the enumerable of data sources it receives that is in `Lettuce` file format, presumes all items in the enumerable are deserialized into <see cref="SerializationType.Json"/>

            Generates data from the enumerable of data sources it receives that is in `Lettuce` file format, presumes all items in the enumerable are deserialized into <see cref="SerializationType.Json"/>
            """
        );
        WriteSingleHookCatalog(resourcesRoot, "generators", "FromLettuceDataSources");

        var context = CreateToolContext(docsRoot, tempRoot, resourcesRoot);

        await new UpdateHookOverviewsCommand().ExecuteAsync(context, CommandArguments.Parse([]));

        var content = File.ReadAllText(overviewPath);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("deserialized into Json"));
            Assert.That(content, Does.Not.Contain("<see cref="));
        });
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

    private static DocsToolContext CreateToolContext(
        string docsRoot,
        string tempRoot,
        string resourcesRoot
    ) =>
        new(
            docsRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            tempRoot,
            resourcesRoot
        );

    private static void WriteSingleHookCatalog(string resourcesRoot, string kind, string name)
    {
        File.WriteAllText(
            Path.Combine(resourcesRoot, "hook-overviews.json"),
            $$"""
            [
              {
                "Kind": "{{kind}}",
                "Name": "{{name}}",
                "Runtime": "runner",
                "WhatItDoes": "Reads generated hook data.",
                "YamlSnippet": "Hooks:\n  - Name: Sample\n    Hook: {{name}}",
                "ConfigExplanation": "This example loads the generated hook configuration."
              }
            ]
            """
        );
    }
}
