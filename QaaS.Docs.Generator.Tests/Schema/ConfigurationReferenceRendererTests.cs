using NJsonSchema;
using NUnit.Framework;
using QaaS.Docs.Generator.Schema;

namespace QaaS.Docs.Generator.Tests.Schema;

[TestFixture]
public sealed class ConfigurationReferenceRendererTests
{
    [Test]
    public async Task RenderRunner_WhenSectionIsNew_EmitsSchemaVerificationMarkers()
    {
        var schema = await JsonSchema.FromJsonAsync(
            """
            {
              "type": "object",
              "properties": {
                "Reporters": {
                  "type": "object",
                  "properties": {
                    "SaveTemplate": {
                      "type": "boolean"
                    }
                  }
                }
              }
            }
            """
        );
        var familyDocs = new FamilySchemaDocs(
            "runner-family",
            schema,
            [
                new SchemaSection(
                    "Reporters",
                    "Reporters",
                    "reporters",
                    "Reporters",
                    "Reporters configure execution-level reporting defaults.",
                    []
                ),
            ]
        );

        var documents = new ConfigurationReferenceRenderer().RenderRunner(familyDocs);
        var tableDocument = documents.Single(document =>
            document.RelativePath
            == "qaas/userInterfaces/runner/configurationSections/reporters/configurations/tableView.md"
        );
        var yamlDocument = documents.Single(document =>
            document.RelativePath
            == "qaas/userInterfaces/runner/configurationSections/reporters/configurations/yamlView.md"
        );

        const string manifestMarker =
            "<!-- Verified-against: QaaS.PackageMirror\\schemas\\runner-family\\latest\\docs-manifest.json -->";
        const string schemaMarker =
            "<!-- Verified-against: QaaS.PackageMirror\\schemas\\runner-family\\latest\\schema.json -->";

        Assert.Multiple(() =>
        {
            Assert.That(tableDocument.Content, Does.Contain(manifestMarker));
            Assert.That(tableDocument.Content, Does.Contain(schemaMarker));
            Assert.That(yamlDocument.Content, Does.Contain(manifestMarker));
            Assert.That(yamlDocument.Content, Does.Contain(schemaMarker));
        });
    }

    [Test]
    public async Task RenderRunner_WhenYamlScaffoldUsesUnionTypes_EmitsSchemaValidSamples()
    {
        var schema = await JsonSchema.FromJsonAsync(
            """
            {
              "type": "object",
              "properties": {
                "Links": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "Configuration": {
                        "type": ["object", "string"],
                        "pattern": "\\$\\{.*\\}"
                      },
                      "Expressions": {
                        "type": ["array", "string"],
                        "minItems": 1,
                        "items": {
                          "type": "string"
                        }
                      },
                      "Port": {
                        "type": ["integer", "string"],
                        "minimum": 0.0,
                        "pattern": "\\$\\{.*\\}"
                      }
                    }
                  }
                }
              }
            }
            """
        );
        var familyDocs = new FamilySchemaDocs(
            "runner-family",
            schema,
            [new SchemaSection("links", "Links", "links", "Links", "Links connect references.", [])]
        );

        var documents = new ConfigurationReferenceRenderer().RenderRunner(familyDocs);
        var yamlDocument = documents.Single(document =>
            document.RelativePath
            == "qaas/userInterfaces/runner/configurationSections/links/configurations/yamlView.md"
        );

        var normalizedContent = yamlDocument.Content.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal
        );

        Assert.Multiple(() =>
        {
            Assert.That(normalizedContent, Does.Contain("Configuration: {}"));
            Assert.That(normalizedContent, Does.Contain("Port: 0"));
            Assert.That(normalizedContent, Does.Contain("Expressions:\n      - 'value'"));
        });
    }
}
