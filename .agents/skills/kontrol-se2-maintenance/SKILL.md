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

## Implement safely

- Keep the two supported loading entry points thin and keep process-wide ownership in `SpaceEngineers2AdapterRuntime`.
- Make Harmony patches idempotent and dispose runtime resources safely.
- Execute SE2 game-state changes on the game path already used by the relevant feature.
- Preserve native mouse and keyboard behavior while Kontrol Input Control is enabled or disabled.
- Keep schema indices append-only and update the README's mapping/runtime documentation with every hook or behavior change.
- Never commit files from `references/` or any installed SE2 assembly.

## Validate locally

Inspect whether `Kontrol.UI` or `SpaceEngineers2` is running before building normal outputs. Ask the user to close locking processes; never redirect outputs as a workaround.

Run:

```text
python scripts/kontrol_adapters.py test --adapter spaceengineers2
```

This must discover or synchronize the local game references, record inspection evidence, validate metadata, build the adapter, and run SE2 tests. Complete the generated manual checklist before changing a compatibility record to `tested`.

Build and test every changed project. Report failures immediately. After validation, check Git status for proprietary or generated files and update README/changelog/compatibility evidence as appropriate. Do not publish; use `$kontrol-adapter-release` for release work.
