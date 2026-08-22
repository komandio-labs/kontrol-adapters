# SE2 regression triage

## Evidence table

| Boundary | Required evidence | Does not prove |
| --- | --- | --- |
| Game update | Steam build ID; executable/assembly versions, SHA-256, MVIDs | Adapter is loaded or compatible |
| Deployment | Installed adapter/SDK/Harmony files and hashes equal the active package | Steam received the plugin parameter |
| Kontrol launch | Host launch log and, when available, target process command line | Harmony or adapter startup succeeded |
| Plugin load | Adapter startup log plus Harmony registration result | Host IPC, inputs, telemetry, or cockpit behavior works |
| Status IPC | Raw status JSON and successful host consumption | Input translation works |
| Controls | Manual cockpit checklist and session logs | Compatibility record may be promoted only after all required checks |

## Locations and high-signal searches

- Host logs: `%LOCALAPPDATA%\\Kontrol\\logs\\Kontrol-*.log`
- Adapter fallback log when debug is enabled: `%LOCALAPPDATA%\\Kontrol\\adapters\\<slug>\\logs\\adapter.log`
- Native plugin deployment: `<SE2 install>\\Game2\\Kontrol.Adapters.SpaceEngineers2.dll`, `Kontrol.Sdk.dll`, and `0Harmony.dll`
- Search host logs for `RuntimeWorker`, `Input runtime worker stopped unexpectedly`, `JsonException`, deployment, launch, and `-plugins`.
- Search adapter logs for runtime location, assembly count, Harmony registration, initialization errors, and first cockpit/input messages.

## Status-contract failure pattern

If the adapter log says Harmony registered but the host worker stops immediately, capture the JSON before editing. Compare its `State` representation (for example `"Active"` versus `1`) with the host deserializer's configured converters and with the SDK assembly actually loaded by the host. Check for package-versus-project-reference drift. Fix the wire contract deliberately and test both the legacy and current payload policy if compatibility is required.

## Reporting template

State: `loaded`, `active`, or `error` only when the appropriate evidence above exists. Report the game build, adapter version, deployment hash result, launch-evidence level, first failure boundary, exact exception, and what remains unvalidated. Keep SE2 private assemblies, full dumps, and user-specific logs out of commits and issue bodies.
