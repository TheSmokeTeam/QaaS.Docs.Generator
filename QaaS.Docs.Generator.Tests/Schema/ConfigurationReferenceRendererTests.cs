using NJsonSchema;
using NUnit.Framework;
using QaaS.Docs.Generator.Schema;

namespace QaaS.Docs.Generator.Tests.Schema;

[TestFixture]
public sealed class ConfigurationReferenceRendererTests
{
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
