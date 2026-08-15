# Space Engineers 2 adapter

> **Status:** experimental integration against Space Engineers 2 private runtime APIs. Adapter `0.1.0-beta.2` is a tested beta against SE2 `2.3.0.2798` (Steam build `24225481`). Revalidate after every game update.

## Disclaimer

This is an unofficial Kontrol integration. It is not affiliated with, endorsed by,
or supported by Keen Software House. Space Engineers and Space Engineers 2 are
trademarks of their respective owners. This repository does not redistribute any
SE2 game files.

The adapter injects into private game implementation details. Use it only where
game rules, the EULA, and any anti-cheat policy permit it. Validate updates in a
safe environment before relying on it in normal play.

## Loading methods

The default deployment is the SE2 native plugin loader. It deploys the adapter,
`Kontrol.Sdk.dll`, and Harmony beside the game, then starts SE2 through the
installed Steam client with the game's `-plugins:<absolute-adapter-path>`
argument.

Process Injection remains available as an alternate deployment method. The
Kontrol host launches SE2 with the recommended Steam
URL `steam://run/1133870/`, waits for Steam's actual `SpaceEngineers2.exe`
process, and attaches its source-built x64 native bootstrap. The native
bootstrap binds to SE2's already-running CoreCLR and calls:

```text
Kontrol.Adapters.SpaceEngineers2.SpaceEngineers2StartupHook.Initialize()
```

The adapter repository declares that managed entry point through
`IAdapterInstaller.GetProcessInjectionEntryPoint()`. Native process discovery,
remote loading, and bootstrap deployment belong to the Kontrol host, not this
public adapter package. No SE2 installation file is changed by Process
Injection.

Both methods activate the same process-wide
`SpaceEngineers2AdapterRuntime`; its start guard prevents duplicate runtimes.

## Local development setup

The adapter is compiled against locally installed, game-owned reference
assemblies. They are deliberately ignored by Git and are never published.

1. Install the supported Space Engineers 2 build through Steam.
2. From the repository root, run `python ./scripts/kontrol_adapters.py sync-se2`.
   It discovers a local Steam installation, or accepts
   `--game-directory <SE2 installation>`.
3. The script copies the required assemblies to `references/<detected version>`
   and selects that version for local builds.
4. Build and run the adapter tests from the repository root.

If the detected game version differs from the validated baseline, the project
can still compile, but it is not supported until the compatibility checks in
this document have been completed and recorded.

## Compatibility and maintenance contract

This document is the maintenance contract between the Kontrol input schema and
the private Space Engineers 2 (SE2) APIs used by the adapter. Update it whenever
SE2 changes, whenever a game reference assembly is replaced, or whenever an
adapter input is added or translated differently.

The code remains the source of truth. This document records why the current
symbols are used, their expected signatures and behavior, and what must be
verified before declaring a newer SE2 build compatible.

## Validated compatibility baseline

| Item | Validated value |
| --- | --- |
| Historical evidence date | 2026-07-30 |
| SE2 file/product version | `2.3.0.2798` |
| Steam application ID | `1133870` |
| Steam build ID | `24225481` |
| Game binary directory | `<SE2 installation>\Game2` |
| Adapter version | `0.1.0-beta.2` (tested beta) |
| SDK contract version | `1.0.0` |
| Adapter input schema | Version `5` |
| Adapter target framework | `net9.0` |
| Harmony package | `Lib.Harmony 2.4.2` |

The locally prepared reference assemblies in `references/2.3.0.2798` matched
the installed game assemblies during the recorded validation. The directory is
intentionally ignored and is never committed. Re-run
`python ./scripts/kontrol_adapters.py test --adapter spaceengineers2` and the
manual checklist before declaring any later game build tested.

### Reference assembly fingerprints

