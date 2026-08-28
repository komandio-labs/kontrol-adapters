using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.Library.Mathematics;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Client-only raw joystick command used by effects and audio, never physics.</summary>
internal static class TranslationPresentationState
{
    private const long MaximumPresentationAgeMilliseconds = 250;
    private static readonly Lock Gate = new();
    private static int _gridHashCode;
    private static bool _hasValue;
    private static long _lastUpdateTick;
    private static VoluntaryThrustData _value;

    internal static void Set(DEntity grid, Quaternion observerOrientation, float surge, float sway, float heave)
    {
        Vector3 observerLocal = new(Clamp(sway), Clamp(heave), -Clamp(surge));
        Vector3 entityLocal = observerOrientation * observerLocal;
        if (!IsFinite(entityLocal)) { Reset(); return; }

        lock (Gate)
        {
            _gridHashCode = grid.GetHashCode();
            _value = new VoluntaryThrustData { VoluntaryThrust = entityLocal };
            _lastUpdateTick = Environment.TickCount64;
            _hasValue = true;
        }
    }

    internal static bool TryGet(DEntity grid, out VoluntaryThrustData value) => TryGet(grid.GetHashCode(), out value);

    internal static bool TryGet(int gridHashCode, out VoluntaryThrustData value) =>
        TryGet(gridHashCode, Environment.TickCount64, out value);

    internal static bool TryGet(int gridHashCode, long nowTick, out VoluntaryThrustData value)
    {
        lock (Gate)
        {
            if (_hasValue && _gridHashCode == gridHashCode)
            {
                // SE2 can stop refreshing cockpit input after a stick returns to
                // center. Do not relinquish the presentation hook in that case:
                // its native cache can still contain the previous high-thrust
                // physical command. Replace the stale raw command with zero
                // until the next input refresh or an explicit lifecycle reset.
                if (nowTick - _lastUpdateTick > MaximumPresentationAgeMilliseconds)
                {
                    _value = default;
                    _lastUpdateTick = nowTick;
                }

                value = _value;
                return true;
            }
        }
        value = default;
        return false;
    }

    internal static void Reset()
    {
        lock (Gate) { _gridHashCode = 0; _value = default; _lastUpdateTick = 0; _hasValue = false; }
    }

    internal static void SetForTests(int gridHashCode, Quaternion observerOrientation, float surge, float sway, float heave, long updateTick = 0)
    {
        Vector3 observerLocal = new(Clamp(sway), Clamp(heave), -Clamp(surge));
        lock (Gate)
        {
            _gridHashCode = gridHashCode;
            _value = new VoluntaryThrustData { VoluntaryThrust = observerOrientation * observerLocal };
            _lastUpdateTick = updateTick == 0 ? Environment.TickCount64 : updateTick;
            _hasValue = true;
        }
    }

    internal static VoluntaryThrustData CreateForTests(Quaternion observerOrientation, float surge, float sway, float heave) =>
        new() { VoluntaryThrust = observerOrientation * new Vector3(Clamp(sway), Clamp(heave), -Clamp(surge)) };

    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
