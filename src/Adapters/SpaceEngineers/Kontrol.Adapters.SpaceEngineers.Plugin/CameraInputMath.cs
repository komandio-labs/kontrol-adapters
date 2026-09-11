using System;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    internal static class CameraInputMath
    {
        private const float ActiveAxisThreshold = 0.001f;
        // MyThirdPersonSpectator consumes mouse-delta-like rotation units. At a
        // 60 Hz update rate this preserves a five-times-faster camera response.
        private const float LookRatePerSecond = 300f;

        internal static bool IsActionActive(ulong states, int action) => (states & (1UL << action)) != 0;

        internal static bool IsCameraLookActive(ulong states) =>
            IsActionActive(states, SpaceEngineersControlLayout.HoldLookAroundAction) ||
            IsActionActive(states, SpaceEngineersControlLayout.ToggleLookAroundAction);

        internal static float ResolveShipAxis(float nativeAxis, float kontrolAxis, bool cameraLookActive) =>
            InputMerge.StrongerAxis(nativeAxis, cameraLookActive ? 0f : kontrolAxis);

        internal static float ResolveLookAxis(float dedicatedCameraAxis, float flightAxis)
        {
            // Look-around works immediately with the pilot's existing pitch/yaw
            // bindings. Dedicated camera bindings are optional overrides.
            return Math.Abs(dedicatedCameraAxis) > ActiveAxisThreshold ? dedicatedCameraAxis : flightAxis;
        }

        internal static float ApplyLookAxis(float axis, float sensitivity, double elapsedSeconds)
        {
            if (Math.Abs(axis) <= ActiveAxisThreshold || elapsedSeconds <= 0d) return 0f;
            float boundedAxis = Math.Max(-1f, Math.Min(1f, axis));
            float boundedSensitivity = Math.Max(0.1f, Math.Min(10f, sensitivity));
            double boundedSeconds = Math.Min(0.1d, elapsedSeconds);
            return boundedAxis * boundedSensitivity * LookRatePerSecond * (float)boundedSeconds;
        }
    }
}
