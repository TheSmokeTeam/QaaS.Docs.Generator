# Copilot instructions — QaaS.Docs.Generator

Read `AGENTS.md` at the repo root first — it documents the generation pipeline, CLI args, and the hard rules.

Essentials:
- net10.0; NUnit 4.5.1; `dotnet build -m` / `dotnet test --no-build`.
- Generated docs are CRLF-canonical with YAML frontmatter and `<!-- Verified-against -->` markers — preserve all three.
- Function reference only includes methods tagged `<qaas-docs-placement group subgroup />`; malformed XML doc comments are silently skipped.
- mkdocs.yml nav is updated only inside marked comment blocks.
- CLI snapshots (Snapshots/*.json) are committed artifacts refreshed via QaaS.Docs.Tools.
- `--check` is the drift gate (exit 2 on differences) — keep regeneration byte-stable.
