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
        float maximumTargetSpeedMetersPerSecond) =>
        (SplitPositive(ComputeAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeAxis(surge, actualSurge, maximumTargetSpeedMetersPerSecond)),
         SplitPositive(ComputeAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeAxis(sway, actualSway, maximumTargetSpeedMetersPerSecond)),
         SplitPositive(ComputeAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond)),
         SplitNegative(ComputeAxis(heave, actualHeave, maximumTargetSpeedMetersPerSecond)));

    internal static float ComputeAxis(float input, float actualVelocity, float maximumTargetSpeedMetersPerSecond)
    {
        float axis = Normalize(input);
        float maximumSpeed = NormalizeMaximumSpeed(maximumTargetSpeedMetersPerSecond);
        float targetVelocity = axis * maximumSpeed;
        float proportionalOutput = (targetVelocity - NormalizeVelocity(actualVelocity)) / maximumSpeed;
        float axisLimit = MathF.Abs(axis);
        return Math.Clamp(proportionalOutput, -axisLimit, axisLimit);
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
