# QaaS.Docs.Tools

`QaaS.Docs.Tools` is the C# orchestration CLI that replaced the PowerShell scripts formerly stored in `qaas-docs/scripts/`.

The project lives alongside the renderer in the same repository so the docs generation implementation and the docs-generation entry points evolve together.

## Commands

```powershell
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- generate-reference-docs --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- refresh-cli-snapshots --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- update-hook-overviews --docs-root D:\QaaS\qaas-docs
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- sync-schema-assets --docs-root D:\QaaS\qaas-docs --mirror-root D:\QaaS\QaaS.PackageMirror
dotnet run --project .\QaaS.Docs.Tools\QaaS.Docs.Tools.csproj -- validate-hook-examples --docs-root D:\QaaS\qaas-docs
```

Use `help --command <name>` to print the full option list for a specific command.
