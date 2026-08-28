# Space Engineers 2 adapter changelog

## Unreleased

## 0.2.0 — release candidate

This release requires Kontrol SDK `1.2.0` and input schema `8`. The exact
SE2 compatibility claim remains pending manual validation of the selected game
build; this changelog does not by itself make an untested build supported.

### Translation controls

- Adds `Velocity Hold` translation control for forward/reverse, strafe, and
  lift, while retaining `Direct Thrust` as a selectable mode.
- Makes `Velocity Hold` the default translation mode and fixes feedback on
  rotated grids and cockpits by transforming world-space velocity through the
  grid and observer frames.
- Adds a configurable 1–300 m/s target-speed cap and resolves the effective
  limit from the active grid's SE2 runtime speed data instead of assuming a
  fixed 300 m/s game limit.
- Adds `Velocity Hold Response` (default `12×`) and refines target reduction,
  throttle reduction, and dampener behavior so ordinary target changes do not
  produce unnecessary opposing thrust.

### Cruise Control

- Replaces the pending `Throttle Hold` action with Cruise Control. Set captures
  the current non-negative forward speed; positive throttle can temporarily
  override it, negative throttle cancels it, and double-clicking Set resets it.
- Adds Cruise target increase/decrease actions with the selected speed display
  units, held-button repeat, accelerated step sizes, and a zero-speed floor.
- Restores manual strafe and lift while Cruise Control is active and keeps
  Cruise's signed correction behavior separate from ordinary Velocity Hold.
- Removes the experimental Cruise Control HUD indicator, setting, template
  hooks, and standalone POC after unsuccessful in-game validation. Cruise
  Control input and physics behavior remain available.

### Presentation, settings, and diagnostics

- Adds typed adapter-resolved speed units for settings and telemetry: Game
  Default, metric (`km/h`), or imperial (`mph`), while canonical physics values
  remain in `m/s`.
- Adds a grid-scoped joystick presentation channel for six-direction thruster
  flames and thrust audio without changing physical `MovementInputs`, Cruise
  Control state, or SE2's shared `VoluntaryThrustData`.
- Keeps native SE2 thrust-audio velocity updates active after joystick or
  keyboard translation release, and uses the grid's SE2 `DEntity` identity for
  thruster effects/audio.
- Prevents transient disabled-input frames from changing the selected flight
  mode and adds compact `[FlightModeTrace]` and `[VelocityHoldTrace]`
  diagnostics for local validation.

### Validation

- Adds focused controller, settings, and compatibility validation tests.
- The exact package must still complete the manual SE2 validation checklist
  before it can carry a `Tested` compatibility claim.

## 0.1.0 — first stable release

### Compatible Game Version
- Space Engineers 2 (`2.4.0.86`)

### Dual Flight Control Modes
- Direct Angular Flight (Default): True 1:1 joystick flight with rate-controlled angular velocity, natural acceleration ramping, and customizable rotational glide.
- Native Reticle Steering: Preserves the game's classic virtual mouse-reticle steering and crosshair dampening on physical joystick axes.

### Real-Time In-Flight Tuning
- Rotational Acceleration Ramp: Fine-tune how quickly the ship reaches full turn speed.
- Rotational Glide Deceleration: Control inertia and coasting when releasing the stick to center.
- Maximum Turn Rate: Scale maximum rotational speed from precision docking to high agility.

### Controls & Actions
- Full 6-DoF analog movement (pitch, yaw, roll, and 3D translation).
- Dedicated binding support for Inertial Dampeners, Camera View Switching, Primary Fire, and Tool actions.

## 0.1.0-beta.1 — first public beta

This beta is initially published as **Untested** until the exact package has
completed the automated and manual SE2 validation checklist.

- Native Plugin Parameter is now the default deployment method.
- Process Injection remains available as an alternate deployment method.

Historical baseline validation was performed against SE2 `2.3.0.2798` on
2026-07-31, but it does not constitute a Tested claim for this beta package.

- Declared the managed Process Injection entry point consumed by the Kontrol
  host's Steam-aware native injector.
- Removed the SE2 assembly-hook deployment path. Kontrol no longer rewrites
  `VRage.Library.dll`; use Process Injection or the native SE2 plugin loader.

## Historical development baseline

- Added the versioned release manifest, compatibility record, and local inspection workflow.
- Separated the shared adapter runtime from the SE2 plugin and CoreCLR startup-hook entry points.

## 2026-07-30 — schema version 5

- Added Camera Mode Switch (`camera.mode_switch`) as trigger bit 13.
- Added Exit Grid, primary fire, and reload action support.
- Recorded historical Space Engineers 2 `2.3.0.2798` evidence; it must be revalidated before a Tested claim.
