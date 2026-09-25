---
name: kontrol-adapter-development
description: Create, extend, debug, review, or test Kontrol game adapters and the public Kontrol SDK. Use for new adapters, input schemas, mappings, IPC contracts, telemetry, diagnostics, runtime lifetime, adapter tests, game-update investigation, and adapter documentation. Use the dedicated SE2 skill for SE2 private-game API maintenance and the release skill for packaging or publication.
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
- For managed game-assembly investigation, follow [Local managed-assembly reverse engineering](#local-managed-assembly-reverse-engineering) and keep generated output under ignored `.scratch/`.
- For SE2-specific Harmony patches, injection, game symbols, or compatibility, switch to `$kontrol-se2-maintenance`.
- For versions, packages, tags, GitHub Releases, or catalogs, switch to `$kontrol-adapter-release`.

## Local managed-assembly reverse engineering

Use the shared local ILSpy CLI workflow for managed game assemblies. Do not add adapter-specific decompiler projects or save generated source under tracked adapter folders.

- Run from the `kontrol-adapters` repository root. Keep ILSpy at `.scratch/tools/ilspycmd.exe`; if needed, install pinned `ilspycmd` version `11.1.0.9782` with `dotnet tool install ilspycmd --version 11.1.0.9782 --tool-path .scratch/tools`.
- Read game assemblies from their existing installation or reference location. Do not copy binaries into decompilation output or modify the game folder.
- Store output under `.scratch/decompiled/<adapter-id>/<game-build-id>/<assembly-name>/`. Use the platform build ID when available; otherwise use a sanitized product version and short assembly fingerprint. `.scratch/` is ignored by Git.
- Prefer listing types or inspecting only the relevant type/member/IL. Use `ilspycmd -p -o <output-directory> <assembly>` for whole-assembly output only when it materially helps the investigation.
- Put an `inspection.json` beside output with adapter/game identity, product version, platform build ID when known, each input assembly's SHA-256 and MVID, ILSpy version, exact command/mode, and output path. Record unknown provenance as unknown; do not infer it from a directory name.
- Check the command exit code and inspect output before making claims. Record durable findings in maintained adapter implementation or compatibility documentation. Do not commit raw decompiled source, local tools, game binaries, or generated reference assemblies.

Keep compiler reference preparation (such as `scripts/kontrol_adapters.py sync-se2`) separate from decompilation. Reference DLLs belong only in their documented ignored `references/` directory and must never be packaged.

## Preserve adapter contracts

- Treat published adapter IDs, input IDs, and schema indices as stable. Append new inputs; do not reorder or reuse indices.
- Treat input-schema version as an IPC compatibility contract, not a feature counter. Adding controls in already-reserved analog slots or action bits does not change the binary frame layout and keeps the existing schema version. Bump only when the frame layout, field meaning/order/capacity, or serialization contract changes; update all producers and consumers together and test that their versions agree.
- Keep the SDK API and IPC contract under the single SDK version defined by the repository.
- Keep adapters independent from WPF and host-owned device polling. Cross process/thread boundaries only through SDK contracts and immutable values.
- Keep normal adapter diagnostics on host IPC. Do not add normal game-side disk logging; keep any debug fallback explicit and opt-in.
- Never commit proprietary game assemblies, local references, build output, logs, dumps, or generated packages.

### Pulsar Legacy payload revisioning

For adapters shipping a Pulsar Legacy plugin DLL, keep `AdapterAssemblyVersion` as the four-part `x.y.z.w` version Pulsar reads. Align the first three numeric segments with the adapter release base and increment the fourth exactly once for each change batch affecting the adapter or shipped payload, including descriptor, dependency, or behavior changes. Update it in `AdapterVersion.props` before building and inspect the produced assembly/file version. This DLL revision does not bump adapter SemVer or consume a prerelease version; follow `$kontrol-adapter-release` for SemVer.

## Confirm compatibility claims

Before changing compatibility metadata to claim a named or currently installed game build is tested, ask whether the user personally completed the manual in-game checklist for that exact build. Do not infer a manual pass from automated tests, assembly inspection, a running process, or validation of another build.

After user confirmation, identify the exact product version, platform build ID, and relevant assembly SHA-256/MVID using the local sync and inspection workflow. Add an append-only record under `src/Adapters/<AdapterFolder>/compatibility/game-builds/`; when multiple adapter versions target one game build, use `<game-product-version>-adapter-<adapter-version>.json`. Record the validation date, automated result, manual checklist result, and user's confirmation in the owning issue, then update the README's compatibility history. Do not bump an unchanged adapter, edit an immutable release, rebuild, or publish solely to add compatibility metadata. Run the adapter/repository consistency validation and report its exact result. Without confirmation, leave the manual result pending and do not make a tested claim.

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
