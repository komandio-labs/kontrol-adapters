namespace Kontrol.Adapters.SpaceEngineers2.Patches;

internal sealed class CruiseControlState
{
    internal const long DoubleClickWindowMilliseconds = 350;

    private bool _isActive;
    private float _targetSpeedMetersPerSecond;
    private long _lastSetClickTick = long.MinValue;

    internal bool IsActive => _isActive;

    internal bool IsFastRetargeting { get; private set; }

    internal float TargetSpeedMetersPerSecond => _targetSpeedMetersPerSecond;

    internal CruiseSetResult SetOrReset(float currentForwardSpeedMetersPerSecond, long nowTick)
    {
        if (_lastSetClickTick != long.MinValue && nowTick - _lastSetClickTick <= DoubleClickWindowMilliseconds)
        {
            Reset();
            return CruiseSetResult.Reset;
        }

        _lastSetClickTick = nowTick;
        if (!float.IsFinite(currentForwardSpeedMetersPerSecond) || currentForwardSpeedMetersPerSecond <= 0f)
        {
            return CruiseSetResult.Ignored;
        }

        _targetSpeedMetersPerSecond = NormalizeTargetSpeed(currentForwardSpeedMetersPerSecond);
        _isActive = true;
        IsFastRetargeting = false;
        return CruiseSetResult.Set;
    }

    internal bool AdjustTarget(float delta)
    {
        if (!_isActive || !float.IsFinite(delta) || delta == 0f) return false;

        float adjustedTarget = NormalizeTargetSpeed(_targetSpeedMetersPerSecond + delta);
        if (adjustedTarget == _targetSpeedMetersPerSecond) return false;

        _targetSpeedMetersPerSecond = adjustedTarget;
        IsFastRetargeting = true;
        return true;
    }

    internal void CompleteFastRetarget() => IsFastRetargeting = false;

    internal void CancelForBrake()
    {
        if (!_isActive) return;

        Reset();
    }

    internal void Reset()
    {
        _isActive = false;
        IsFastRetargeting = false;
        _targetSpeedMetersPerSecond = 0f;
        _lastSetClickTick = long.MinValue;
    }

    private static float NormalizeTargetSpeed(float speed) =>
        float.IsFinite(speed) ? Math.Max(speed, 0f) : 0f;
}

/// <summary>
/// Repeats a held Cruise Control adjustment button with a small initial delay
/// and familiar digital-clock-style step acceleration.
/// </summary>
internal sealed class CruiseAdjustmentRepeater
{
    internal const long InitialRepeatDelayMilliseconds = 350;
    internal const long RepeatIntervalMilliseconds = 125;
    internal const long FiveMeterStepDelayMilliseconds = 750;
    internal const long TenMeterStepDelayMilliseconds = 1_500;

    private int _direction;
    private long _startedAtTick;
    private long _nextRepeatTick;

    internal float Update(bool increaseHeld, bool decreaseHeld, long nowTick)
    {
        int direction = increaseHeld == decreaseHeld ? 0 : increaseHeld ? 1 : -1;
        if (direction == 0)
        {
            Reset();
            return 0f;
        }

        if (direction != _direction)
        {
            _direction = direction;
            _startedAtTick = nowTick;
            _nextRepeatTick = nowTick + InitialRepeatDelayMilliseconds;
            return direction;
        }

        if (nowTick < _nextRepeatTick) return 0f;

        _nextRepeatTick = nowTick + RepeatIntervalMilliseconds;
        long heldMilliseconds = nowTick - _startedAtTick;
        float step = heldMilliseconds >= TenMeterStepDelayMilliseconds
            ? 10f
            : heldMilliseconds >= FiveMeterStepDelayMilliseconds
                ? 5f
                : 1f;
        return direction * step;
    }

    internal void Reset()
    {
        _direction = 0;
        _startedAtTick = 0;
        _nextRepeatTick = 0;
    }
}

internal enum CruiseSetResult
{
    Set,
    Reset,
    Ignored
}
