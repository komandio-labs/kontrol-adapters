namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>
/// Converts the current shaped host translation axes into either direct thrust
/// or local-velocity feedback thrust. It intentionally has no game dependency
/// so the control law can be verified without an SE2 process.
/// </summary>
internal static class TranslationVelocityController
{
    internal const float DefaultMaximumTargetSpeedMetersPerSecond = 300f;

    internal static (float fwd, float back, float right, float left, float up, float down) ComputeDirectThrust(
        float surge, float sway, float heave) =>
        (SplitPositive(surge), SplitNegative(surge),
         SplitPositive(sway), SplitNegative(sway),
         SplitPositive(heave), SplitNegative(heave));

    internal static (float fwd, float back, float right, float left, float up, float down) ComputeVelocityHoldThrust(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond,
        float responseGain = 1f) =>
        ComputeVelocityHoldAxes(
            surge, sway, heave, actualSurge, actualSway, actualHeave,
            maximumTargetSpeedMetersPerSecond, responseGain);

    /// <summary>
    /// Computes the signed controller used by Cruise Control's positive-throttle
    /// handoff. This intentionally preserves the original strict setpoint
    /// behavior, including opposing correction when the ship is overspeed.
    /// </summary>
    internal static (float fwd, float back, float right, float left, float up, float down) ComputeCruiseVelocityHoldThrust(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond) =>
        ComputeCruiseVelocityHoldAxes(
            surge, sway, heave, actualSurge, actualSway, actualHeave,
            maximumTargetSpeedMetersPerSecond);

    internal static float ComputeAxis(
        float input, float actualVelocity, float maximumTargetSpeedMetersPerSecond, float responseGain = 1f) =>
        ComputeAccelerationAxis(Normalize(input), actualVelocity, maximumTargetSpeedMetersPerSecond, responseGain);

    internal static float ComputeCruiseAxis(float input, float actualVelocity, float maximumTargetSpeedMetersPerSecond) =>
        ComputeSignedAxis(Normalize(input), actualVelocity, maximumTargetSpeedMetersPerSecond, responseGain: 1f);

    private static (float fwd, float back, float right, float left, float up, float down) ComputeVelocityHoldAxes(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond, float responseGain) =>
        (SplitPositive(ComputeAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond, responseGain)),
         SplitNegative(ComputeAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond, responseGain)),
         SplitPositive(ComputeAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond, responseGain)),
         SplitNegative(ComputeAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond, responseGain)),
         SplitPositive(ComputeAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond, responseGain)),
         SplitNegative(ComputeAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond, responseGain)));

    private static (float fwd, float back, float right, float left, float up, float down) ComputeCruiseVelocityHoldAxes(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond) =>
        (SplitPositive(ComputeCruiseAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeCruiseAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond)),
         SplitPositive(ComputeCruiseAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeCruiseAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond)),
         SplitPositive(ComputeCruiseAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeCruiseAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond)));

    private static float ComputeSignedAxis(
        float input, float actualVelocity, float maximumTargetSpeedMetersPerSecond, float responseGain)
    {
        float maximumSpeed = NormalizeMaximumSpeed(maximumTargetSpeedMetersPerSecond);
        float targetVelocity = input * maximumSpeed;
        float velocityError = targetVelocity - NormalizeVelocity(actualVelocity);
        // Response gain is deliberately one-sided. It keeps acceleration toward
        // a higher target strong near the speed limit, but must not turn a small
        // throttle reduction into amplified reverse thrust. Overspeed correction
        // retains the original proportional gain of one.
        bool acceleratingTowardInputTarget = input * velocityError > 0f;
        float gain = acceleratingTowardInputTarget && float.IsFinite(responseGain)
            ? Math.Max(responseGain, 0f)
            : 1f;
        float proportionalOutput = gain * velocityError / maximumSpeed;
        float axisLimit = MathF.Abs(input);
        return Math.Clamp(proportionalOutput, -axisLimit, axisLimit);
    }

    private static float ComputeAccelerationAxis(
        float input, float actualVelocity, float maximumTargetSpeedMetersPerSecond, float responseGain)
    {
        float maximumSpeed = NormalizeMaximumSpeed(maximumTargetSpeedMetersPerSecond);
        float targetVelocity = input * maximumSpeed;
        float velocityError = targetVelocity - NormalizeVelocity(actualVelocity);

        // Ordinary Velocity Hold does not manufacture braking thrust. A target
        // below the current velocity is left to SE2: dampeners ON brake, while
        // dampeners OFF coast. Cruise Control uses ComputeSignedAxis instead.
        if (input * velocityError <= 0f) return 0f;

        float gain = float.IsFinite(responseGain) ? Math.Max(responseGain, 0f) : 1f;
        float proportionalOutput = gain * velocityError / maximumSpeed;
        return Math.Clamp(proportionalOutput, -MathF.Abs(input), MathF.Abs(input));
    }

    internal static float ComputeMinimumForwardSpeedThrust(
        float targetSpeedMetersPerSecond,
        float actualForwardSpeedMetersPerSecond,
        float maximumTargetSpeedMetersPerSecond)
    {
        float maximumSpeed = NormalizeMaximumSpeed(maximumTargetSpeedMetersPerSecond);
        float targetSpeed = float.IsFinite(targetSpeedMetersPerSecond)
            ? Math.Max(targetSpeedMetersPerSecond, 0f)
            : 0f;
        float actualSpeed = NormalizeVelocity(actualForwardSpeedMetersPerSecond);

        // Cruise Control is a minimum-speed controller: it adds forward thrust
        // below the target and never commands reverse thrust to slow down.
        return Math.Clamp((targetSpeed - actualSpeed) / maximumSpeed, 0f, 1f);
    }

    internal static float ComputeCruiseForwardVelocityHoldAxis(
        float manualThrottle,
        float cruiseTargetSpeedMetersPerSecond,
        float maximumTargetSpeedMetersPerSecond)
    {
        float maximumSpeed = NormalizeMaximumSpeed(maximumTargetSpeedMetersPerSecond);
        float cruiseAxis = Math.Clamp(
            float.IsFinite(cruiseTargetSpeedMetersPerSecond)
                ? Math.Max(cruiseTargetSpeedMetersPerSecond, 0f) / maximumSpeed
                : 0f,
            0f,
            1f);

        // Cruise is the floor. Positive throttle raises that floor only when
        // its velocity target is higher than the captured cruise target.
        return Math.Max(SplitPositive(manualThrottle), cruiseAxis);
    }

    internal static float KilometersPerHour(float metersPerSecond) => metersPerSecond * 3.6f;

    internal static float NormalizeMaximumSpeed(float maximumTargetSpeedMetersPerSecond) =>
        float.IsFinite(maximumTargetSpeedMetersPerSecond) && maximumTargetSpeedMetersPerSecond > 0f
            ? maximumTargetSpeedMetersPerSecond
            : DefaultMaximumTargetSpeedMetersPerSecond;

    private static float SplitPositive(float value) => Math.Max(Normalize(value), 0f);

    private static float SplitNegative(float value) => Math.Max(-Normalize(value), 0f);

    private static float Normalize(float value) => float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

    private static float NormalizeVelocity(float value) => float.IsFinite(value) ? value : 0f;
}
