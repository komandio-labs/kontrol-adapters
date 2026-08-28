# SE2 Translation and Velocity Hold Control Model

Status: implementation handoff/design specification

This document describes the intended Space Engineers 2 adapter behavior and
the constraints an implementation agent must preserve. It is not, by itself,
a claim that the current local adapter binary implements every item below.
The owning work item is `komandio-labs/kontrol-adapters#22`.

## 1. The central distinction

There are two different values in this design:

1. **Physical movement command**: the value sent to SE2's
   `MovementInputs`. This is the value that SE2 uses to calculate movement,
   acceleration, dampening, gravity compensation, and the resulting ship
   velocity.
2. **Presentation command**: the user's shaped joystick axis, used to make
   thruster flames, thrust activity, and sound communicate what the pilot is
   commanding.

In Direct Thrust mode these values are normally the same. In Velocity Hold they
are intentionally different:

```text
joystick axis = 0.50
presentation command = 0.50
physical SE2 command = controller output required to approach/hold the target
```

The presentation command must not be used as the physical command merely to
make the thrusters look correct. Doing that would reintroduce SE2's native
behavior: any nonzero input continuously accelerates the ship until a game
limit or another force stops it.

## 2. What SE2 actually does with movement input

SE2's movement input is analog internally, even when a native keyboard action
normally supplies only `0` or `1`. `MovementInputs` contains floating-point
fields for the six translation directions:

```text
Forward, Backward, Right, Left, Up, Down
```

The game builds a signed movement vector approximately as:

```text
X = Right - Left
Y = Up - Down
Z = Backward - Forward
```

Therefore:

```text
Forward = 0.10 → movement.Z = -0.10
Forward = 1.00 → movement.Z = -1.00
Right   = 0.50 → movement.X = +0.50
Down    = 0.25 → movement.Y = -0.25
```

SE2's `Thrust6Directions.SampleVector` treats the vector components as
directional multipliers. In simplified terms, `0.10` requests about 10% of the
available thrust in that direction. The actual acceleration still depends on
ship mass, available thruster force, direction, efficiency, gravity, drag, and
other game rules.

The important consequence is:

```text
nonzero MovementInputs held continuously → continuous physical acceleration
```

SE2 does not interpret `Forward = 0.50` as “travel at 50% of maximum speed.”
Velocity Hold is the adapter-side controller that adds that interpretation.

## 3. Coordinate system and six-axis mapping

The host provides three signed, normalized translation axes:

```text
surge: negative = reverse, positive = forward
sway:  negative = left,    positive = right
heave: negative = down,    positive = up
```

Each is shaped by the host and delivered in `[-1, +1]`. The adapter converts
them into SE2's six directional fields:

```text
forward  = max( surge, 0)
backward = max(-surge, 0)

right    = max( sway, 0)
left     = max(-sway, 0)

up       = max( heave, 0)
down     = max(-heave, 0)
```

All three axes may be active simultaneously. The same control model applies to
forward/backward, left/right, and up/down; it is not forward-only.

The rotational flight mode does not change this translation contract. Both
`DirectAngularFlight` and `NativeReticleSteering` must use the same selected
translation mode. Only the rotational input path differs.

## 4. Direct Thrust mode

Direct Thrust is the literal mapping:

```text
physical command = presentation command = shaped joystick axis
```

A held 50% forward axis requests approximately 50% forward movement thrust on
every update. It continues accelerating while SE2 can increase velocity. A
held 10% axis similarly continues accelerating, only more slowly.

When the axis returns to zero, the adapter submits zero voluntary thrust for
that axis. The result then depends on SE2's dampener state:

```text
dampeners ON  → SE2 may apply opposing thrust and brake the ship
dampeners OFF → the ship coasts unless another force acts
```

Direct Thrust must remain available and must not acquire Velocity Hold's speed
feedback behavior.

## 5. Velocity Hold mode

Velocity Hold gives the current joystick axis a target-speed meaning. It does
not send the target speed to SE2 as if SE2 had a native analog speed target;
the adapter calculates a physical `MovementInputs` command every update.

