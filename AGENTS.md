# AGENTS.md — QaaS.Docs.Generator

Guidance for AI agents working in this repository.

## What this repo is

Deterministic markdown generator for the QaaS docs site (qaas-docs). Produces CLI reference, schema configuration reference, function reference, and hook reference pages from: a schema mirror (`--mirror-root`, layout `schemas/{runner-family|mocker-family}/latest/schema.json` + `docs-manifest.json`), committed CLI snapshots (`Snapshots/runner-cli.json`, `mocker-cli.json`), and Roslyn scans of Runner/Mocker/Framework source trees.

## Projects (net10.0)

- **QaaS.Docs.Generator** — main console app. Namespaces: `Cli`, `Schema`, `Functions`, `Hooks`, `Navigation`, `Generation`. Entry: `Program.cs` → `GeneratorOptions.Parse` → parallel loads → render passes (`CliReferenceRenderer`, `ConfigurationReferenceRenderer`, `HookReferenceRenderer`, `FunctionReferenceRenderer`) → `GeneratedDocumentWriter` → `MkDocsNavigationRenderer`. Exit codes: 0 ok / 1 args / 2 drift-or-write / 3 exception.
- **QaaS.Docs.Tools** — maintenance verbs: `generate-reference-docs`, `refresh-cli-snapshots`, `sync-schema-assets`, `update-hook-overviews`, `validate-hook-examples`.
- **QaaS.Docs.Generator.Tests** — NUnit 4.5.1, 10 test files mirroring the namespaces.

Dependencies: NJsonSchema 11.1.0, Microsoft.CodeAnalysis.CSharp 5.3.0. Deliberately NO project references to Runner/Mocker/Framework — snapshots + source scanning keep this repo independently buildable.

## Build & test

```powershell
dotnet build -m --no-restore
dotnet test --no-build
# full CLI run requires the five roots:
dotnet run --project QaaS.Docs.Generator -- --docs-root <qaas-docs> --mirror-root <mirror> --runner-root <r> --mocker-root <m> --framework-root <f> [--check]
```

## Hard rules

1. **CRLF is canonical** for all generated files (`GeneratedDocumentLineEndings`). Never let LF slip in — golden tests and drift checks (`--check`, exit 2) will fail.
2. **Placement tags gate function docs**: a public method appears in the function reference only with `<qaas-docs-placement group="X" subgroup="Y" />` in its XML docs. Malformed XML = silent skip.
3. **mkdocs.yml is edited only via marked blocks** (`# <!-- runner-functions-start/end -->` etc.). Don't hand-edit inside markers; missing markers silently skip updates.
4. **Snapshots are committed artifacts** — refresh via `QaaS.Docs.Tools refresh-cli-snapshots`, then commit the JSON.
5. Frontmatter and `<!-- Verified-against -->` markers are preserved across regenerations — don't strip them.
6. Hook overview pages get a second-stage enrichment by QaaS.Docs.Tools; generator-stage `--check` excludes them.

## Gotchas

- Wrong `--mirror-root` → `InvalidOperationException` in `FamilySchemaDocs.LoadAsync`.
- Schema section order comes from `docs-manifest.json` (`Order`); fallback hardcoded in `FallbackSections.ForFamily()`.
- Heading anchor collisions are not deduplicated — keep headings unique.
- Recent work is about reducing generated-docs churn; prefer changes that keep regeneration byte-stable.

## Process

Non-trivial changes follow the QaaS harness pipeline (plan → contract → implement → adversarial evaluation, all rubric dimensions ≥7/10). Golden-file tests first for renderer changes; run `--check` against a real docs tree when touching writers. Conventional commits.
