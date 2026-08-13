# SE2 diagnostic routing

Use source search and the adapter README to confirm current names before relying on this map.

| Symptom | Start with |
| --- | --- |
| Adapter never connects | Loading entry point, shared runtime start guard, heartbeat channel |
| Connects then disconnects | Target process lifetime, runtime exception, heartbeat disposal, host generation |
| Pitch/yaw/roll failure | `CockpitInputPatch`, rotation fields, final control submission |
| Translation sticks after release | neutral `InputFrame`, translation merge, explicit control commit |
| Vehicle action failure | schema bit, `TriggeredActions`, edge detection, reflected SE2 handler |
| Fire/reload failure | momentary discrete state, active tool handler, press and release forwarding |
| Camera switch failure | active camera-system capture and native camera toggle invocation |
| Keyboard/mouse blocked | native input snapshot, merge policy, restoration after Kontrol processing |
| Telemetry failure | observed cockpit block, grid entity, SDK telemetry channel |

For a new SE2 build, run the Python SE2 test command first. Treat generated inspection data as a candidate only. Promote a committed compatibility record only after automated tests and the manual in-game checklist pass.
