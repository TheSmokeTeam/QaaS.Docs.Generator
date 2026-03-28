namespace QaaS.Docs.Tools.Models;

/// <summary>
/// Represents the curated prose content that turns a generated hook overview stub into the full authored docs page.
/// </summary>
internal sealed class HookOverviewEntry
{
    /// <summary>
    /// Gets or sets the hook family, such as <c>assertions</c> or <c>probes</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hook name as it appears in generated docs.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime name used to choose the correct example template host.
    /// </summary>
    public string Runtime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the curated prose for the "What It Does" section.
    /// </summary>
    public string WhatItDoes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the YAML snippet inserted into the generated overview page.
    /// </summary>
    public string YamlSnippet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human explanation of how the example configuration fields behave.
    /// </summary>
    public string ConfigExplanation { get; set; } = string.Empty;
}
