# VTOL VR Adapter

This adapter is an initial implementation for VTOL VR build 25514272. It publishes stable Kontrol inputs for pitch, roll, yaw, throttle, tilt, and virtual brakes. The game-side plugin uses the VTOL VR Mod Loader `VtolMod` lifecycle and the game's `VehicleInputManager`/`FlightControlComponent` route. No manual in-game compatibility result is claimed.

## Requirements

- VTOL VR and the separately installed Steam version of VTOL VR Mod Loader. Kontrol detects its managed API in either the game's integrated loader folder or the Mod Loader Steam app installed alongside VTOL VR in the same Steam library.
- Build references from the matching game and Mod Loader installation for the separate `net472` mod project. Pass `--game-directory <VTOL VR folder>` when packaging and set `VTOL_VR_MODLOADER_DIR` to the separate Mod Loader Steam app folder when it is not integrated under the game folder; references are read only and never copied into the package.
- Kontrol deploys the mod files and opens VTOL VR Mod Loader through Steam. Select **Play** in Mod Loader to start the game; `item.json` configures the Kontrol mod to load on start.

The host entry assembly targets `net9.0`; the in-game Mod Loader payload targets `net472`. The package carries the host's .NET 9 SDK plus a separately named .NET Standard SDK assembly that the installer places beside the game plugin as `Kontrol.Sdk.dll`. The package build cannot be completed without the local loader/game reference assemblies. Build output and local references must remain outside tracked files.

After installing Mod Loader, create a local test package with `python scripts/kontrol_adapters.py pack --adapter vtol-vr --version 0.1.0 --game-directory <VTOL VR folder> --output .scratch/packages/kontrol-adapter-vtol-vr-0.1.0.zip`. Import the package into Kontrol, select the VTOL VR installation, and use Kontrol's deployment and launch actions. Deployment copies only the adapter-owned mod files to `@Mod Loader\\Mods\\Kontrol.VtolAdapter`; launch opens the Mod Loader through Steam. Kontrol checks for `ModLoader.Framework.dll` in either the game's integrated loader folder or the adjacent Steam Mod Loader app folder, then deploys under the game's `@Mod Loader\\Mods` directory. The pack command only writes the selected output file and repository-local build output; the mod project's post-build targets do not copy files to the game.

## Inputs

| Input | Game route |
| --- | --- |
| Pitch, roll, yaw | `VehicleInputManager`'s `pyrOutputs` dispatch to `FlightControlComponent.SetPitchYawRoll(Vector3)` (vector order is pitch, yaw, roll). |
| Throttle | Harmony postfix on `VRThrottle.Update()` calls `RemoteSetThrottle(float)` after native processing, preserving the game's throttle event path. |
| Tilt | `TiltController.PadInputScaled(Vector3)`; the game retains its battery and airspeed guards. |
| Brakes | `VehicleInputManager.SetVirtualBrakes(float)`; the game applies it to wheel control outputs. |

Throttle follows the mapped axis position by default. Enable **Incremental Throttle** in adapter settings to make positive axis deflection increase the current throttle and negative deflection decrease it. Centering the axis holds the current throttle. **Throttle Change Rate** sets the full-deflection change per second; partial deflection scales proportionally.

After Kontrol opens Mod Loader, select its Play button to start the game with the mod. Axis polarity and in-game behavior still require manual verification on a Mod Loader-enabled build. Throttle and tilt controls are only effective for active vehicle components.

## Inspected build and manual validation

- Steam AppID: `667970`; inspected build ID: `25514272`.
- Product version: unknown. `Assembly-CSharp.dll` SHA-256: `0342964C369E38B62B86574B99FD2395CB2DF7E0B9C514799739A8103864C222`; MVID: `4d230274-f8a6-4822-b8d5-412308d3413c`.
- Evidence is in ignored `.scratch/decompiled/vtol-vr/25514272/Assembly-CSharp/inspection.json`. Inspection is not a manual compatibility pass.
- Manual testing remains pending: verify Mod Loader discovery/unload; pitch/roll/yaw direction, range, and neutral behavior; throttle idle-to-full and event effects; tilt limits, battery and airspeed guards; brakes; disabled-input pass-through; scene changes; and conflicts with native controls.