| Assembly | File version | SHA-256 |
| --- | --- | --- |
| `Game2.Client.dll` | `2.3.0.2798` | `296360843CD3ED707D5572124E93FB611F8869B4EA07B2953BB3D2116566E12E` |
| `Game2.Simulation.dll` | `2.3.0.2798` | `C841BD8F931DC8E9FFEA7F6BB079E47CADF27CA909485C221DF16D9684D181E9` |
| `VRage.Core.dll` | `2.3.0.2798` | `4B14831F57DABD76F1DC6A6899B2D1CC310ED62830F83D4F9DABE85E6DD51683` |
| `VRage.Core.Game.dll` | `2.3.0.2798` | Captured locally by `kontrol_adapters.py sync-se2`; update this fingerprint when revalidating the baseline. |
| `VRage.DCS.dll` | `2.3.0.2798` | `1C71E3D7D6A0EF3459883DA0B97E42CA9E69B32CA9D22AD397C564C22BD93116` |
| `VRage.Library.dll` | `2.3.0.2798` | `02A6393FC3B0D4A57C1EA11B9C448A8EC21758D9E6F0B95F8A4B29F7B229B077` |
| `VRage.Physics.dll` | `2.3.0.2798` | `9ADBEF930CB43E6FE6F0A31B4B731C225B0F6BDBBFFC520CDB7EB2957125C22B` |
| `VRage.Input.dll` | `2.3.0.2798` | Captured locally by `kontrol_adapters.py sync-se2`; update this fingerprint when revalidating the baseline. |

Key module version IDs (MVIDs):

| Assembly | MVID |
| --- | --- |
| `Game2.Client.dll` | `3e9ef3aa-d7a2-41ad-a627-b068f054b48b` |
| `Game2.Simulation.dll` | `65b73207-e158-40ad-bc09-8994d0ca67a6` |
| `VRage.Library.dll` | `b072f1e4-5ad2-41c2-9798-ef67f96cbede` |

A different hash or MVID does not automatically mean the adapter is broken. It
means the private symbols and their IL behavior below must be revalidated.

## Kontrol input schema and IPC layout

The host samples physical devices at 60 Hz and writes an `InputFrame` only when
its effective content changes. Schema order is also the IPC layout: analog inputs
occupy `AnalogValues` in analog declaration order, while discrete inputs use the
overall schema index as their bit number.

Do not reorder or reuse existing indices. Append new inputs and increment the
schema version so saved user mappings continue to refer to the same controls.

| Schema index | IPC location | ID | Type | Default shaping | Adapter-to-SE2 translation |
| ---: | --- | --- | --- | --- | --- |
| 0 | `AnalogValues[0]` | `flight.pitch` | Analog | Deadzone `.10`, exponent `1.0`, invertible | Positive -> `lookDown`; negative -> `lookUp` |
| 1 | `AnalogValues[1]` | `flight.roll` | Analog | Deadzone `.10`, exponent `1.0`, invertible | Positive -> `MovementInputs.RollRight`; negative -> `RollLeft` |
| 2 | `AnalogValues[2]` | `flight.yaw` | Analog | Deadzone `.08`, exponent `1.0`, invertible | Positive -> `lookRight`; negative -> `lookLeft` |
| 3 | `AnalogValues[3]` | `movement.forward` | Analog | Deadzone `.08`, exponent `1.5`, invertible | Positive -> `Forward`; negative -> `Backward` |
| 4 | `AnalogValues[4]` | `movement.strafe` | Analog | Deadzone `.08`, exponent `1.5`, invertible | Positive -> `Right`; negative -> `Left` |
| 5 | `AnalogValues[5]` | `movement.lift` | Analog | Deadzone `.05`, exponent `1.0`, invertible | Positive -> `Up`; negative -> `Down` |
| 6 | `TriggeredActions` bit 6 | `systems.dampeners` | Trigger | Rising edge, host-latched for 150 ms | `ToggleDampeners(true, Start)` |
| 7 | `TriggeredActions` bit 7 | `systems.lights` | Trigger | Rising edge, host-latched for 150 ms | `ToggleLights(true, Start)` |
| 8 | `TriggeredActions` bit 8 | `systems.parking_brakes` | Trigger | Rising edge, host-latched for 150 ms | `ToggleParkingBrakes(true, Start)` |
| 9 | `TriggeredActions` bit 9 | `systems.power` | Trigger | Rising edge, host-latched for 150 ms | `TogglePower(true, Start)` |
| 10 | `TriggeredActions` bit 10 | `systems.exit_grid` | Trigger | Rising edge, host-latched for 150 ms | `InteractionActivated(true, Start)`, which calls `Unpossess()` |
| 11 | `DiscreteStates` bit 11 | `weapons.fire_primary` | Momentary | Held while the physical button is held | Active weapon/tool primary handler, with press and release |
| 12 | `DiscreteStates` bit 12 | `weapons.reload` | Momentary | Held while the physical button is held | Active block-weapon secondary/right-mouse handler, with press and release |
| 13 | `TriggeredActions` bit 13 | `camera.mode_switch` | Trigger | Rising edge, host-latched for 150 ms | `CameraSystemComponent.ToggleCameraView()` on the camera update path |

