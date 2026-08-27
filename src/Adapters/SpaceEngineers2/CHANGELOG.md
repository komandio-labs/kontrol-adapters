# Space Engineers 2 adapter changelog

## Unreleased

- Adds an independent `Velocity Hold` translation-control option. It maps the
  current shaped translation axis to a local target velocity and uses
  axis-scaled proportional feedback, while retaining `Direct Thrust` as the
  default unchanged mapping.
- Adds a configurable 1–300 m/s target-speed cap (300 m/s / 1080 km/h by
  default) and uses a lower private SE2 velocity limit when available.
- Adds focused controller and settings tests. SE2 dampener interaction remains
  pending interactive game validation; this local package is not a published
  compatibility claim.
- Fixes Velocity Hold feedback on rotated grids/cockpits by converting SE2's
  world-space rigid-body velocity through both the grid and observer frames.

## 0.2.0 — local validation build

- Changes forward/reverse, strafe left/right, and lift up/down from closed-loop
  target-velocity control to direct proportional thrust in both SE2 flight
  modes. The host's shaped analog magnitude is now submitted unchanged to the
  corresponding SE2 thrust direction.
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
