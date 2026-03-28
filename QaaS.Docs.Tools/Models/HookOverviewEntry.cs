namespace QaaS.Docs.Tools.Models;

/// <summary>
/// Represents the curated prose content that turns a generated hook overview stub into the full authored docs page.
/// </summary>
internal sealed class HookOverviewEntry
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public string WhatItDoes { get; set; } = string.Empty;
    public string YamlSnippet { get; set; } = string.Empty;
    public string ConfigExplanation { get; set; } = string.Empty;
}
