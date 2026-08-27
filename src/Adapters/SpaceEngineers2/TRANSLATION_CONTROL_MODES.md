# SE2 translation-control modes

For the implementation handoff covering the intended physical/presentation
split, six-axis behavior, dampeners, dynamic speed-limit discovery, thruster
selection, sound, and Cruise Control invariants, see
[`VELOCITY_HOLD_CONTROL_MODEL.md`](./VELOCITY_HOLD_CONTROL_MODEL.md). This
document records the current adapter implementation details; the handoff
document separates those details from the target behavior for the next change.

This document specifies the meaning of the three translational axes in the
Space Engineers 2 adapter: forward/reverse (surge), right/left (sway), and
up/down (heave). It describes the default behavior and the optional
velocity-target behavior. Velocity Hold is the default.

## Implementation status

Velocity Hold is implemented as an adapter-side realtime setting named
`translationControlMode`; it is independent of `flightModelMode` and defaults
to `VelocityHold`. Its controller is an axis-scaled proportional controller
with a realtime configurable response gain (default `Kp = 12`), no integral
term, and no slew limiter. The response gain applies while accelerating toward
the selected target. Ordinary Velocity Hold does not submit opposing physical
thrust when a target is below current velocity: Dampeners ON brakes and
Dampeners OFF coasts. Cruise Control keeps its separate signed correction.
`velocityHoldMaxTargetSpeed` is an optional target cap;
`0` means no adapter cap. The active grid's `SoftSpeedLimitData.Speed` is used
first, then SE2's private `_velocityLimits.LinearVelocityLimit`; a `300 m/s`
adapter fallback is used only when neither runtime value is available. A
positive optional cap limits the discovered runtime value. A recognized legacy
`LinearVelocity`, `MaxLinearVelocity`, `MaximumLinearVelocity`, or `MaxSpeed`
member is accepted as a compatibility fallback.

The implementation uses SE2's world-space rigid-body measurement and transforms
it first into grid-local space, then into the cockpit/observer input frame. If
`q_grid` is the grid's world orientation and `q_observer` is the observer's
grid-relative orientation, the input-frame velocity is
`inverse(q_observer) × inverse(q_grid) × worldVelocity`. It then preserves
`surge = -Z`, `sway = X`, and `heave = Y`. If that measurement is unavailable
during cockpit transition or telemetry loss, it safely falls back to Direct
Thrust for that update rather than commanding a velocity target from guessed
data.

The adapter does not change the user's dampener preference or add an arbitrary
minimum thrust. A zero target therefore leaves braking/coasting to SE2's
enabled/disabled dampener state. Nonzero targets remain on the physical
Velocity Hold controller path. Native behavior in atmosphere, space, gravity,
and lateral movement still requires manual validation on SE2 build 2.4.0.86.

For effects and audio, the adapter separately stores the raw shaped axis as
the SE2 entity-local vector `(sway, heave, -surge)`, rotated by the observer
orientation. Client-only Harmony hooks substitute this vector only when SE2's
thruster flame caches and thrust-audio checks read it. They never write
`VoluntaryThrustData`, which remains the physical/dampener channel. The state
is keyed by SE2's grid `DEntity` identity and cleared on input loss,
cockpit/grid changes, reset, and shutdown. While Cruise Control is actively
holding speed with the translation axes centered, presentation instead shows
Cruise's already-computed physical hold command; manual throttle continues to
show the raw shaped axis. This presentation-only exception never changes Cruise
targets, state, or SE2 physics data.

## Scope and terminology

The translation-control mode is independent of the adapter's rotational flight
mode (`DirectAngularFlight` or `NativeReticleSteering`). Both rotational modes
use the same selected translation mapping.

The host remains responsible for device reading and input shaping:

- deadzone removal;
- response-curve/exponent application;
- inversion and sign normalization; and
- delivery of a signed normalized value in `[-1, +1]`.

The adapter is responsible for converting that value into SE2-specific movement
commands. The host always sends the current axis value. It does not send a
different protocol value when a translation-control option changes.

Let each shaped host axis be:

```text
x_s  = surge input:  negative reverse, positive forward
x_y  = sway input:   negative left,    positive right
x_h  = heave input:  negative down,    positive up
```

Each value is clamped to `[-1, +1]`. The equations below apply independently
to each axis; all three may be active simultaneously.

The word *thrust* below means the normalized magnitude submitted to SE2's
`MovementInputs`, not a physical force in newtons. A value of `0.20` means the
adapter requests 20% of the available movement thrust on that direction.

## Mode A: Direct Thrust (optional)

### Concept

The host axis is an immediate thrust command. It does not represent a speed
target, and the adapter does not use measured velocity to reduce or reverse the
command.

For each axis, the positive and negative components are separated and sent to
the corresponding SE2 direction:

```text
forward  = max(x_s, 0)
backward = max(-x_s, 0)

right    = max(x_y, 0)
left     = max(-x_y, 0)

up       = max(x_h, 0)
down     = max(-x_h, 0)
```

In vector form, for an axis value `x`:

```text
u_direct(x) = x
```

with the sign represented by the appropriate positive or negative SE2 input.
There is no velocity term in this equation.

