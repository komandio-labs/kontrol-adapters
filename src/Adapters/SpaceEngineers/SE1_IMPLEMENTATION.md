# Space Engineers 1 implementation notes

This adapter targets the Space Engineers 1 Steam build recorded as
`steam-build-24675677` (Steam app `244850`). The game-owned reference DLLs are
prepared locally by the sync script and are never committed or packaged.

The adapter has two assemblies with different jobs:

- `Kontrol.Adapters.SpaceEngineers.dll` is the .NET 9 discovery/installer
  assembly loaded by Kontrol.
- `Kontrol.Adapters.SpaceEngineers.Plugin.dll` is the .NET Framework 4.8
  Pulsar Legacy payload loaded inside the game process. It owns the Harmony
  hooks and reads the shared input frame.

## Input-frame contract and host-to-game path

The host polls the device, applies the schema's deadzone/exponent/inversion,
and writes an immutable `InputFrame` to
`Local\\Kontrol_Input_space-engineers`. The plugin reads that frame at its
normal update and again at the final ship-control commit. Analog slots are
append-only and currently mean:

| Slot | Input |
| ---: | --- |
| 0 | `flight.pitch` |
| 1 | `flight.roll` |
| 2 | `flight.yaw` |
| 3 | `movement.forward` |
| 4 | `movement.strafe` |
| 5 | `movement.lift` |
| 6 | `camera.look_horizontal` |
| 7 | `camera.look_vertical` |
| 8 | `camera.zoom` (`Look Around Zoom`) |

The host's discrete state bits carry held and toggled Look Around. This is
why adding the camera controls did not require an IPC schema-version change:
the existing fixed analog capacity and state-bit layout are unchanged.

## Flight controls

`MyShipController.MoveAndRotate` is patched at the final point before SE1
consumes its native keyboard/mouse and joystick arguments. The plugin first
copies the native movement, rotation, and roll values, then merges Kontrol
values with `InputMerge.StrongerAxis`. A neutral Kontrol frame is therefore a
pass-through, and native keyboard/mouse flight remains available while Kontrol
is connected.

The mapping is:

- pitch: slot 0 -> native rotation X;
- yaw: slot 2 -> native rotation Y;
- roll: slot 1 -> native roll;
- strafe/lift: slots 4/5 -> native movement X/Y;
- forward/backward: slot 3 -> native movement Z.

When Look Around is active, pitch and yaw are routed to camera look rather than
being merged as ship rotation. The Kontrol forward value is suppressed from
ship movement for that interval so a shared forward/zoom physical axis cannot
zoom and thrust simultaneously. The native movement argument is retained, so
keyboard/mouse forward input is not suppressed. Strafe, lift, and roll are not
blanket-suppressed.

## Camera look and zoom

Look Around is active when either the hold or toggle state bit is set. Existing
flight pitch/yaw are used as camera axes unless the dedicated horizontal or
vertical camera action is non-zero. Camera rotation is converted to SE1's
mouse-delta-like units using elapsed time and the `Camera Look Sensitivity`
setting (bounded to `0.1..10`, default `2.0`). A centered camera axis does not
call the game camera controller.

SE1's third-person zoom implementation was decompiled from the synced build.
`Sandbox.Engine.Utils.MyThirdPersonSpectator.UpdateZoom`:

1. derives the controlled entity's binding context;
2. gates the whole zoom block on native Look Around or native joystick-last-used;
3. reads `CAMERA_ZOOM_IN` and `CAMERA_ZOOM_OUT` through
   `MyControllerHelper.IsControlAnalog(MyStringId, MyStringId, bool)`; and
4. scales only the length of its private `m_lookAt` vector, preserving its
   direction (subject to the game's distance clamps).

The two calls are directional, non-negative magnitudes. The Harmony transpiler
therefore replaces only the one Look Around gate and the two exact analog
callsites. It requires the observed IL shape (`ldsfld CAMERA_ZOOM_*`,
`ldc.i4.0`, then the three-argument call) and fails closed if the count or shape
changes after a game update.

For an eligible Kontrol frame, the gate resolves to native Look Around OR the
adapter's Look Around state. The wrapper calls the native analog function first
and merges the Kontrol directional magnitude afterward. `camera.zoom` is used
when active; otherwise slot 3 (`movement.forward`) is the compatibility
fallback. Positive input is zoom in and negative input is zoom out. The
resolved value is clamped to native `0..1`; at the recommended `2x` sensitivity
the Kontrol magnitude is identity, while higher settings accelerate partial
input without exceeding native full scale.

The plugin does not fake Alt or mouse-wheel events and does not mutate SE1's
camera transform or distance fields. A second `UpdateZoom` can be reached when
`MyCockpit.Rotate` delegates to `MyThirdPersonSpectator.Rotate`; the adapter's
camera-rotation helper suppresses only that nested call, so one gameplay frame
applies native/Kontrol zoom once.

Zoom injection is eligible only when all of these are true:

- the frame is enabled;
- the controlled object is a locally human-controlled `MyShipController`;
- the object exposes a camera controller;
- the camera is third person; and
- Kontrol Look Around is active.

Otherwise the native SE1 input path is returned untouched.

## Diagnostics and update evidence

The plugin emits throttled diagnostics for:

- native hook installation and verified callsite counts;
- host frame values and the final flight merge;
- camera mode, elapsed look time, applied look deltas, and forward-thrust
  suppression;
- raw dedicated/fallback zoom axes, resolved direction, sensitivity, native
  value before merging, and merged value afterward.

For a game update, run the local reference sync and compare the relevant
assembly hashes/MVIDs before rebuilding. The standalone IL validator should
continue to report one gate replacement and two directional analog
replacements with unchanged opcode/control-flow metadata. Then run the adapter
tests and package validator. A passing build is not a visual camera guarantee:
the manual in-game checklist must still verify straight-line distance motion,
both directions and partial magnitudes, no duplicate zoom during camera look,
no re-entry jump, forward-axis suppression, and unchanged native keyboard/mouse
and Alt+wheel behavior.
