# QaaS.Docs.Generator

`QaaS.Docs.Generator` is the deterministic documentation renderer for the QaaS docs site.

This repository is consumed by [`qaas-docs`](https://github.com/TheSmokeTeam/qaas-docs) as a git submodule so the renderer can evolve independently without living inline inside the docs repository.

It consumes three kinds of inputs:

1. Mirror-owned schema contracts from `QaaS.PackageMirror`
2. Committed CLI snapshots stored under `Snapshots/`
3. A committed function manifest stored under `Functions/function-manifest.json`

It writes the generated markdown into the stable `docs/` paths already used by `mkdocs.yml`.

## Why the inputs are split this way

- Schema structure belongs in `QaaS.PackageMirror`, because that repo already owns family schema generation.
- CLI snapshots are committed here on purpose so `QaaS.Runner` and `QaaS.Mocker` do not need any docs-only export code.
- The function manifest is committed here so grouping and inclusion rules can change without touching the source repos.

## Refresh process

1. Regenerate mirror artifacts in `QaaS.PackageMirror`.
2. Refresh the committed CLI snapshot files manually only when the live CLI help or option surface changes.
3. Update `Functions/function-manifest.json` only when the curated user-facing API surface changes.
4. From `qaas-docs`, run `scripts/Generate-ReferenceDocs.ps1`.
5. From `qaas-docs`, run the same script with `-Check -BuildSite` before opening a PR.

## CLI snapshot contract

The CLI snapshots are intentionally committed artifacts.

- They are captured from the live `Bootstrap.New(...)` help paths in `QaaS.Runner` and `QaaS.Mocker`.
- They are updated manually and committed after a one-time local capture from those live help paths.
- The renderer consumes only the committed snapshot JSON files, which keeps `QaaS.Docs.Generator` buildable without project references to sibling repositories.

## Important constraints

- Generated files are checked for drift by hash.
- The generator intentionally does not depend on committed docs exporters inside `QaaS.Runner` or `QaaS.Mocker`.
- Functions are discovered from the current source tree but included only when listed in the manifest.

## Repository-local verification

This repository validates the buildable renderer surface:

```powershell
dotnet build .\QaaS.Docs.Generator.csproj -c Release
```

The full end-to-end docs verification remains in `qaas-docs`, because that is where `Generate-ReferenceDocs.ps1` runs against the mirror artifacts and the generated documentation tree.
