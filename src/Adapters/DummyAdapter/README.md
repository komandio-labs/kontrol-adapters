# Dummy Adapter

The Dummy Adapter is a development-only Kontrol integration for the local
`Kontrol.Sandbox.Game` project. It demonstrates adapter metadata, input schema,
host IPC, telemetry publication, diagnostics, and CoreCLR process injection
without requiring a commercial game installation.

## Purpose

Use it to validate Kontrol host behavior and to develop SDK features before
working against a game-specific adapter. It is not intended for end users or as
a template for bypassing a target game's supported integration mechanisms.

## Local development

Build the adapter repository, then select **Kontrol Sandbox** in the Kontrol
application. The adapter launches the sandbox using the standard CoreCLR
bootstrapper and exchanges input and telemetry through local IPC channels.

```powershell
dotnet build Kontrol.Adapters.slnx
dotnet test Kontrol.Adapters.slnx
```

## Schema and deployment

The current schema is version `1` and includes analog movement/rotation inputs
plus momentary, toggle, and trigger actions. It supports only the
`ProcessInjection` deployment method and changes no target files.

## Maintenance

Keep schema indices stable and append new inputs only. Update this README and
[CHANGELOG.md](CHANGELOG.md) when IPC behavior or the sandbox contract changes.

The adapter release manifest is [adapter.manifest.json](adapter.manifest.json).
Run `python ./scripts/kontrol_adapters.py test --adapter dummyadapter` from the repository
root before packaging it locally.
