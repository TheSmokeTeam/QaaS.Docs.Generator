using NUnit.Framework;
using QaaS.Docs.Generator.Functions;

namespace QaaS.Docs.Generator.Tests.Functions;

[TestFixture]
public class FunctionReferenceRendererTests
{
    [Test]
    public async Task BuildAsync_WhenRemarksAreGeneratedApiSurfaceFallback_DropsRemarks()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var runnerRoot = Path.Combine(tempRoot, "runner");
        var mockerRoot = Path.Combine(tempRoot, "mocker");
        var frameworkRoot = Path.Combine(tempRoot, "framework");
        Directory.CreateDirectory(runnerRoot);
        Directory.CreateDirectory(mockerRoot);
        Directory.CreateDirectory(frameworkRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(runnerRoot, "SessionBuilder.cs"),
                """
                public class SessionBuilder
                {
                    /// <summary>
                    /// Sets the session name.
                    /// </summary>
                    /// <remarks>
                    /// Use this method when working with the documented Runner session builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
                    /// </remarks>
                    /// <qaas-docs group="Builders" subgroup="Sessions" />
                    public SessionBuilder Named(string name) => this;
                }
                """
            );

            var catalog = await FunctionCatalogBuilder.BuildAsync(
                runnerRoot,
                mockerRoot,
                frameworkRoot
            );