### Host-side analog shaping

Kontrol applies inversion first. For raw value `v`, deadzone `d`, and exponent
`e`, the effective value is:

```text
0                                           when abs(v) <= d
sign(v) * ((abs(v) - d) / (1 - d)) ^ e      otherwise
```

The adapter then rejects non-finite values and clamps the result to `[-1, 1]`.
Consequently, a physical maximum remains exactly `-1` or `+1`. The SE2 adapter
does not apply another response curve.

### Native keyboard and mouse coexistence

Kontrol is merged with SE2's native state; it is not a global input replacement.
For directional movement pairs, the stronger magnitude is retained. Pitch and
yaw are fed through SE2's directional look fields so full joystick travel follows
the same path as a full digital control.

The adapter captures SE2's private native fields before each temporary merge and
restores them after the game update consumes the combined state. Primary fire
and reload are each the logical OR of the corresponding native mouse state and
Kontrol button state. Releasing one source does not release the action while the
other remains pressed.

Mapping capture changes must call `GameStateService.NotifyConfigChanged()` after
saving. The runtime worker reads only immutable binding snapshots; mutating and
saving the WPF-owned configuration dictionary does not update the live runtime
by itself.

When Input Control is disabled, Kontrol contributes no movement or actions. A
held Kontrol fire state is explicitly released, and trigger edge tracking is
reset so the next enabled press can be observed.

## SE2 private integration points

All types in this section are currently in `Game2.Client.dll` unless noted. These
are private implementation details, not a stable SE2 mod API.

### Cockpit movement and rotation

Primary type:

```text
Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent
```

Harmony hooks and direct calls:

| Method | Expected signature | Kontrol use |
| --- | --- | --- |
| `UpdateRotationData` | `void UpdateRotationData(ref AngularReticleLocalData localData, Optional<T> debugSettings)` | Prefix temporarily merges axes; postfix restores native fields |
| `ComputeReticlePositioning` | `void ComputeReticlePositioning()` | Prefix temporarily merges axes; postfix restores native fields |
| `UpdateControlData` | `void UpdateControlData()` | Explicit prefix reads action state; adapter also invokes it to commit each effective cockpit state, including neutral |

The Harmony field injection depends on these private instance fields:

```text
float _pitchAnalog
float _yawAnalog
float _lookUp
float _lookDown
float _lookLeft
float _lookRight
Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs _movementInputs
Keen.Game2.Simulation.WorldObjects.CubeBlocks.CubeBlockComponent _observedBlock
```

`MovementInputs`, from `Game2.Simulation.dll`, must continue to expose these
floating-point members with the same meanings:

```text
Forward, Backward, Right, Left, Up, Down, RollRight, RollLeft
```

The critical behavior to inspect in a new game build is not only whether these
symbols exist. Confirm where `UpdateControlData()` submits `_movementInputs`, and
confirm that `UpdateRotationData`/`ComputeReticlePositioning` still consume the
directional look fields before Kontrol restores the native snapshot.

### Cockpit actions

The following non-public instance methods must have this exact shape:

```text
void InteractionActivated(bool value, Keen.VRage.Input.ControlActivation activation)
void ToggleDampeners(bool value, Keen.VRage.Input.ControlActivation activation)
void ToggleLights(bool value, Keen.VRage.Input.ControlActivation activation)
void ToggleParkingBrakes(bool value, Keen.VRage.Input.ControlActivation activation)
void TogglePower(bool value, Keen.VRage.Input.ControlActivation activation)
```

In the validated build, `ControlActivation.Start` is enum value `0` and `End` is
value `2`. Kontrol creates the enum through the reflected parameter type to avoid
adding a runtime dependency on SE2's input assembly.

The four toggles call the corresponding operation on the observed block's grid.
`InteractionActivated` calls `Unpossess()`. Reinspect these method bodies if a
new version retains the names but changes their semantics.

### Camera Mode Switch

SE2 registers its native Toggle Camera input (currently `V` by default) in:

```text
Keen.Game2.Client.GameSystems.CameraSystems.CameraSystemComponent
    InputContext PrepareInputContext()
```

`PrepareInputContext()` connects `CameraSystemDefinition.ToggleCamera` to this
non-public trigger handler:

