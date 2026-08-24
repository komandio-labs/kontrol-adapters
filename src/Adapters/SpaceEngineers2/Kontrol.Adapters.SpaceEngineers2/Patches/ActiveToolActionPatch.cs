using System.Reflection;
using HarmonyLib;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.BlockTools;
// ReSharper disable InconsistentNaming

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>
/// Bridges Kontrol's held primary-fire state to whichever SE2 weapon/tool input
/// handler is active. Native mouse input is retained and ORed with Kontrol so
/// either input source can press or release independently.
/// </summary>
[HarmonyPatch]
internal static class ActiveToolActionPatch
{
    private static readonly Lock Sync = new();
    private static object? _activeHandler;
    private static MethodInfo? _primaryMethod;
    private static MethodInfo? _secondaryMethod;
    private static bool _nativePrimaryPressed;
    private static bool _kontrolPrimaryPressed;
    private static bool _lastPrimaryDelivered;
    private static bool _nativeSecondaryPressed;
    private static bool _kontrolSecondaryPressed;
    private static bool _lastSecondaryDelivered;

    [ThreadStatic]
    private static bool _injecting;

    [HarmonyPatch(typeof(InputHandlerBaseComponent), "Activate")]
    [HarmonyPostfix]
    private static void ActivatePostfix(InputHandlerBaseComponent __instance)
    {
        var primaryMethod = ResolvePrimaryMethod(__instance);
        if (primaryMethod is null) return;

        lock (Sync)
        {
            _activeHandler = __instance;
            _primaryMethod = primaryMethod;
            _secondaryMethod = ResolveSecondaryMethod(__instance);
            _nativePrimaryPressed = false;
            _lastPrimaryDelivered = false;
            _nativeSecondaryPressed = false;
            _lastSecondaryDelivered = false;
        }

        SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 activated primary-action handler '{__instance.GetType().Name}'.");
    }

    [HarmonyPatch(typeof(InputHandlerBaseComponent), "Deactivate")]
    [HarmonyPrefix]
    private static void DeactivatePrefix(InputHandlerBaseComponent __instance)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_activeHandler, __instance)) return;
            bool releasePrimary = _lastPrimaryDelivered;
            bool releaseSecondary = _lastSecondaryDelivered;
            _kontrolPrimaryPressed = false;
            _nativePrimaryPressed = false;
            _kontrolSecondaryPressed = false;
            _nativeSecondaryPressed = false;
            if (releasePrimary) InvokeActive(_primaryMethod, false, "primary fire");
            if (releaseSecondary) InvokeActive(_secondaryMethod, false, "reload");

            _activeHandler = null;
            _primaryMethod = null;
            _secondaryMethod = null;
            _nativePrimaryPressed = false;
            _lastPrimaryDelivered = false;
            _nativeSecondaryPressed = false;
            _lastSecondaryDelivered = false;
        }
    }

    [HarmonyPatch(typeof(BlockToolInputHandlerBaseComponent), "PrimaryAction")]
    [HarmonyPrefix]
    private static void BlockPrimaryPrefix(BlockToolInputHandlerBaseComponent __instance, ref bool value) =>
        MergeNativeState(__instance, ref value, secondary: false);

    [HarmonyPatch(typeof(BlockToolInputHandlerBaseComponent), "SecondaryAction")]
    [HarmonyPrefix]
    private static void BlockSecondaryPrefix(BlockToolInputHandlerBaseComponent __instance, ref bool value) =>
        MergeNativeState(__instance, ref value, secondary: true);

    [HarmonyPatch(typeof(AutomatedWeaponInputHandlerComponent), "Shoot")]
    [HarmonyPrefix]
    private static void AutomatedWeaponShootPrefix(AutomatedWeaponInputHandlerComponent __instance, ref bool value) =>
        MergeNativeState(__instance, ref value, secondary: false);

    internal static void ApplyPrimaryFire(bool pressed)
    {
        lock (Sync)
        {
            _kontrolPrimaryPressed = pressed;
            if (_activeHandler is null || _primaryMethod is null) return;

            bool combined = _nativePrimaryPressed || _kontrolPrimaryPressed;
            if (combined == _lastPrimaryDelivered) return;
            InvokeActive(_primaryMethod, combined, "primary fire");
        }
    }

    internal static void ApplyReload(bool pressed)
    {
        lock (Sync)
        {
            _kontrolSecondaryPressed = pressed;
            if (_activeHandler is null || _secondaryMethod is null) return;

            bool combined = _nativeSecondaryPressed || _kontrolSecondaryPressed;
            if (combined == _lastSecondaryDelivered) return;
            InvokeActive(_secondaryMethod, combined, "reload");
        }
    }

    private static void MergeNativeState(object instance, ref bool value, bool secondary)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_activeHandler, instance)) return;
            if (secondary)
            {
                if (!_injecting) _nativeSecondaryPressed = value;
                value = _nativeSecondaryPressed || _kontrolSecondaryPressed;
                _lastSecondaryDelivered = value;
            }
            else
            {
                if (!_injecting) _nativePrimaryPressed = value;
                value = _nativePrimaryPressed || _kontrolPrimaryPressed;
                _lastPrimaryDelivered = value;
            }
        }
    }

    private static void InvokeActive(MethodInfo? method, bool value, string actionName)
    {
        if (_activeHandler is null || method is null) return;

        try
        {
            _injecting = true;
            var parameters = method.GetParameters();
            if (parameters.Length != 2 || !parameters[1].ParameterType.IsEnum)
                throw new MissingMethodException($"SE2 {actionName} handler '{method.Name}' has an unsupported signature.");
            method.Invoke(_activeHandler,
            [
                value,
                Enum.ToObject(parameters[1].ParameterType, value ? 0 : 2)
            ]);
            if (actionName == "reload") _lastSecondaryDelivered = value;
            else _lastPrimaryDelivered = value;
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Kontrol {actionName} changed to {value} through '{_activeHandler.GetType().Name}'.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError($"SE2 rejected the Kontrol {actionName} action.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 {actionName} error: {ex.InnerException}");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError($"Kontrol could not invoke SE2 {actionName}.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 {actionName} error: {ex}");
        }
        finally
        {
            _injecting = false;
        }
    }

    private static MethodInfo? ResolvePrimaryMethod(object instance) => instance switch
    {
        BlockToolInputHandlerBaseComponent => AccessTools.DeclaredMethod(typeof(BlockToolInputHandlerBaseComponent), "PrimaryAction"),
        AutomatedWeaponInputHandlerComponent => AccessTools.DeclaredMethod(typeof(AutomatedWeaponInputHandlerComponent), "Shoot"),
        _ => null
    };

    private static MethodInfo? ResolveSecondaryMethod(object instance) => instance switch
    {
        BlockToolInputHandlerBaseComponent => AccessTools.DeclaredMethod(typeof(BlockToolInputHandlerBaseComponent), "SecondaryAction"),
        _ => null
    };
}
