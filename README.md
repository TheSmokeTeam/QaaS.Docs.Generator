# QaaS.Docs.Generator

`QaaS.Docs.Generator` is the deterministic documentation renderer for the QaaS docs site.

This repository is consumed by [`qaas-docs`](https://github.com/TheSmokeTeam/qaas-docs) as a git submodule so the renderer can evolve independently without living inline inside the docs repository.

It consumes three kinds of inputs:

1. Mirror-owned schema contracts from `QaaS.PackageMirror`
2. CLI snapshots captured into `Snapshots/` from the current Runner, Mocker, and Framework worktrees
3. Source-level XML documentation and `qaas-docs` placement tags in `QaaS.Runner`, `QaaS.Mocker`, and `QaaS.Framework`

It writes the generated markdown into the stable `docs/` paths already used by `mkdocs.yml`.

## Why the inputs are split this way

- Schema structure belongs in `QaaS.PackageMirror`, because that repo already owns family schema generation.
- CLI snapshots are committed here on purpose so the docs build has deterministic inputs, but they are refreshed automatically by `qaas-docs/scripts/Generate-ReferenceDocs.ps1`.
- Function grouping lives in the source repos so each documented public method carries its own docstring and docs placement metadata.

## Refresh process

1. Regenerate mirror artifacts in `QaaS.PackageMirror`.
2. Refresh the committed CLI snapshot files from `qaas-docs/scripts/Generate-ReferenceDocs.ps1` or `qaas-docs/scripts/Refresh-CliSnapshots.ps1`.
3. Update the annotated public methods in `QaaS.Runner`, `QaaS.Mocker`, and `QaaS.Framework` when the curated user-facing API surface changes.
4. From `qaas-docs`, run `scripts/Generate-ReferenceDocs.ps1`.
5. From `qaas-docs`, run the same script with `-Check -BuildSite` before opening a PR.

## CLI snapshot contract

The CLI snapshots are intentionally committed artifacts.

- They are captured from the live `Bootstrap.New(...)` help paths in `QaaS.Runner` and `QaaS.Mocker`.
- They are updated manually and committed after a one-time local capture from those live help paths.
- The renderer consumes only the committed snapshot JSON files, which keeps `QaaS.Docs.Generator` buildable without project references to sibling repositories.

## Important constraints

- Generated files are checked for content drift by full-file comparison.
- Generated markdown content is the source of truth; the renderer does not prepend synthetic hash headers.
- The generator intentionally does not depend on committed docs exporters inside `QaaS.Runner` or `QaaS.Mocker`.
- Functions are discovered from the current source tree but included only when a public method carries a `qaas-docs` placement tag in its XML documentation comment.

## Repository-local verification

This repository validates the buildable renderer surface:

```powershell
dotnet build .\QaaS.Docs.Generator.csproj -c Release
```

The full end-to-end docs verification remains in `qaas-docs`, because that is where `Generate-ReferenceDocs.ps1` runs against the mirror artifacts and the generated documentation tree.
