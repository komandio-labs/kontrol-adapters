using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.Library.Mathematics;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Client-only raw joystick command used by effects and audio, never physics.</summary>
internal static class TranslationPresentationState
{
    private static readonly Lock Gate = new();
    private static int _gridHashCode;
    private static bool _hasValue;
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
            _hasValue = true;
        }
    }

    internal static bool TryGet(DEntity grid, out VoluntaryThrustData value) => TryGet(grid.GetHashCode(), out value);

    internal static bool TryGet(int gridHashCode, out VoluntaryThrustData value)
    {
        lock (Gate)
        {
            if (_hasValue && _gridHashCode == gridHashCode) { value = _value; return true; }
        }
        value = default;
        return false;
    }

    internal static void Reset()
    {
        lock (Gate) { _gridHashCode = 0; _value = default; _hasValue = false; }
    }

    internal static void SetForTests(int gridHashCode, Quaternion observerOrientation, float surge, float sway, float heave)
    {
        Vector3 observerLocal = new(Clamp(sway), Clamp(heave), -Clamp(surge));
        lock (Gate)
        {
            _gridHashCode = gridHashCode;
            _value = new VoluntaryThrustData { VoluntaryThrust = observerOrientation * observerLocal };
            _hasValue = true;
        }
    }

    internal static VoluntaryThrustData CreateForTests(Quaternion observerOrientation, float surge, float sway, float heave) =>
        new() { VoluntaryThrust = observerOrientation * new Vector3(Clamp(sway), Clamp(heave), -Clamp(surge)) };

    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
