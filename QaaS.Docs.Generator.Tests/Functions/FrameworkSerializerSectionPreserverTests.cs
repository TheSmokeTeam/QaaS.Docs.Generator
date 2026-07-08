using NUnit.Framework;
using QaaS.Docs.Generator.Functions;

namespace QaaS.Docs.Generator.Tests.Functions;

[TestFixture]
public sealed class FrameworkSerializerSectionPreserverTests
{
    [Test]
    public void Apply_WhenSerializerSectionsAreMissing_PreservesExistingSections()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(docsRoot);

        try
        {
            WriteExistingExtensionMethodsPage(docsRoot);
            var documents = new[]
            {
                new GeneratedDocument(
                    "framework/functions/extension-methods.md",
                    """
                    # Extension Methods

                    ## Extension Methods

                    ### Communication data

                    Generated communication data docs.

                    ### Date time

                    Generated date time docs.

                    ### Running communication data

                    Generated running communication data docs.

                    ### Running session data

                    Generated running session data docs.

                    ### Serilog

                    Generated Serilog docs.

                    """
                ),
            };

            var preserved = FrameworkSerializerSectionPreserver.Apply(docsRoot, documents);

            var content = preserved.Single().Content;
            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain("### Deserializer {: #deserializer}"));
                Assert.That(content, Does.Contain("Existing deserializer docs."));
                Assert.That(
                    content,
                    Does.Contain("### Serialization type {: #serialization-type}")
                );
                Assert.That(content, Does.Contain("Existing serialization type docs."));
                Assert.That(content, Does.Contain("### Serializer {: #serializer}"));
                Assert.That(content, Does.Contain("Existing serializer docs."));
                Assert.That(
                    content.IndexOf("### Deserializer", StringComparison.Ordinal),
                    Is.LessThan(
                        content.IndexOf("### Running communication data", StringComparison.Ordinal)
                    )
                );
                Assert.That(
                    content.IndexOf("### Serializer", StringComparison.Ordinal),
                    Is.LessThan(content.IndexOf("### Serilog", StringComparison.Ordinal))
                );
            });
        }
        finally
        {
            if (Directory.Exists(docsRoot))
            {
                Directory.Delete(docsRoot, recursive: true);
            }
        }
    }

    [Test]
    public void Apply_WhenGeneratedContentAlreadyHasSerializerSections_DoesNotDuplicateThem()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(docsRoot);

        try
        {
            WriteExistingExtensionMethodsPage(docsRoot);
            var documents = new[]
            {
                new GeneratedDocument(
                    "framework/functions/extension-methods.md",
                    """
                    # Extension Methods

                    ## Extension Methods

                    ### Deserializer

                    Fresh deserializer docs.

                    ### Serialization type

                    Fresh serialization type docs.

                    ### Serializer

                    Fresh serializer docs.

                    """
                ),
            };

            var preserved = FrameworkSerializerSectionPreserver.Apply(docsRoot, documents);

            var content = preserved.Single().Content;
            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain("Fresh deserializer docs."));
                Assert.That(content, Does.Not.Contain("Existing deserializer docs."));
                Assert.That(CountOccurrences(content, "### Deserializer"), Is.EqualTo(1));
                Assert.That(CountOccurrences(content, "### Serialization type"), Is.EqualTo(1));
                Assert.That(CountOccurrences(content, "### Serializer"), Is.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(docsRoot))
            {
                Directory.Delete(docsRoot, recursive: true);
            }
        }
    }

    private static void WriteExistingExtensionMethodsPage(string docsRoot)
    {
        var path = Path.Combine(docsRoot, "docs", "framework", "functions", "extension-methods.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            # Extension Methods

            ## Extension Methods {: #extension-methods_1}

            ### Deserializer {: #deserializer}

            Existing deserializer docs.

            ### Serialization type {: #serialization-type}

            Existing serialization type docs.

            ### Serializer {: #serializer}

            Existing serializer docs.

            ### Serilog {: #serilog}

            Existing Serilog docs.

            """
        );
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
