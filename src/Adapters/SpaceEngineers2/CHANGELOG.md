# Space Engineers 2 adapter changelog

## Unreleased

- Removes the experimental Cruise Control HUD indicator, setting, template
  hooks, and standalone POC after unsuccessful in-game validation. Cruise
  Control input and physics behavior remain unchanged.

- Keeps SE2's native per-frame thrust-audio velocity update active after
  joystick or keyboard translation release, allowing engine sound to fall with
  deceleration without changing raw-command thruster visuals or movement data.
- Makes Cruise Control Set ignore zero or reverse forward speed while retaining
  double-click Reset. Cruise target adjustment buttons now change by 1 unit in
  the player-selected speed presentation on press, repeat while held, and
  accelerate through 5 and 10 displayed-unit steps. Explicit
  adjustment retargets use full available forward/reverse thrust until the new
  target is reached; easing ordinary throttle retains its existing behavior.
- Restores manual strafe and lift while Cruise Control is active, and adds
  transition-only `[FlightModeTrace]` diagnostics for unexpected Direct Angular
  Flight/Native Reticle Steering changes.

- Removes the Velocity Hold adapter speed constants. The cap slider now uses
  only the active SE2 grid's runtime speed limit and remains unavailable until
  that game-defined limit can be observed.
- Adds typed adapter-resolved numeric presentation units. `Speed Display Units`
  now resolves the final unit for each SE2 speed parameter, including telemetry
  and the Velocity Hold Target-Speed Cap slider: Game Default (SE2 HUD), Metric
  (`km/h`), or Imperial (`mph`). Canonical physics values remain `m/s`.
- Makes hands-off Cruise Control show its calculated hold thrust through the
  thruster animation/audio presentation channel. Manual throttle continues to
  show the raw shaped joystick axis, and presentation remains separate from
  Cruise state and SE2 physics data.
- Lets SE2 dampeners handle ordinary Velocity Hold target reductions. The
  adapter no longer sends opposing movement thrust below the selected target:
  Dampeners ON brakes and Dampeners OFF coasts. Cruise Control retains its
  separate signed correction behavior.
- Limits the high Velocity Hold response gain to acceleration toward the
  selected target. Small throttle reductions near the speed limit now retain
  gentle baseline braking instead of being multiplied into heavy reverse thrust.
- Prevents transient disabled-input frames from switching Direct Angular Flight
  back to SE2 target-based/Reticle gyro mode. Native Reticle Steering still
  preserves the cockpit's prior target-based preference. Reticle presentation
  now falls back to the cockpit orientation while its observer transform is
  temporarily unavailable. Adds a compact 4 Hz `[VelocityHoldTrace]`
  diagnostic line that records mode, dampeners, raw target axes, local
  velocity, physical command, and presentation vector without host truncation.
- Fixes Velocity Hold thruster effects/audio using the grid's SE2 `DEntity`
  identity rather than the managed grid object's unrelated hash code.
- Adds the realtime `Velocity Hold Response` setting (default `12×`) so full
  throttle remains strong near the target instead of asymptotically creeping
  toward the speed limit.
- Fixes Velocity Hold target reductions: when the current velocity exceeds the
  newly selected nonzero target, the physical controller now commands bounded
  opposing thrust rather than coasting at the old speed.
- Adds a grid-scoped raw joystick presentation channel for six-direction
  thruster flames and thrust audio. Velocity Hold physical `MovementInputs`,
  Cruise Control, and SE2's shared `VoluntaryThrustData` remain untouched.
- Resolves Velocity Hold speed from active-grid `SoftSpeedLimitData.Speed`,
  then SE2's `LinearVelocityLimit`; the optional target cap no longer treats
  300 m/s as a permanent game limit. Removes the arbitrary dampener guard.
- Changes ordinary Velocity Hold to remove same-direction thrust without
  commanding opposite-direction braking when the live throttle is reduced.
  Cruise Control's signed positive-throttle handoff remains unchanged.
- Replaces the pending `Throttle Hold` action with Cruise Control. Set captures
  the current forward speed; positive throttle temporarily overrides it;
  negative throttle cancels it. Cruise targets can be adjusted by +/-10 m/s,
  never go below zero, and double-clicking Set resets Cruise Control.
- Refines Cruise Control's positive-throttle override under Velocity Hold: it
  now targets the higher of the captured cruise speed and throttle-derived
  speed, preventing both underspeed braking and direct-thrust overshoot.
- Makes Velocity Hold the default translation mode and lists it before Direct
  Thrust.
- Adds an independent `Velocity Hold` translation-control option. It maps the
  current shaped translation axis to a local target velocity and uses
  directional axis-scaled proportional feedback, while retaining `Direct
  Thrust` as the selectable direct mapping.
- Adds a configurable 1–300 m/s target-speed cap (300 m/s / 1080 km/h by
  default) and uses a lower private SE2 velocity limit when available.
- Adds focused controller and settings tests. SE2 dampener interaction remains
  pending interactive game validation; this local package is not a published
  compatibility claim.
- Fixes Velocity Hold feedback on rotated grids/cockpits by converting SE2's
  world-space rigid-body velocity through both the grid and observer frames.

## 0.2.0 — local validation build

- Adds the initial forward/reverse, strafe left/right, and lift up/down
  translation mapping for both SE2 flight modes.
- Retains rate-limited input tracing for the pending manual validation. This
  local build is not a published compatibility claim.

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
