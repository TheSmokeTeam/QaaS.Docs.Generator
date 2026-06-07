using NUnit.Framework;
using QaaS.Docs.Generator.Navigation;

namespace QaaS.Docs.Generator.Tests.Navigation;

[TestFixture]
public sealed class MkDocsNavigationRendererTests
{
    [Test]
    public void Update_WhenWritingFunctionSectionPages_CarriesParentVerificationMarkers()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(docsRoot);

        try
        {
            WriteMkDocsFile(docsRoot);
            WriteFunctionOverview(
                docsRoot,
                "qaas/functions/index.md",
                "Runner Functions",
                "Builders",
                "Assertions",
                "builders/assertions.md"
            );
            WriteFunctionOverview(
                docsRoot,
                "mocker/functions/index.md",
                "Mocker Functions",
                "Builders",
                "Executions",
                "builders/executions.md"
            );
            WriteFunctionOverview(
                docsRoot,
                "framework/functions/index.md",
                "Framework Functions",
                "Functions",
                "YAML",
                "yaml.md"
            );
            WriteFunctionPage(
                docsRoot,
                "qaas/functions/builders/assertions.md",
                "Assertions",
                "QaaS.Runner\\QaaS.Runner.Assertions\\ConfigurationObjects\\AssertionBuilder.cs"
            );
            WriteFunctionPage(
                docsRoot,
                "mocker/functions/builders/executions.md",
                "Executions",
                "QaaS.Mocker\\QaaS.Mocker\\ExecutionBuilder.cs"
            );
            WriteFunctionPage(
                docsRoot,
                "framework/functions/yaml.md",
                "YAML",
                "QaaS.Framework\\QaaS.Framework.Configurations\\ConfigurationBuilderExtensions\\YamlConfigurationBuilderExtension.cs"
            );
            WriteHookIndexes(docsRoot);

            new MkDocsNavigationRenderer().Update(docsRoot, check: false);

            var sectionPage = File.ReadAllText(
                Path.Combine(
                    docsRoot,
                    "docs",
                    "qaas",
                    "functions",
                    "builders",
                    "assertions-sections",
                    "general.md"
                )
            );

            Assert.That(
                sectionPage,
                Does.Contain(
                    "<!-- Verified-against: QaaS.Runner\\QaaS.Runner.Assertions\\ConfigurationObjects\\AssertionBuilder.cs -->"
                )
            );
            Assert.That(
                sectionPage,
                Does.Contain(
                    "> TL;DR — This page mirrors the `General` section from [Assertions](../assertions.md) as a focused reference."
                )
            );
            Assert.That(sectionPage, Does.Contain("## C# (CAC) usage"));
            Assert.That(sectionPage, Does.Contain("- [Assertions](../assertions.md)"));
            Assert.That(
                File.Exists(
                    Path.Combine(
                        docsRoot,
                        "docs",
                        "qaas",
                        "functions",
                        "builders",
                        "assertions-sections",
                        "when-to-use.md"
                    )
                ),
                Is.False
            );
        }
        finally
        {
            if (Directory.Exists(docsRoot))
            {
                Directory.Delete(docsRoot, recursive: true);
            }
        }
    }

    private static void WriteMkDocsFile(string docsRoot)
    {
        File.WriteAllText(
            Path.Combine(docsRoot, "mkdocs.yml"),
            """
            nav:
              - Runner:
                  # qaas-docs-generator start: runner-functions
                  # qaas-docs-generator end: runner-functions
              - Mocker:
                  # qaas-docs-generator start: mocker-functions
                  # qaas-docs-generator end: mocker-functions
              - Framework:
                  # qaas-docs-generator start: framework-functions
                  # qaas-docs-generator end: framework-functions
              - Assertions:
                  # qaas-docs-generator start: hook-assertions
                  # qaas-docs-generator end: hook-assertions
              - Generators:
                  # qaas-docs-generator start: hook-generators
                  # qaas-docs-generator end: hook-generators
              - Probes:
                  # qaas-docs-generator start: hook-probes
                  # qaas-docs-generator end: hook-probes
              - Processors:
                  # qaas-docs-generator start: hook-processors
                  # qaas-docs-generator end: hook-processors

            """
        );
    }

    private static void WriteFunctionOverview(
        string docsRoot,
        string relativePath,
        string title,
        string group,
        string pageTitle,
        string pagePath
    )
    {
        WriteDocsFile(
            docsRoot,
            relativePath,
            $"""
            # {title}

            ## Available Functions

            ### {group}

            - [{pageTitle}]({pagePath})

            """
        );
    }

    private static void WriteFunctionPage(
        string docsRoot,
        string relativePath,
        string title,
        string markerPath
    )
    {
        WriteDocsFile(
            docsRoot,
            relativePath,
            $"""
            ---
            id: {relativePath.Replace('/', '.').Replace(".md", string.Empty)}
            type: reference
            status: stable
            since: 2.0.0
            last_verified: 2026-05-27
            applies_to: [qaas]
            keywords: [qaas, reference]
            summary: "Reference page."
            ---
            <!-- Verified-against: {markerPath} -->

            # {title}

            > TL;DR — Source-backed function page.

            ## When to use

            Use this section to choose the right function.

            ## General

            ### `DoThing`

            ??? info "Source file, signature, and docstring"
                **Member**
                `Example.DoThing()`

            ## See also

            - [Functions](../index.md)

            """
        );
    }

    private static void WriteHookIndexes(string docsRoot)
    {
        WriteHookIndex(docsRoot, "assertions/index.md", "Assertions", "availableAssertions");
        WriteHookIndex(docsRoot, "generators/index.md", "Generators", "availableGenerators");
        WriteHookIndex(docsRoot, "probes/index.md", "Probes", "availableProbes");
        WriteHookIndex(docsRoot, "processors/index.md", "Processors", "availableProcessors");
    }

    private static void WriteHookIndex(
        string docsRoot,
        string relativePath,
        string title,
        string availableDirectory
    )
    {
        WriteDocsFile(
            docsRoot,
            relativePath,
            $"""
            # {title}

            ## Available Hooks

            ### General

            - [Example]({availableDirectory}/Example/overview.md): Example hook.

            """
        );
    }

    private static void WriteDocsFile(string docsRoot, string relativePath, string content)
    {
        var path = Path.Combine(
            docsRoot,
            "docs",
            relativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
