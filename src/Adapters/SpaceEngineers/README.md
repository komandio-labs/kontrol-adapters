# Space Engineers adapter

Initial API-first adapter for the Space Engineers client. It uses Pulsar Legacy's public `VRage.Plugins.IPlugin` contract and `IMyControllableEntity`; it does not use Harmony or modify game assemblies.

| Metadata | Value |
| --- | --- |
| Adapter version | `1.0.0` |
| Target game build | `steam-build-24675677` |
| Kontrol discovery entry (`pluginDll`) | `Kontrol.Adapters.SpaceEngineers.dll` (`net9.0`) |
| Pulsar Legacy payload | `Kontrol.Adapters.SpaceEngineers.Plugin.dll` (`net48`) |
| Steam application ID | `244850` |
| Input channel | `Local\Kontrol_Input_space-engineers` |

## Supported controls

- Pitch, roll, yaw, forward/reverse, strafe, and lift.
- Dampeners, lights, landing gear, and handbrake as edge-triggered actions.

The plugin releases injected movement when Kontrol input is disabled, when no locally controlled entity is available, or when Pulsar unloads it. Axis direction must be checked with the generated local manual checklist before this build is considered validated.

## Adapter-owned deployment plan

The `BinPluginsFolder` plan is resolved from the selected game directory and the
Pulsar root. Its Kontrol entry assembly is the packaged `.NET 9`
`Kontrol.Adapters.SpaceEngineers.dll`; its only owned deployment file is the
separate `.NET Framework 4.8` payload
`Kontrol.Adapters.SpaceEngineers.Plugin.dll`.

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
3. Deploy from Kontrol. The adapter uses the net9 discovery entry assembly for Kontrol, but copies the separate net48 `Kontrol.Adapters.SpaceEngineers.Plugin.dll` payload and its `Kontrol.Adapters.SpaceEngineers.Plugin.xml` descriptor to `%APPDATA%\Pulsar\Legacy\Local` (or the root configured through `KONTROL_PULSAR_DIRECTORY`). The descriptor supplies the friendly name, description, and clickable documentation link shown by Pulsar.
4. Enable the plugin in the active Pulsar Legacy profile, then use Kontrol's Launch action.
5. Complete the ignored `references/<build>/manual-checklist.md` created by the adapter test command.

Pulsar remains responsible for the profile that enables the plugin. Kontrol neither changes Space Engineers files nor modifies Steam launch options.