### Consequences

For a ship mass `m` and available force `F_max`, an idealized axis
acceleration is approximately:

```text
a = x × F_max / m
```

Therefore, a held 20% input keeps applying approximately 20% thrust and keeps
accelerating while the game can increase velocity. A held 90% input applies
approximately 90% thrust and accelerates more rapidly. The game speed limit,
available power, ship mass, gravity, and thruster effectiveness can limit the
result.

The input percentage is not a percentage of maximum speed:

```text
20% input ≠ 20% of the speed limit
```

### Neutral and dampeners

When the shaped axis returns to zero, the adapter submits zero thrust for that
axis. This means “stop applying acceleration”; it does not itself mean “make
velocity zero.” SE2 dampeners may then apply opposing thrust when enabled.

```text
x = 0, dampeners ON  → SE2 can brake toward zero velocity
x = 0, dampeners OFF → no adapter thrust; the ship coasts unless other forces act
```

This is why the direct mode can show full proportional fire while the axis is
held and still stop the ship after the axis is released: thrust and braking are
separate behaviors.

### Current implementation contract

The current adapter implementation uses `ComputeProportionalThrust` for both
SE2 translation paths. It may read and log local velocity for diagnostics, but
that measurement is not part of the direct thrust command and cannot reduce
the submitted thrust. Native keyboard movement is merged separately where the
selected flight path supports it.

## Mode B: Velocity Hold (default)

### Concept

Velocity Hold gives the axis a target-speed meaning while preserving the host
protocol. The adapter stores no new host-side value; it interprets the current
axis value as the current desired velocity on that local ship axis.

For each axis `i`, define:

```text
x_i       = current shaped host input in [-1, +1]
V_max,i   = configured maximum target velocity for that axis, in m/s
v_i       = measured current local velocity, in m/s
v*_i      = desired local velocity, in m/s
e_i       = velocity error, in m/s
u_i       = signed normalized thrust command in [-1, +1]
```

The target is recomputed every adapter update:

```text
v*_i = x_i × V_max,i
e_i  = v*_i - v_i
```

For the commonly used SE2 grid limit of `300 m/s`:

```text
V_max = 300 m/s = 1080 km/h

x = +0.20 → v* = +60 m/s  = +216 km/h
x = +0.80 → v* = +240 m/s = +864 km/h
x = +0.90 → v* = +270 m/s = +972 km/h
```

The configured limit must remain data-driven because SE2's speed settings and
flight rules can change between game builds. The values above are examples,
not a promise that every future build uses the same limit.

### Axis-scaled proportional control

The required behavior is not only “higher input means higher target speed.” A
higher input must also permit stronger acceleration. Velocity Hold limits its
signed output by the magnitude of the current axis:

```text
u_i = clamp(K_p × e_i / V_max,i, -abs(x_i), +abs(x_i))
```

`K_p` is dimensionless. A ship starting at zero velocity gets the requested
axis magnitude as its initial command because the output is clamped to the axis
magnitude:

```text
v_i = 0 → u_i = x_i
```

As the ship approaches the target, the error shrinks and the thrust command
shrinks. For positive forward input, the ideal no-disturbance behavior is:

```text
u_i = clamp(K_p × x_i × (1 - v_i / v*_i), -abs(x_i), +abs(x_i))
```

when the ship is between zero and its positive target. The same signed
equation applies to Cruise Control's positive-throttle handoff.

Example for a 20% forward target (`v* = 60 m/s`):

```text
current speed =   0 m/s → thrust ≈ 20%
current speed =  30 m/s → thrust ≈ 20% at the default response gain
current speed =  60 m/s → thrust ≈  0%
```

Example for a 90% forward target (`v* = 270 m/s`):

```text
current speed =   0 m/s → thrust ≈ 90%
current speed = 135 m/s → thrust ≈ 90% at the default response gain
current speed = 270 m/s → thrust ≈  0%
```

Thus 90% input can accelerate approximately 4.5 times harder than 20% input
for the same ship and conditions, while also targeting 4.5 times the speed.
Game-side force limits can still saturate both commands.

### PI correction for real disturbances

A proportional controller is sufficient to describe the ideal space case, but
drag, gravity, changing mass, thruster imbalance, and dampener intervention can
create a persistent error. A small integral term can remove that error:

```text
I_i[k] = I_i[k-1] + e_i[k] × Δt

u_raw = K_p × e_i / V_max,i + K_i × I_i
u_i = directionalClamp(u_raw, x_i)
```

The integral accumulator must have anti-windup behavior. It should be clamped
when thrust saturates and reset or decayed when the target changes direction, the
axis returns to neutral, input control is disabled, or the controlled grid
changes. `directionalClamp` preserves the current axis direction for ordinary
Velocity Hold; Cruise Control may use its signed clamp. A derivative term is not
initially required because velocity is already measured directly and a derivative
of that signal would amplify game-physics noise.

### Dynamic axis changes

The target is a live setpoint, not a command that is accepted once and then
completed. The adapter recalculates it every update:

