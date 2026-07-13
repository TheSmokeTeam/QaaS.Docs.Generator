using NJsonSchema;
using NUnit.Framework;
using QaaS.Docs.Generator.Schema;

namespace QaaS.Docs.Generator.Tests.Schema;

[TestFixture]
public sealed class SchemaYamlRendererTests
{
    [Test]
    public async Task Render_WhenListsContainObjects_UsesConventionalSequenceIndentation()
    {
        var schema = await JsonSchema.FromJsonAsync(
            """
            {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "Name": {
                    "type": "string"
                  },
                  "Labels": {
                    "type": "array",
                    "minItems": 1,
                    "items": {
                      "type": "string"
                    }
                  },
                  "Replicas": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "properties": {
                        "Id": {
                          "type": "integer"
                        },
                        "Attributes": {
                          "type": "array",
                          "minItems": 1,
                          "items": {
                            "type": "string"
                          }
                        }
                      }
                    }
                  },
                  "Credentials": {
                    "type": "object",
                    "properties": {
                      "Token": {
                        "type": "string"
                      }
                    }
                  }
                }
              }
            }
            """
        );

        var lines = SchemaYamlRenderer.Render("Storages", schema);

        Assert.That(
            lines,
            Is.EqualTo(
                new[]
                {
                    "Storages:",
                    "  - Name: 'value'",
                    "    Labels:",
                    "      - 'value'",
                    "    Replicas:",
                    "      - Id: 0",
                    "        Attributes:",
                    "          - 'value'",
                    "    Credentials:",
                    "      Token: 'value'",
                }
            )
        );
    }

    [Test]
    public async Task Render_WhenObjectListItemHasNoProperties_EmitsEmptyObjectValue()
    {
        var schema = await JsonSchema.FromJsonAsync(
            """
            {
              "type": "array",
              "items": {
                "type": "object"
              }
            }
            """
        );

        var lines = SchemaYamlRenderer.Render("Items", schema);

        Assert.That(lines, Is.EqualTo(new[] { "Items:", "  - {}" }));
    }

    [Test]
    public async Task Render_WhenListIsOptional_EmitsInlineEmptyList()
    {
        var schema = await JsonSchema.FromJsonAsync(
            """
            {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
            """
        );

        var lines = SchemaYamlRenderer.Render("Values", schema);

        Assert.That(lines, Is.EqualTo(new[] { "Values: []" }));
    }
}
