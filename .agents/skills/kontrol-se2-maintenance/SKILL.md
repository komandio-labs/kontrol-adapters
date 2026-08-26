---
name: kontrol-se2-maintenance
description: Diagnose, implement, test, and maintain the Kontrol Space Engineers 2 adapter. Use for SE2 process injection or plugin loading, Harmony patches, cockpit axes and actions, camera or weapons, heartbeat, telemetry, SE2 logs, private game API changes, local game references, fingerprints, and tested-game compatibility records. Do not use for unrelated adapters or GitHub publication.
---

# Kontrol SE2 Maintenance

## Establish the validated baseline

1. Locate the repository root and read applicable `AGENTS.md` files.
2. Read `src/Adapters/SpaceEngineers2/README.md`, its changelog, manifest, compatibility records, and current adapter source/tests.
3. Treat the README and code as the authoritative current runtime map. Use [references/runtime-map.md](references/runtime-map.md) only as a diagnostic routing checklist.
4. Inspect current logs and working-tree changes before changing code.

## Diagnose by boundary

- Loading failure: trace Steam launch, target process discovery, managed entry point, `SpaceEngineers2AdapterRuntime`, Harmony installation, and heartbeat.
- Disconnection: distinguish adapter runtime/heartbeat failure from host-generation or IPC-channel failure.
- Axis failure: trace normalized `InputFrame` values into cockpit translation, rotation, and the final SE2 control commit.
- Stuck movement: verify a neutral frame reaches SE2 and clears retained control state.
- Action failure: verify schema bit, trigger-versus-momentary semantics, rising-edge handling, active game object, and native SE2 method invocation.
- Native input failure: verify Kontrol merges with and restores SE2 keyboard/mouse state instead of replacing it.
- Game-update failure: synchronize references, compare exact relevant fingerprints/MVIDs, then inspect symbol and signature changes.

Do not guess private APIs from method names alone. Inspect the installed assemblies and existing tests, and add opt-in rate-limited debug evidence when runtime behavior remains ambiguous.

## Investigate a reported regression before changing code

Start with evidence, not a presumed game-API break. Read [references/regression-triage.md](references/regression-triage.md) and record the following in the owning GitHub issue:

1. Installed SE2 product version, Steam build ID, a sanitized install-location category (for example, Steam library or custom library), and hashes/MVIDs of every manifest-relevant assembly. Never record an absolute install path, username, home directory, hostname, or other developer-machine detail.
2. Adapter manifest target version, deployed adapter/SDK/Harmony file presence, versions, timestamps, and SHA-256 equality with the active Kontrol adapter package.
3. The selected deployment method and the evidence level for launch: source-level expected arguments, an observed Kontrol launch log/command line, or an in-game adapter load log. Never describe one level as another.
4. The first adapter and host error with timestamp and stack trace. Search both current and prior session logs; a successful Harmony message proves assembly load/patch registration only, not working IPC, cockpit controls, or telemetry.

Treat the boundaries independently in this order: deployment and launch, in-game adapter startup/Harmony, status IPC, input/telemetry IPC, then cockpit behavior. A host-side `RuntimeWorker` exception after successful Harmony registration is a host/SDK IPC regression until the raw payload proves otherwise; do not attribute it to the SE2 update.

For status or heartbeat failures, inspect the raw shared-memory JSON and both producer and consumer assembly versions. Check enum encoding, field names, optional fields, and serializer options. Add or update a compatibility test that round-trips the exact payload across the adapter's packaged SDK and the host's actual SDK dependency; matching C# type names alone do not prove wire compatibility.

When logs do not identify the selected method and sanitized launch arguments, add one host-side information-level diagnostic at the launch boundary. Do not add broad per-frame logging or claim a Kontrol-initiated launch merely because the adapter fallback log shows it was loaded.

## Live validation boundaries

- WPF launch or UI inspection must use the managed `wpf-inspector` session described by the repository `AGENTS.md`; end the session after validation.
- Obtain explicit user confirmation before point-clicking Launch. Keep Kontrol and SE2 in the foreground interactive desktop session.
- During a live run, capture the Kontrol launch evidence, target process command line when available, adapter startup log, status payload/result, and any first runtime error. State precisely if the game reaches only the load/patch stage rather than the cockpit manual checklist.

## Implement safely

- Keep the two supported loading entry points thin and keep process-wide ownership in `SpaceEngineers2AdapterRuntime`.
- Make Harmony patches idempotent and dispose runtime resources safely.
- Execute SE2 game-state changes on the game path already used by the relevant feature.
- Preserve native mouse and keyboard behavior while Kontrol Input Control is enabled or disabled.
- Keep schema indices append-only and update the README's mapping/runtime documentation with every hook or behavior change.
- Never commit files from `references/` or any installed SE2 assembly.

## Repository privacy and path hygiene

- Never add developer names, personal email addresses, usernames, home directories, hostnames, machine identifiers, absolute local paths, Steam installation paths, or AppData paths to tracked files, documentation, examples, tests, logs, compatibility evidence, generated metadata, or issue bodies.
- Use generic placeholders such as `<SE2 installation>`, command-line parameters, environment variables, runtime discovery, temporary test directories, and relative repository paths instead of machine-specific defaults.
- Redact personal and machine-specific values from logs, stack traces, command lines, screenshots, and diagnostic evidence before recording or sharing them.
- Before committing, search the complete diff and tracked-file set for personal identifiers and machine-specific path patterns. Organization and product metadata such as `Komandio Labs` is allowed.

## Validate locally

Inspect whether `Kontrol.UI` or `SpaceEngineers2` is running before building normal outputs. Ask the user to close locking processes; never redirect outputs as a workaround.

Run:

```text
python scripts/kontrol_adapters.py test --adapter spaceengineers2
```

This must discover or synchronize the local game references, record inspection evidence, validate metadata, build the adapter, and run SE2 tests. Complete the generated manual checklist before changing a compatibility record to `tested`.

Build and test every changed project. Report failures immediately. After validation, check Git status for proprietary or generated files and update README/changelog/compatibility evidence as appropriate. Do not publish; use `$kontrol-adapter-release` for release work.

NEVER copy, mutate, or deploy files to external directories (such as `%LOCALAPPDATA%`, `%APPDATA%`, or Steam game folders) unless specifically requested by the user. Keep all build outputs strictly within the workspace.
