using System.Text.Json;
using QaaS.Docs.Tools.Infrastructure;
using QaaS.Docs.Tools.Models;

namespace QaaS.Docs.Tools;

/// <summary>
/// Loads the maintained hook-overview prose catalog from JSON so the docs commands can enrich generated pages and
/// validate example templates without relying on PowerShell dot-sourcing.
/// </summary>
internal sealed class HookOverviewCatalog
{
    private readonly IReadOnlyList<HookOverviewEntry> _entries;

    private HookOverviewCatalog(IReadOnlyList<HookOverviewEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// Gets the curated hook-overview entries loaded from the JSON catalog.
    /// </summary>
    public IReadOnlyList<HookOverviewEntry> Entries => _entries;

    /// <summary>
    /// Loads the catalog from the tool resource directory.
    /// </summary>
    public static async Task<HookOverviewCatalog> LoadAsync(string resourcesRoot)
    {
        var path = Path.Combine(resourcesRoot, "hook-overviews.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Hook overview catalog not found at '{path}'.", path);
        }

        var json = await Utf8File.ReadAllTextAsync(path);
        var entries = JsonSerializer.Deserialize<List<HookOverviewEntry>>(json)
            ?? throw new InvalidOperationException($"Could not deserialize hook overview catalog from '{path}'.");

        return new HookOverviewCatalog(entries);
    }
}