```text
void ToggleCameraView()
```

Kontrol patches `CameraSystemComponent.Init()` to retain the active camera
system instance. It consumes schema bit 13 from the same continuous cockpit
input-commit path used for the other semantic actions, then invokes
`ToggleCameraView()` on that cached instance. This is deliberate:
`UpdateCameraControl()` is event-driven and cannot reliably consume an action
which exists only in Kontrol. The implementation still uses SE2's semantic
camera operation rather than synthesizing the `V` key.

When updating SE2 references, verify that `CameraSystemComponent.Init()` still
runs for the active camera system, that `ToggleCameraView()` remains the handler registered for
`CameraSystemDefinition.ToggleCamera`, and that both methods remain
parameterless instance methods.

### Primary fire and reload

Kontrol observes active input handlers through:

```text
Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.InputHandlerBaseComponent
    void Activate()
    void Deactivate()
```

It recognizes and invokes either of these active-handler paths:

```text
Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.BlockTools.BlockToolInputHandlerBaseComponent
    void PrimaryAction(bool value, ControlActivation activation)
    void SecondaryAction(bool value, ControlActivation activation)

Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.AutomatedWeaponInputHandlerComponent
    void Shoot(bool value, ControlActivation activation)
```

For block weapons, `PrimaryAction` currently forwards to
`BlockToolControllableBaseComponent.RequestActivatePrimaryAction(bool)`, while
`SecondaryAction` forwards to `RequestActivateSecondaryAction(bool)`. On
`WeaponBlockToolControllableComponent`, the secondary path calls
`ActivateSecondaryActionImpl(bool)` and raises SE2's internal
`RequestReload(bool)` signal. This is the cockpit weapon action currently bound
to the right mouse button. For an automated weapon, `Shoot` forwards to
`AutomatedWeaponBlockToolControllableComponent.RequestShootTurret(bool)`.

Both the activation lifecycle and the primary/secondary forwarding methods must
be checked after an SE2 update. A renamed method is obvious; a handler that no
longer becomes active while a cockpit weapon is selected is a behavioral
breaking change.

## IPC, telemetry, and diagnostics

The adapter uses the following named shared-memory channels:

```text
Local\Kontrol_Input_SE2
Local\Kontrol_Telemetry_SE2
Local\Kontrol_Logs_SE2
Local\Kontrol_AdapterStatus_SE2
```

`InputFrame` contains schema version, Input Control state, 32 analog slots, a
64-bit held/toggle state field, and a 64-bit trigger field. Changing its layout
requires coordinated SDK, host, adapter, and test changes.

Cockpit telemetry reflects `_observedBlock.Grid.Entity` and currently reads:

- `Keen.VRage.Physics.Data.RigidBodyMassProperties` for ship mass.
- `Keen.VRage.Physics.Data.RigidBodyData` for linear and angular velocity.
- `Keen.Game2.Simulation.WorldObjects.Movement.DampeningData` presence for the
  displayed dampener state.

Telemetry failure should not prevent controls from being applied. If SE2 changes
the observed-block or entity-data model, update telemetry independently from the
input translation.

With adapter debug logging enabled, useful compatibility evidence includes:

```text
Harmony patches successfully registered...
Kontrol input is available while piloting a cockpit.
Received Kontrol input frame: ... actions=0x...
Applied to SE2: ...
Kontrol invoked SE2 cockpit action '...'.
SE2 activated primary-action handler '...'.
Kontrol primary fire changed to True/False through '...'.
Kontrol reload changed to True/False through '...'.
Kontrol invoked SE2 Camera Mode Switch through 'ToggleCameraView'.
```

Normal adapter logs travel over IPC and are persisted by the Kontrol host. The
adapter's fallback `adapter-debug.log` remains opt-in.

## Upgrade procedure for a new SE2 version

1. Record the SE2 file/product version, Steam build ID when available, update
   date, SHA-256 hashes, and MVIDs before changing code.
2. Run `scripts/kontrol_adapters.py sync-se2` and compare the installed
   `<SE2 installation>\Game2` assemblies with the generated
   `references/<version>` directory.
   Never assume an unchanged file version means unchanged IL.
3. Inspect the exact types, methods, parameters, fields, enum values, and method
   bodies listed in this document with Mono.Cecil or a .NET decompiler.
