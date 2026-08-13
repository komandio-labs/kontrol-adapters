# Kontrol Adapters

Kontrol is a Windows application for playing supported PC games with additional
physical input devices: joysticks, throttles, button boxes, wheels, pedals, and
custom controllers. It lets a player configure those devices alongside the
usual keyboard, mouse, and game controller.

This repository contains the game-specific adapters and public SDK used by the
[Kontrol app](https://github.com/komandio-labs/kontrol-app). The app discovers
devices, lets the player create mappings and profiles, and manages the game
session. An adapter makes that input meaningful to one game: it receives
normalized input from the Kontrol host over local IPC, translates it to that
game's runtime input model, and can return telemetry, status, and diagnostics.

Where a game adapter supports it, this is native game control rather than a
simple keyboard-key remap. The adapter speaks to the game's own input model, so
physical controls can be represented as game actions with game-specific
semantics.

## How it fits together

1. A player connects one or more physical input devices to their PC.
2. The Kontrol app reads the devices and applies the player's mappings and
   active profile.
3. The host sends a normalized input frame to the active game adapter.
4. The adapter translates that frame into the supported game's native runtime
   controls and reports useful state back to Kontrol.

The first integration is the experimental [Space Engineers 2 adapter](src/Adapters/SpaceEngineers2/README.md).
Each adapter documents its own supported game versions, mappings, installation,
and limitations.

## Included projects

| Area | Purpose |
| --- | --- |
| `src/Kontrol.Sdk` | Public adapter contracts, IPC structures, diagnostics, and metadata attributes. |
| `src/Adapters/SpaceEngineers2` | Experimental Space Engineers 2 integration, compatibility guide, and tests. |
| `src/Adapters/DummyAdapter` | Minimal adapter, documentation, and tests for development and IPC validation. |
| `src/Kontrol.Sandbox.Game` | Local sandbox game used while developing the SDK. |

## Quick start

Install Python 3 and the .NET 9 SDK. The repository's developer and
release commands are provided by `scripts/kontrol_adapters.py`.

```powershell
git clone https://github.com/komandio-labs/kontrol-adapters.git
Set-Location kontrol-adapters
python ./scripts/kontrol_adapters.py sync-se2
dotnet build Kontrol.Adapters.slnx
dotnet test Kontrol.Adapters.slnx
```

The SE2 preparation script copies game-owned reference assemblies from a local Steam installation into an ignored versioned directory. Game binaries are never committed or redistributed by this repository.

Validate repository metadata and run a targeted adapter check with the local-first tooling:

```powershell
python ./scripts/kontrol_adapters.py validate
python ./scripts/kontrol_adapters.py test --adapter dummyadapter
python ./scripts/kontrol_adapters.py test --adapter spaceengineers2
```

Create an unpublished local package only for an explicitly selected adapter:

```powershell
python ./scripts/kontrol_adapters.py pack --adapter dummyadapter --version 1.0.0
```

Packages are written to ignored `artifacts/` and are never uploaded by these
commands. The package tool includes the Apache-2.0 license automatically and
enforces each adapter's declared runtime-file allowlist.

For releases, use the single [publishing procedure](docs/PUBLISHING.md). It
explains how a locally verified ZIP becomes a signed public Pages package,
descriptor, and catalog entry.

## Architecture

The Kontrol host owns device polling, configuration, UI, IPC reads/writes, and the session runtime worker. An adapter owns only its game-specific translation and must treat the host's `InputFrame` as immutable. Adapter-to-host state crosses the boundary through SDK IPC contracts and must never depend on WPF objects.

Read [architecture](docs/ARCHITECTURE.md), [build instructions](docs/BUILDING.md),
the [adapter development guide](docs/DEVELOPING_ADAPTERS.md), the
[versioning policy](docs/VERSIONING.md), and the
[publishing procedure](docs/PUBLISHING.md) before adding or releasing an
integration.

## Compatibility and safety

Adapters that use private game APIs or process injection are inherently version-sensitive. A successful build does not declare a game build supported: validate the target game's symbols and behavior, document the result in that adapter's README, and test in an environment where the game's rules, EULA, and anti-cheat policy permit the integration.

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md), keep game binaries out of commits, and add or update tests when changing schema, IPC, or game translation behavior.

For non-security questions and bugs, use GitHub Issues. For security reports,
follow [SECURITY.md](SECURITY.md). Community expectations are in the
[Code of Conduct](CODE_OF_CONDUCT.md).

## License

Copyright 2026 Komandio Labs. Licensed under the [Apache License 2.0](LICENSE).
