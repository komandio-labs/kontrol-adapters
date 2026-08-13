using System.Reflection;
using HarmonyLib;
using Keen.Game2.Client.GameSystems.CameraSystems;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>
/// Caches SE2's active camera system, then processes the semantic camera-mode
/// action from the cockpit's continuously running input-commit path. SE2's
/// camera update method is event-driven, so it cannot be used to consume
/// adapter-only input reliably.
/// </summary>
[HarmonyPatch]
internal static class CameraActionPatch
{
    private const int CameraModeSwitchBit = 13;
    private static readonly MethodInfo? ToggleCameraViewMethod =
        AccessTools.DeclaredMethod(typeof(CameraSystemComponent), "ToggleCameraView");
    private static bool _missingMethodReported;
    private static CameraSystemComponent? _activeCameraSystem;

    [HarmonyPatch(typeof(CameraSystemComponent), "Init")]
    [HarmonyPostfix]
    private static void CaptureCameraSystem(CameraSystemComponent __instance)
    {
        _activeCameraSystem = __instance;
        SpaceEngineers2AdapterDiagnostics.WriteDebug("Kontrol captured the active SE2 camera system.");
    }

    internal static void ProcessCameraModeSwitch(ulong newActions)
    {
        if ((newActions & (1UL << CameraModeSwitchBit)) == 0)
        {
            return;
        }

        try
        {
            if (ToggleCameraViewMethod is null)
            {
                if (_missingMethodReported) return;
                _missingMethodReported = true;
                SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol received Camera Mode Switch, but SE2's camera toggle method was not found.");
                return;
            }

            if (_activeCameraSystem is null)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol received Camera Mode Switch before SE2's camera system was ready.");
                return;
            }

            ToggleCameraViewMethod.Invoke(_activeCameraSystem, null);
            SpaceEngineers2AdapterDiagnostics.WriteDebug("Kontrol invoked SE2 Camera Mode Switch through 'ToggleCameraView'.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("SE2 rejected the Kontrol Camera Mode Switch action.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 Camera Mode Switch error: {ex.InnerException}");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol could not invoke SE2 Camera Mode Switch.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 Camera Mode Switch error: {ex}");
        }
    }

}