For each axis `i`:

```text
x_i       = shaped joystick axis in [-1, +1]
V_max     = runtime-resolved game speed limit in m/s
v_i       = current measured velocity in the matching local axis, m/s
v_target  = x_i × V_max
error     = v_target - v_i
```

The controller converts `error` into a signed, normalized physical command.
The exact proportional/integral gains belong to the existing controller and
must not be redesigned as part of the presentation-only change.

Illustrative target values when `V_max = 300 m/s`:

```text
20% axis →  60 m/s = 216 km/h
50% axis → 150 m/s = 540 km/h
90% axis → 270 m/s = 972 km/h
100%     → 300 m/s = 1080 km/h
```

These numbers explain the apparent contradiction between the pilot's 50%
throttle indication and the physical engine command. At the beginning of an
acceleration, the controller may request substantial thrust. As the measured
velocity approaches the target, the physical command can become small or can
change as required by the existing controller and environmental forces.

That physical reduction is intentional. The visual and audio channels should
still represent the pilot's 50% command.

### Target changes

The target is recalculated continuously:

```text
axis 20% → 80%: target increases and the controller accelerates toward it
axis 80% → 20%: target decreases and the controller follows existing
                 Velocity Hold correction behavior
axis crosses zero: target changes direction
axis returns zero: target becomes zero
```

Do not replace this with a one-time capture or a fixed thrust value.

## 6. Dampeners and environmental forces

The desired user-facing behavior is:

```text
nonzero target, dampeners ON  → hold the target velocity
nonzero target, dampeners OFF → hold the target velocity when possible
zero target, dampeners ON     → brake toward zero
zero target, dampeners OFF    → coast at current velocity
```

This is a control requirement, not a claim that zero `MovementInputs` always
means zero net acceleration.

At a target speed in empty space, approximately zero physical thrust may be
needed. In atmosphere, drag can require continuous thrust to maintain the same
speed. In gravity, a vertical velocity target may require force to counter
gravity. A fixed value such as `0.001` cannot generally solve either case.

With dampeners enabled, SE2 may interpret a zero voluntary movement command as
a request to apply its own opposing dampening. The implementation must account
for this interaction rather than assuming the dampener state is irrelevant.
Possible approaches, in preferred order, are:

1. Use a stable native desired-velocity/flight-assist path if one is available.
2. Keep Velocity Hold's physical command separate from and coordinated with
   native dampener output.
3. Use a documented compatibility workaround such as a deadband or minimum
   same-direction command, clearly recognizing that it may produce a small
   speed ripple and is not exact force balancing.

Do not change the user's dampener preference as a side effect of the controller.
Do not use the presentation command to solve dampener behavior.

## 7. Runtime game speed limit

The adapter must not treat `300 m/s` as an eternal hard-coded game fact.
`300 m/s` is only an example and currently corresponds to `1080 km/h`.

SE2 exposes two useful runtime concepts:

1. `IVelocityLimitProvider.LinearVelocityLimit`: the general game velocity
   limit available through the movement/thrust context.
2. `SoftSpeedLimitData.Speed`: the active soft speed limit associated with the
   current grid/cockpit context. `GridSpeedLimitComponent` adds or removes this
   data, and `ThrustComponent.ComputeThrust` consumes it.

The adapter should resolve the target-speed ceiling in this order:

```text
active current-grid SoftSpeedLimitData.Speed
    ↓ if unavailable
SE2 IVelocityLimitProvider.LinearVelocityLimit
    ↓ if unavailable
optional adapter fallback/configuration
```

An absent `SoftSpeedLimitData` means that no per-grid soft limit is active; it
does not mean the speed limit is zero. The provider fallback is required.

SE2 values are meters per second. Display conversion is:

```text
km/h = m/s × 3.6
m/s  = km/h ÷ 3.6
```

The current velocity must never be mistaken for the maximum speed. For
example, a ship travelling at 150 m/s may still have a 300 m/s limit.