            Assert.That(catalog.Entries.Single().Remarks, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task BuildAsync_SummaryWithParamrefAndTypeparamref_RendersTheReferencedNames()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var runnerRoot = Path.Combine(tempRoot, "runner");
        var mockerRoot = Path.Combine(tempRoot, "mocker");
        var frameworkRoot = Path.Combine(tempRoot, "framework");
        Directory.CreateDirectory(runnerRoot);
        Directory.CreateDirectory(mockerRoot);
        Directory.CreateDirectory(frameworkRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(runnerRoot, "DataExtensions.cs"),
                """
                public static class DataExtensions
                {
                    /// <summary>
                    /// Converts the <paramref name="body"/> into <typeparamref name="TBody"/> and returns it
                    /// (see <see cref="ConvertBodyTo"/>).
                    /// </summary>
                    /// <remarks>
                    /// Example: `if (data.TryGetBodyAs&lt;string&gt;(out var text)) { ... }`
                    /// </remarks>
                    /// <qaas-docs group="Extension Methods" subgroup="Data" />
                    public static TBody GetBodyAs<TBody>(object body) => default!;
                }
                """
            );

            var catalog = await FunctionCatalogBuilder.BuildAsync(
                runnerRoot,
                mockerRoot,
                frameworkRoot
            );

            Assert.Multiple(() =>
            {
                Assert.That(
                    catalog.Entries.Single().Summary,
                    Is.EqualTo("Converts the body into TBody and returns it (see ConvertBodyTo).")
                );
                Assert.That(
                    catalog.Entries.Single().Remarks,
                    Is.EqualTo("Example: `if (data.TryGetBodyAs<string>(out var text)) { ... }`")
                );
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public void Render_WhenOverviewIsGenerated_UsesAvailableFunctionsSection()
    {
        var catalog = new FunctionCatalog([
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
                HasExplicitPlacement: true
            ),
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
                HasExplicitPlacement: false
            ),
        ]);

        var documents = new FunctionReferenceRenderer().Render(catalog);
        var overviewDocument = documents.Single(document =>
            document.RelativePath == "qaas/functions/index.md"
        );

        Assert.Multiple(() =>
        {
            Assert.That(overviewDocument.Content, Does.Contain("## Available Functions"));
            Assert.That(overviewDocument.Content, Does.Contain("### Builders"));
            Assert.That(overviewDocument.Content, Does.Contain("### Extension Methods"));
            Assert.That(
                overviewDocument.Content,
                Does.Contain("- [Assertions](builders/assertions.md)")
            );
            Assert.That(
                overviewDocument.Content,
                Does.Contain("- [Extension Methods](extension-methods.md)")
            );
        });
    }

    [Test]
    public void Render_WhenFunctionPageIsGenerated_EmitsDistinctVerificationMarkers()
    {
        var catalog = new FunctionCatalog([
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Reporters",
                Kind: "function",
                DisplayName: "ReporterBuilder.ShouldSaveLogs(bool shouldSaveLogs)",
                ShortName: "ShouldSaveLogs",
                OverloadName: "ShouldSaveLogs",
                Signature: "public ReporterBuilder ShouldSaveLogs(bool shouldSaveLogs)",
                Summary: "Controls whether logs are saved.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner.Assertions/ConfigurationObjects/ReporterBuilder.cs",
                LineNumber: 108,
                DeclaringType: "ReporterBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Reporters",
                Kind: "function",
                DisplayName: "ReporterBuilder.ShouldSaveAttachments(bool shouldSaveAttachments)",
                ShortName: "ShouldSaveAttachments",
                OverloadName: "ShouldSaveAttachments",
                Signature: "public ReporterBuilder ShouldSaveAttachments(bool shouldSaveAttachments)",
                Summary: "Controls whether attachments are saved.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner.Assertions/ConfigurationObjects/ReporterBuilder.cs",
                LineNumber: 121,
                DeclaringType: "ReporterBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
        ]);

        var documents = new FunctionReferenceRenderer().Render(catalog);
        var reportersDocument = documents.Single(document =>
            document.RelativePath == "qaas/functions/builders/reporters.md"
        );
        const string marker =
            "<!-- Verified-against: QaaS.Runner\\QaaS.Runner.Assertions\\ConfigurationObjects\\ReporterBuilder.cs -->";

        Assert.Multiple(() =>
        {
            Assert.That(reportersDocument.Content, Does.StartWith(marker));
            Assert.That(CountOccurrences(reportersDocument.Content, marker), Is.EqualTo(1));
        });
    }

    [Test]
    public void Render_WhenExecutionUpdatesReporters_GroupsUnderReportersSection()
    {
        var catalog = new FunctionCatalog([
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Executions",
                Kind: "function",
                DisplayName: "ExecutionBuilder.UpdateReporters(ReporterBuilder reporterBuilder)",
                ShortName: "UpdateReporters",
                OverloadName: "UpdateReporters",
                Signature: "public ExecutionBuilder UpdateReporters(ReporterBuilder reporterBuilder)",
                Summary: "Updates the reporter configuration.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner/ExecutionBuilder.cs",
                LineNumber: 618,
                DeclaringType: "ExecutionBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Executions",
                Kind: "function",
                DisplayName: "ExecutionBuilder.WithMetadata(MetaDataConfig metaDataConfig)",
                ShortName: "WithMetadata",
                OverloadName: "WithMetadata",
                Signature: "public ExecutionBuilder WithMetadata(MetaDataConfig metaDataConfig)",
                Summary: "Sets the metadata configuration.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner/ExecutionBuilder.cs",
                LineNumber: 605,
                DeclaringType: "ExecutionBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
        ]);

        var documents = new FunctionReferenceRenderer().Render(catalog);
        var executionsDocument = documents.Single(document =>
            document.RelativePath == "qaas/functions/builders/executions.md"
        );

        Assert.Multiple(() =>
        {
            Assert.That(executionsDocument.Content, Does.Contain("## Reporters"));
            Assert.That(executionsDocument.Content, Does.Contain("### `UpdateReporters`"));
            Assert.That(executionsDocument.Content, Does.Not.Contain("## Collection helpers"));
        });
    }

    [Test]
    public void Render_WhenReporterSavesTemplate_GroupsUnderReportingAndArtifacts()
    {
        var catalog = new FunctionCatalog([
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Reporters",
                Kind: "function",
                DisplayName: "ReporterBuilder.ShouldSaveTemplate(bool shouldSaveTemplate)",
                ShortName: "ShouldSaveTemplate",
                OverloadName: "ShouldSaveTemplate",
                Signature: "public ReporterBuilder ShouldSaveTemplate(bool shouldSaveTemplate)",
                Summary: "Controls whether templates are saved.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner.Assertions/ConfigurationObjects/ReporterBuilder.cs",
                LineNumber: 134,
                DeclaringType: "ReporterBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
            new FunctionEntry(
                Product: "Runner",
                Group: "Configuration as Code",
                Subgroup: "Reporters",
                Kind: "function",
                DisplayName: "ReporterBuilder.ConfigureReportPortal(ReportPortalConfig reportPortalConfig)",
                ShortName: "ConfigureReportPortal",
                OverloadName: "ConfigureReportPortal",
                Signature: "public ReporterBuilder ConfigureReportPortal(ReportPortalConfig reportPortalConfig)",
                Summary: "Sets the ReportPortal configuration.",
                Remarks: string.Empty,
                RelativePath: "QaaS.Runner.Assertions/ConfigurationObjects/ReporterBuilder.cs",
                LineNumber: 160,
                DeclaringType: "ReporterBuilder",
                IsExtensionMethod: false,
                HasExplicitPlacement: true
            ),
        ]);

        var documents = new FunctionReferenceRenderer().Render(catalog);
        var reportersDocument = documents.Single(document =>
            document.RelativePath == "qaas/functions/builders/reporters.md"
        );

        Assert.Multiple(() =>
        {
            Assert.That(reportersDocument.Content, Does.Contain("## Reporting and artifacts"));
            Assert.That(reportersDocument.Content, Does.Contain("### `ShouldSaveTemplate`"));
        });
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