4. Search first for these semantic anchors if a symbol disappeared:
   `CockpitInputHandlerComponent`, `MovementInputs`, `UpdateControlData`,
   `UpdateRotationData`, `ComputeReticlePositioning`, `ToggleDampeners`,
   `ToggleLights`, `ToggleParkingBrakes`, `TogglePower`, `Unpossess`,
   `PrimaryAction`, `SecondaryAction`, `RequestActivatePrimaryAction`,
   `RequestActivateSecondaryAction`, `RequestReload`, `Shoot`,
   `RequestShootTurret`, `CameraSystemComponent`, `UpdateCameraControl`, and
   `ToggleCameraView`.
5. Replace the checked-in reference assemblies only after reviewing the change.
   Keep every reference at the same SE2 version; do not mix game builds.
6. Update the adapter code and this document together. Preserve existing schema
   indices. Append new inputs and bump the schema version only when necessary.
7. Build the full solution and run all tests as required by the repository
   `AGENTS.md`.
8. Run the manual compatibility matrix below with adapter debug logging enabled.
9. Add a row to Compatibility history describing the result and any adaptation.

### Required manual compatibility matrix

- Enter and exit a cockpit; confirm adapter connection and cockpit detection.
- Hold each analog axis in both directions, return it to neutral, and verify the
  ship stops receiving that input.
- Test combined native and Kontrol input for keyboard movement, mouse look, and
  primary fire.
- Trigger dampeners, lights, parking brakes, and power once each; verify one game
  state change per physical press.
- Trigger Exit grid once and verify the player leaves the controlled seat.
- Trigger Camera Mode Switch repeatedly and verify it cycles through the same
  camera modes as SE2's native Toggle Camera binding.
- Press, hold, and release Primary fire with a selected cockpit weapon.
- Press, hold, and release Reload; confirm it matches the selected weapon's
  right-mouse reload behavior.
- Change the selected weapon/tool and repeat Primary fire.
- Disable Input Control while moving and while firing; verify Kontrol releases
  its contribution and native keyboard/mouse remain usable.
- Review adapter session logs for Harmony errors, missing methods, parameter-count
  errors, null references, and repeated trigger invocations.

## Failure symptom guide

| Symptom | Inspect first |
| --- | --- |
| Adapter loads but reports integration failure | Harmony target names/signatures and assembly versions |
| Pitch/yaw do nothing | Directional look fields and rotation/reticle job order |
| Translation or roll does nothing | `MovementInputs` members and final `UpdateControlData` commit path |
| Ship continues moving at neutral | Neutral-frame delivery, directional pair reset, and snapshot restore timing |
| Toggle logs `TargetParameterCountException` | Cockpit action method parameters and `ControlActivation` values |
| Action arrives but game state does not change | Action method IL and observed grid/controllable ownership |
| Primary fire does nothing | Active handler lifecycle and selected block weapon handler type |
| Primary fire remains active | `End` value, release forwarding, and active-handler deactivation |
| Reload does nothing | `SecondaryAction`, `RequestActivateSecondaryAction`, and the selected weapon's `RequestReload` signal |
| Camera Mode Switch does nothing | `CameraSystemComponent.UpdateCameraControl()`, `ToggleCameraView()`, and schema bit 13 edge detection |
| Native mouse/keyboard stops working | Native snapshot restore and OR/maximum merge behavior |
| Controls work but telemetry fails | `_observedBlock`, `Grid.Entity`, and entity data component types |

## Compatibility history

| Date | SE2 version | Schema | Result | Notes |
| --- | --- | ---: | --- | --- |
| 2026-07-30 | `2.3.0.2798` | 3 | Partially compatible | Axes and cockpit toggles worked; newly captured Exit grid and Primary fire bindings were saved but not published to the runtime worker |
| 2026-07-30 | `2.3.0.2798` | 4 | Automated validation passed; manual game validation pending | Live capture now publishes an immutable runtime snapshot; added held Reload through the block weapon secondary/right-mouse path; full solution build and 34 tests passed |
| 2026-07-30 | `2.3.0.2798` | 5 | Automated validation passed; manual game validation pending | Added semantic Camera Mode Switch through `CameraSystemComponent.ToggleCameraView()` on the global camera update path; full solution build and 36 tests passed |
| 2026-07-31 | `2.3.0.2798` | 5 | Manual validation passed | Native Plugin Parameter and Process Injection both loaded, connected, and delivered working controls; full solution build succeeded with 51 automated tests passed |