If a user-configured cap is retained, it should be an explicit optional cap:

```text
effective maximum = min(runtime game limit, optional user cap)
```

It must not silently replace runtime discovery.

## 8. Physical versus presentation data

SE2 stores controlled thrust information in `VoluntaryThrustData`. The game
uses this data in more than one place:

- `ThrusterEffectsComponent.UpdateThrusterAnimationData` selects and scales
  directional thruster flame effects.
- `ThrustEffectsComponent.CheckThrustAudio` derives thrust audio intensity.
- Other movement/effect paths, including dampening-related logic, can also
  observe voluntary-thrust data.

Consequently, blindly overwriting `VoluntaryThrustData` with the raw joystick
axis is unsafe: it can make the visuals correct while changing physics or
dampener behavior, or can make audio correct by corrupting another movement
consumer.

The implementation should preserve two channels:

```text
physical channel:
  adapter's existing Velocity Hold output → SE2 movement/physics

presentation channel:
  raw shaped axes → thruster visualization and thrust audio
```

Preferred implementation strategy:

1. Keep the existing physical `MovementInputs` and Velocity Hold calculation
   unchanged.
2. Store the latest raw local presentation vector for the controlled grid,
   with safe lifetime/reset handling.
3. Patch or adapt the narrowest SE2 presentation consumers so they use that raw
   vector for flame and audio intensity only.
4. Verify that the patch does not feed the raw vector back into movement,
   dampeners, or other physical calculations.

If the only viable hook is the producer of `VoluntaryThrustData`, the next
agent must first inspect the call order and all consumers in the validated SE2
build. A producer-level replacement is acceptable only if the original
physical value remains available to every physics/dampener consumer.

## 9. Which thrusters should be shown

The raw presentation vector should use the same local coordinate convention as
SE2's voluntary thrust data:

```text
presentation local vector = (sway, heave, -surge)
```

Then it must be expressed in the entity-local frame expected by SE2, using the
same observer/grid orientation conversion as the existing movement path.

SE2 selects thrusters by component sign:

```text
vector.X > 0 → right-facing thrust
vector.X < 0 → left-facing thrust
vector.Y > 0 → up-facing thrust
vector.Y < 0 → down-facing thrust
vector.Z < 0 → forward-facing thrust
vector.Z > 0 → backward-facing thrust
```

The component magnitude controls the displayed intensity. Thus a 50% right
axis activates the right-direction presentation at approximately 50%, even if
the physical Velocity Hold command is currently 3% or 0%.

The adapter should not enumerate individual thrusters or guess their physical
locations. SE2's `ThrusterEffectsComponent` already performs the directional
selection and applies per-thruster efficiency. The adapter's responsibility is
to provide the correct local presentation vector at the correct presentation
hook.

## 10. Sound behavior

Sound must follow the same presentation command as the flame effects. If the
pilot holds 50% forward, the thrust sound should communicate approximately 50%
command, even when Velocity Hold has reduced the physical command near the
target.

However, sound is not independent of SE2's effect data: `CheckThrustAudio`
reads voluntary-thrust data and combines it with current velocity when deriving
audio intensity. Therefore the implementation must test both:

```text
raw throttle → expected directional sound/activity
physical VH output → unchanged movement and dampener behavior
```

The sound result should be documented as an intentional presentation cue, not
as a measurement of physical acceleration or fuel/energy consumption.

## 11. Cruise Control invariants

The visual/physical split must not alter Cruise Control semantics.

Cruise Control remains a forward-speed target layered on top of the existing
translation controller:

- Set captures the current non-negative forward speed.
- Positive throttle may temporarily request a higher speed.
- Returning throttle to the neutral deadband resumes the captured target.
- Negative throttle beyond the jitter deadband cancels Cruise Control and gives
  manual braking/reverse control.
- Increase/decrease adjust the target by 10 m/s and clamp it at zero.
- Double-click Reset cancels Cruise Control.

Cruise Control's physical output must remain on its existing path. Its
presentation output may show the current raw throttle axis, but the adapter
must not reinterpret raw presentation data as a new Cruise Control target or
change Cruise state transitions.

