# Space Engineers adapter

Joystick, HOTAS, HOSAS, controller, and button-box adapter for the Space Engineers client. It uses Pulsar Legacy's public `VRage.Plugins.IPlugin` contract and narrowly scoped Harmony patches at SE1's final ship-control and third-person zoom commits; it does not modify files in the game installation.

| Metadata | Value |
| --- | --- |
| Adapter version | `1.0.0-beta.1` |
| Target game build | `steam-build-24675677` |
| Kontrol discovery entry (`pluginDll`) | `Kontrol.Adapters.SpaceEngineers.dll` (`net9.0`) |
| Pulsar Legacy payload | `Kontrol.Adapters.SpaceEngineers.Plugin.dll` + `0Harmony.dll` (`net48`) |
| Steam application ID | `244850` |
| Input channel | `Local\Kontrol_Input_space-engineers` |

## Supported controls

- Flight: pitch, roll, yaw, forward/backward, strafe left/right, and up/down.
- Weapons &amp; tools: Use tool / Fire weapon and Secondary mode.
- Vehicle controls: Use / Interact, inertia dampeners, broadcasting, lights, Park, handbrake, and local or connected-grid power.
- Camera: first/third person, held or toggled look-around, camera axes, and smooth analog third-person zoom.
- Interface and communication: HUD, Terminal / Inventory, Inventory, Chat screen, Voice Chat, and toolbar slots 1–0.

The plugin merges Kontrol into SE1's native movement arguments immediately before the game consumes them, so keyboard/mouse and joystick input coexist. Look-around reuses the flight pitch/yaw axes by default, with optional Look Around Horizontal and Look Around Vertical overrides; its realtime Camera Look Sensitivity setting defaults to 2.0×. While look-around is active, third-person zoom reuses forward/backward thrust by default, with an optional Look Around Zoom override. The plugin releases injected movement and held fire when Kontrol input is disabled, when no locally controlled entity is available, or when Pulsar unloads it. Axis direction must be checked with the generated local manual checklist before this build is considered validated.

For the SE1 input-frame layout, final flight-control merge, native third-person zoom interception, camera-look routing, diagnostics, and game-update checks, see [SE1 implementation notes](SE1_IMPLEMENTATION.md).

## Adapter-owned deployment plan

The `BinPluginsFolder` plan is resolved from the selected game directory and the
Pulsar root. Its Kontrol entry assembly is the packaged `.NET 9`
`Kontrol.Adapters.SpaceEngineers.dll`; its only owned deployment file is the
separate `.NET Framework 4.8` payload
`Kontrol.Adapters.SpaceEngineers.Plugin.dll` and its adapter-owned `0Harmony.dll` runtime.

Kontrol writes the payload and its Pulsar descriptor to the resolved Pulsar Legacy target
`<Pulsar root>\Legacy\Local`, launches `<Pulsar root>\Legacy.exe` with
`<Space Engineers root>\Bin64\SpaceEngineers.exe`, and does not write Space
Engineers files or Steam launch settings. The active Pulsar Legacy profile must
still be enabled manually. Uninstall removes only the adapter-owned payload and
descriptor;
Pulsar Legacy itself and the game installation remain unchanged. Shortcuts are
not supported for this adapter.

## Local Pulsar test

1. Install Pulsar separately and choose its **Legacy** runtime for Space Engineers.
2. Sideload the local adapter ZIP into Kontrol, then select **Plugin folder** deployment for Space Engineers.
3. Deploy from Kontrol. The adapter uses the net9 discovery entry assembly for Kontrol, but copies the separate net48 `Kontrol.Adapters.SpaceEngineers.Plugin.dll` payload, `0Harmony.dll`, and its `Kontrol.Adapters.SpaceEngineers.Plugin.xml` descriptor to `%APPDATA%\Pulsar\Legacy\Local` (or the root configured through `KONTROL_PULSAR_DIRECTORY`). The descriptor supplies the friendly name, description, and clickable documentation link shown by Pulsar.
4. Enable the plugin in the active Pulsar Legacy profile, then use Kontrol's Launch action.
5. Complete the ignored `references/<build>/manual-checklist.md` created by the adapter test command.

Pulsar remains responsible for the profile that enables the plugin. Kontrol neither changes Space Engineers files nor modifies Steam launch options.
