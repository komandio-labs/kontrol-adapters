# Kontrol Adapters

This repo is the public, open-source extension layer for
[Kontrol](https://www.komandio.com/kontrol). It contains the C# SDK,
game-specific adapters, compatibility metadata, and
developer and publishing tools used to connect physical input hardware to
supported PC games.

An adapter makes hardware input meaningful to one game. It receives normalized
input from the Kontrol host over local IPC, translates it to that game's native
runtime input model, and can return telemetry, status, and diagnostics. The SDK
defines the contracts, IPC structures, settings, and metadata that make this
boundary reliable and extensible.

This repository is intended for developers who want to inspect how an
integration works, build or test an adapter, or contribute support for another
simulation title. Adapter source, manifests, tests, and documentation live
together so each integration can document its own game-version compatibility
and limitations.

Where a game adapter supports it, this is native game control rather than a
simple keyboard-key remap. The adapter speaks to the game's own input model, so
physical controls can be represented as game actions with game-specific
semantics.

## The free Kontrol app

[Kontrol](https://www.komandio.com/kontrol) is a free Windows application
developed by Komandio Labs. It lets supported PC games use additional physical
input devices such as joysticks, throttles, button boxes, wheels, pedals, and
custom controllers alongside a keyboard, mouse, or game controller.

Kontrol discovers connected devices, lets players configure mappings and
profiles, and manages the game session. It is designed for simulation hardware
including full 6-DoF dual-stick (HOSAS) setups, HOTAS controls, throttles, and
rudder pedals. The official no-charge release does not require an account,
product key, license activation, device binding, or payment information.

The app uses the SDK and adapters in this repository to communicate with a
supported game. The host handles device polling, configuration, mappings, and
the session runtime; an adapter handles the game-specific translation. You can
browse signed adapters in the official [Kontrol Adapter Library](https://www.komandio.com/kontrol/adapters.html) or [get Kontrol on itch.io](https://komandio-labs.itch.io/kontrol).

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
dotnet build Kontrol.Adapters.slnx
dotnet test Kontrol.Adapters.slnx
```

These commands cover the repository's SDK, tooling, adapters, and sandbox. Some
adapters require locally installed game references or other prerequisites. Read
an adapter's README before building or validating that integration; the
[Space Engineers 2 adapter README](src/Adapters/SpaceEngineers2/README.md)
documents its local-reference setup, compatibility checks, and game-specific
validation workflow.

Validate repository metadata and run a targeted adapter check with the
local-first tooling:

```powershell
python ./scripts/kontrol_adapters.py validate
python ./scripts/kontrol_adapters.py test --adapter dummyadapter
```

Create an unpublished local package for an explicitly selected adapter:

```powershell
python ./scripts/kontrol_adapters.py pack --adapter <adapter-id> --version <version>
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
