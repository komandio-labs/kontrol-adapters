using System;

namespace Kontrol.Adapters.VtolVR;

/// <summary>Applies a signed throttle axis as a rate of change in normalized throttle per second.</summary>
public static class ThrottleRateControl
{
    public static float Advance(float currentThrottle, float axis, float ratePerSecond, float deltaTime)
    {
        if (float.IsNaN(currentThrottle) || float.IsInfinity(currentThrottle)) currentThrottle = 0f;
        if (float.IsNaN(axis) || float.IsInfinity(axis)) axis = 0f;
        if (float.IsNaN(ratePerSecond) || float.IsInfinity(ratePerSecond) || ratePerSecond < 0f) ratePerSecond = 0f;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) deltaTime = 0f;

        float next = currentThrottle + Math.Max(-1f, Math.Min(1f, axis)) * ratePerSecond * deltaTime;
        return Math.Max(0f, Math.Min(1f, next));
    }
}
