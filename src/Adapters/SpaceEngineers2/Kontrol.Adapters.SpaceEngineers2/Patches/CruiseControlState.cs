namespace Kontrol.Adapters.SpaceEngineers2.Patches;

internal sealed class CruiseControlState
{
    internal const float TargetStepMetersPerSecond = 10f;
    internal const long DoubleClickWindowMilliseconds = 350;

    private bool _isActive;
    private float _targetSpeedMetersPerSecond;
    private long _lastSetClickTick = long.MinValue;

    internal bool IsActive => _isActive;

    internal float TargetSpeedMetersPerSecond => _targetSpeedMetersPerSecond;

    internal CruiseSetResult SetOrReset(float currentForwardSpeedMetersPerSecond, long nowTick)
    {
        if (_lastSetClickTick != long.MinValue && nowTick - _lastSetClickTick <= DoubleClickWindowMilliseconds)
        {
            Reset();
            return CruiseSetResult.Reset;
        }

        _lastSetClickTick = nowTick;
        _targetSpeedMetersPerSecond = NormalizeTargetSpeed(currentForwardSpeedMetersPerSecond);
        _isActive = true;
        return CruiseSetResult.Set;
    }

    internal void IncreaseTarget() => AdjustTarget(TargetStepMetersPerSecond);

    internal void DecreaseTarget() => AdjustTarget(-TargetStepMetersPerSecond);

    internal void CancelForBrake()
    {
        if (!_isActive) return;

        Reset();
    }

    internal void Reset()
    {
        _isActive = false;
        _targetSpeedMetersPerSecond = 0f;
        _lastSetClickTick = long.MinValue;
    }

    private void AdjustTarget(float delta)
    {
        if (!_isActive) return;

        _targetSpeedMetersPerSecond = NormalizeTargetSpeed(_targetSpeedMetersPerSecond + delta);
    }

    private static float NormalizeTargetSpeed(float speed) =>
        float.IsFinite(speed) ? Math.Max(speed, 0f) : 0f;
}

internal enum CruiseSetResult
{
    Set,
    Reset
}
