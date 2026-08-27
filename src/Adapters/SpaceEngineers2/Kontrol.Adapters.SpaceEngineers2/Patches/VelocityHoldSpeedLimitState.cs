namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Latest verified SE2 grid speed limit, used only for UI presentation.</summary>
internal static class VelocityHoldSpeedLimitState
{
    private static float _maximumMetersPerSecond;

    internal static float Current => Volatile.Read(ref _maximumMetersPerSecond);

    internal static void Set(float value) => Volatile.Write(
        ref _maximumMetersPerSecond,
        float.IsFinite(value) && value > 0f ? value : 0f);
}
