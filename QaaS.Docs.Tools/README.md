# QaaS.Docs.Tools

`QaaS.Docs.Tools` is the C# orchestration CLI that replaced the PowerShell scripts formerly stored in `qaas-docs/scripts/`.

The project lives alongside the renderer in the same repository so the docs generation implementation and the docs-generation entry points evolve together.

Repository path:

- `tools/QaaS.Docs.Generator/QaaS.Docs.Tools`

## Commands

```powershell
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- generate-reference-docs --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- refresh-cli-snapshots --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- update-hook-overviews --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- sync-schema-assets --docs-root D:\QaaS\qaas-docs --mirror-root D:\QaaS\QaaS.PackageMirror
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- validate-hook-examples --docs-root D:\QaaS\qaas-docs
```

Use `help --command <name>` to print the full option list for a specific command.

## What each command owns

- `generate-reference-docs`: runs the deterministic renderer, removes obsolete generated paths, refreshes hook overviews, mirrors stable schema assets, and optionally checks site buildability.
- `refresh-cli-snapshots`: rebuilds the committed Runner and Mocker CLI snapshot files that the renderer consumes.
- `sync-schema-assets`: copies the stable family schema download assets from `QaaS.PackageMirror` into `docs/assets`.
- `update-hook-overviews`: applies the curated prose catalog to generated hook overview pages.
- `validate-hook-examples`: builds local validation hosts and runs `template` over every curated hook example.

## Documentation contract

- Command behavior is documented in the README you are reading.
- Command entrypoints and shared helpers carry XML documentation comments so the implementation stays maintainable when the docs pipeline changes.
- The tool is intentionally kept in the same repository as `QaaS.Docs.Generator` so the generator and its orchestration layer can be reviewed and versioned together.