## 12. State and lifecycle requirements

Raw presentation state must be keyed to the active controlled grid/cockpit or
otherwise be impossible to apply to a different grid. Reset it when:

- input control is disabled;
- the observed cockpit/grid changes;
- the player exits the grid;
- the adapter runtime resets or shuts down;
- telemetry becomes unavailable for the current control update.

If raw presentation data is unavailable, the safe fallback is the physical
SE2 value for that update, not stale raw data from another grid or an earlier
frame.

All values must be finite and clamped to `[-1, +1]`. Presentation state must be
thread-safe because input capture, game update hooks, and effect consumers may
not run on the same schedule.

## 13. Required validation matrix

The next implementation agent must test both rotational flight modes:

| Translation mode | Flight mode | Dampeners | Required checks |
| --- | --- | --- | --- |
| Direct Thrust | Direct Angular Flight | On/off | Raw input equals physical thrust; native dampening behavior remains unchanged |
| Direct Thrust | Native Reticle Steering | On/off | Same translation behavior as Direct Angular Flight |
| Velocity Hold | Direct Angular Flight | On/off | Physical speed target is maintained; presentation follows raw input |
| Velocity Hold | Native Reticle Steering | On/off | Same target and presentation behavior as Direct Angular Flight |
| Velocity Hold + Cruise | Both | On/off | Cruise capture, override, resume, cancel, and reset are unchanged |

For every row, exercise all three translation axes:

- forward and reverse;
- left and right;
- up and down;
- 0%, 10%, 20%, 50%, 90%, and 100% input;
- changing input while already moving;
- axis reversal;
- simultaneous diagonal axes.

Also validate:

- space with dampeners off: release coasts;
- space with dampeners on: zero target brakes, nonzero target holds as designed;
- atmosphere: drag compensation does not make presentation fall to zero;
- gravity: vertical velocity behavior is distinguished from altitude hold;
- changing mass or insufficient thrust;
- active game speed limit discovery;
- a grid with no `SoftSpeedLimitData`;
- telemetry loss, cockpit exit, grid change, and input disable;
- native keyboard movement coexisting with the adapter.

The key observable assertion for the split is:

```text
raw axis remains 50%:
  visual thruster intensity ≈ 50%
  sound/activity ≈ 50% presentation command
  physical SE2 command may be any controller-required value
  ship speed remains governed by Velocity Hold
```

## 14. Files and private SE2 contracts to re-check after game updates

Adapter-side areas:

```text
src/Adapters/SpaceEngineers2/Kontrol.Adapters.SpaceEngineers2/
  Patches/CockpitInputPatch.cs
  Patches/TranslationVelocityController.cs
  Settings/SpaceEngineers2SettingsProvider.cs
```

SE2 contracts to inspect against the selected local reference build:

```text
Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs
Keen.Game2.Simulation.WorldObjects.Movement.VoluntaryThrustData
Keen.Game2.Simulation.WorldObjects.Movement.SoftSpeedLimitData
Keen.Game2.Simulation.WorldObjects.Movement.ThrustComponent
Keen.Game2.Client...ThrustEffectsComponent
Keen.Game2.Client...ThrusterEffectsComponent
```

Confirm method signatures, data ownership, coordinate transforms, and call
order before adding Harmony hooks. These are private game implementation
details, not stable public mod APIs.

## 15. Implementation boundary

The requested first implementation should be narrow:

1. Preserve the already-agreed physical Velocity Hold and Cruise Control
   behavior.
2. Add the raw six-axis presentation channel.
3. Make flame and sound use that presentation channel without altering physics.
4. Replace any mandatory adapter speed constant with runtime SE2 limit
   discovery, retaining only an explicit fallback/user cap if needed.
5. Add focused tests for coordinate mapping, lifecycle reset, speed-limit
   resolution, and Cruise Control non-regression.

Do not combine this work with a new flight-control model, a new damping policy,
or a release/version change unless separately authorized.