```text
axis moves 20% → 80%:
  target changes from 60 m/s to 240 m/s
  thrust rises toward the current 80% output limit

axis moves 80% → 20% while moving faster than the new target:
  target drops from 240 m/s to 60 m/s
  the controller commands reverse physical thrust until the new target is reached

axis crosses +20% → -20%:
  target changes sign
  the controller brakes through zero and continues toward the reverse target

axis returns to 0%:
  target becomes zero
  dampeners or adapter-owned braking determine whether the ship stops or coasts
```

The initial implementation deliberately does not add a thrust slew limit: the
host already supplies shaped axis values and a limiter would weaken the required
20% versus 90% initial-thrust difference. Add one only if interactive testing
shows frame-to-frame jitter.

### Local velocity and signs

Velocity must be measured in the same local frame used for the target. SE2
rotates the cockpit/observer movement vector into grid-local space, then the
grid orientation carries that vector into world space. Velocity feedback must
undo both transforms in reverse order before the error is calculated:

```text
gridVelocity  = inverse(gridWorldOrientation) × worldVelocity
localVelocity = inverse(observerRelativeOrientation) × gridVelocity
```

The resulting local components are interpreted as:

```text
surge velocity = -localVelocity.Z
sway velocity  =  localVelocity.X
heave velocity =  localVelocity.Y
```

The adapter must preserve these signs when calculating `e_i` and when splitting
`u_i` into SE2's positive and negative `MovementInputs` fields.

### Dampener policy

Velocity Hold defines dampener behavior explicitly. A nonzero target must remain
stable whether the user's dampener setting is enabled or disabled, while a
positive throttle reduction must not create adapter-owned reverse thrust.

There are two possible implementation strategies, in descending order of
control quality:

1. Use a native SE2 desired-velocity/flight-assist path, if a stable API exists.
2. Suppress native dampener braking while ordinary Velocity Hold owns a
   nonzero target, and use directional controller output for acceleration.
The adapter deliberately does not fabricate a fixed minimum command: atmospheric
drag and gravity require environment-specific force, not a universal constant.
If dampeners are disabled, the controller still supplies signed feedback for a
nonzero target; it outputs zero only at the target. A zero target remains
SE2-owned, so dampeners off coasts and dampeners on brakes.

For the user-facing preference, the intended semantics are:

```text
nonzero axis target, dampeners ON  → hold that target speed
nonzero axis target, dampeners OFF → hold that target speed
zero axis target, dampeners ON     → brake toward zero
zero axis target, dampeners OFF    → coast at current velocity
```

The exact transition must be validated against the SE2 build because the
adapter currently submits `MovementInputs`, not a documented desired-velocity
command. The implementation does not toggle or persist a dampener state; it
keeps the user's preference intact on mode changes, cockpit exit, input disable,
and adapter shutdown.

### Gravity and heave

Forward and lateral targets can be held with little or no thrust in ideal space
once velocity matches the target. A zero heave-speed target in gravity is not
the same as zero force: the adapter must provide enough upward force to balance
gravity if the intended behavior is hover/altitude hold. If the option is only
defined as velocity hold, it should target vertical velocity and document that
altitude itself is not held.

## Comparison

| Question | Direct Thrust (optional) | Velocity Hold (default) |
| --- | --- | --- |
| Meaning of axis | Instantaneous thrust magnitude | Desired local velocity |
| 20% input | Approximately 20% thrust continuously | Target 20% of configured speed limit |
| 90% input | Approximately 90% thrust continuously | Target 90% of configured speed limit and up to 90% thrust |
| Velocity feedback | Diagnostic only | Required every update |
| At target speed | Still accelerates if input remains nonzero | Thrust tapers toward the required value |
| Axis to center | Zero adapter thrust; SE2 may damp | Target velocity becomes zero |
| Axis reversal | Immediate opposite thrust mapping | Controller brakes through zero toward the new target |
| Dampeners off | Ship coasts when adapter thrust is zero | Controller corrects nonzero targets; zero target coasts |
| Dampeners on | SE2 may brake after neutral input | SE2 braking applies at a zero target; nonzero-target stability requires game validation |
| Host protocol | Shaped signed axis | Same shaped signed axis |
| Rotational mode dependency | None | None |

## Acceptance tests for Velocity Hold

Validate each axis in a controlled game build:

1. At zero velocity, hold 20%, 80%, and 90%; verify initial thrust follows the
   corresponding input magnitude and target speed is approached without a
   full-thrust-to-target step.
2. Change the held axis continuously between neutral, partial, full, and
   reverse; verify the target and output react every update.
3. At a nonzero target, verify speed remains within the configured tolerance
   with dampeners both enabled and disabled.
4. Release the axis with dampeners enabled; verify braking toward zero.
5. Release the axis with dampeners disabled; verify coasting.
6. Reverse direction from an established speed; verify braking precedes stable
   reverse motion and opposing commands are not simultaneously submitted.
7. Test heave in gravity and in space; distinguish velocity hold from altitude
   hold and record the chosen gravity-compensation behavior.
8. Confirm the controller handles changing mass, insufficient thrust, the game
   speed cap, loss of telemetry, cockpit exit, and input disable safely.

Direct Thrust remains available for users who prefer its existing behavior.
