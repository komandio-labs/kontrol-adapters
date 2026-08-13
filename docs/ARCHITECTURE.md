# Architecture

Kontrol separates the Windows host runtime from game-specific adapter code.

| Component | Ownership |
| --- | --- |
| Kontrol host | Input-device handles, immutable mapping snapshots, adapter IPC channel reads/writes, WPF UI, logs, and runtime-worker lifetime. |
| Adapter SDK | Stable adapter metadata, input schema, shared IPC structures, diagnostics, and telemetry contracts. |
| Game adapter | Translation from `InputFrame` to the target game's input/runtime model and publication of game-specific telemetry or diagnostics. |

`InputFrame` schema order is part of the IPC contract. Preserve existing indices, append new inputs, increment the schema version, and add tests for frame behavior. Do not read host UI state directly from adapter code.

An adapter that hooks private game APIs must keep a game-version compatibility record and validate both symbol shape and behavior after each game update.
