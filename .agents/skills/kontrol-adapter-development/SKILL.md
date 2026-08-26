---
name: kontrol-adapter-development
description: Create, extend, debug, review, or test Kontrol game adapters and the public Kontrol SDK. Use for new adapters, input schemas, mappings, IPC contracts, telemetry, diagnostics, runtime lifetime, adapter tests, and adapter documentation. Do not use for Space Engineers 2 private-game API maintenance or for tagging and publishing releases; use the dedicated SE2 or release skill instead.
---

# Kontrol Adapter Development

## Establish context

1. Locate the repository root containing `Kontrol.Adapters.slnx`.
2. Read every applicable `AGENTS.md` from the working directory through the repository root.
3. Read `docs/ARCHITECTURE.md`, `docs/DEVELOPING_ADAPTERS.md`, the selected adapter's `adapter.manifest.json`, README, and changelog.
4. Inspect the working tree before editing. Preserve unrelated user changes and generated local game references.

## Choose the workflow

- For a new adapter, follow the existing `DummyAdapter` layout and add an adapter root, manifest, version properties, implementation project, tests, README, and changelog.
- For behavior changes, trace the full boundary from `InputFrame` through adapter translation to the game API before editing.
- For diagnosis, determine the failing boundary before implementing a fix. Read [references/debugging.md](references/debugging.md).
- For SE2-specific Harmony patches, injection, game symbols, or compatibility, switch to `$kontrol-se2-maintenance`.
- For versions, packages, tags, GitHub Releases, or catalogs, switch to `$kontrol-adapter-release`.

## Preserve adapter contracts

- Treat published adapter IDs, input IDs, and schema indices as stable. Append new inputs; do not reorder or reuse indices.
- Keep the SDK API and IPC contract under the single SDK version defined by the repository.
- Keep adapters independent from WPF and host-owned device polling. Cross process/thread boundaries only through SDK contracts and immutable values.
- Keep normal adapter diagnostics on host IPC. Do not add normal game-side disk logging; keep any debug fallback explicit and opt-in.
- Never commit proprietary game assemblies, local references, build output, logs, dumps, or generated packages.

## Repository privacy and path hygiene

- Never add developer names, personal email addresses, usernames, home directories, hostnames, machine identifiers, absolute local paths, Steam installation paths, or AppData paths to tracked files, documentation, examples, tests, logs, compatibility evidence, generated metadata, or issue bodies.
- Do not hardcode developer-specific paths or machine-specific defaults. Use generic placeholders, command-line parameters, environment variables, runtime discovery, temporary test directories, and relative repository paths.
- Redact personal and machine-specific values from logs, stack traces, command lines, screenshots, and diagnostic evidence before recording or sharing them.
- Before committing, search the complete diff and tracked-file set for personal identifiers and machine-specific path patterns. Organization and product metadata such as `Komandio Labs` is allowed.

## Implement and document

1. Make the smallest coherent source and test change.
2. Update the adapter README when mappings, game hooks, loading behavior, or validation requirements change.
3. Update the adapter changelog for user-visible behavior.
4. Update schemas and versioning documentation only when the public contract changes.

## Validate

Before a normal-output build on Windows, inspect whether Kontrol or the target game is running. If either process can lock changed outputs, ask the user to close it. Do not create alternate output directories to bypass locks.

Use the repository command as the user-facing validation entry point:

```text
python scripts/kontrol_adapters.py test --adapter <slug>
```

Also build every affected project and run its relevant tests. For cross-project changes, build and test `Kontrol.Adapters.slnx`. Report the first meaningful failure immediately and do not claim deployment when the normal build did not complete.

Finish by checking `git diff --check`, reviewing Git status, and confirming that no forbidden artifacts are staged. Do not commit or push unless the user explicitly requests it.
