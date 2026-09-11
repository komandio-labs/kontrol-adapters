using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Sandbox.Game;
using Sandbox.Engine.Utils;
using MyShipController = Sandbox.Game.Entities.MyShipController;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    /// <summary>
    /// Merges Kontrol into the exact native movement arguments SE1 is about to consume.
    /// </summary>
    internal static class ShipControlCommitHook
    {
        private const string HarmonyId = "Kontrol.Adapters.SpaceEngineers";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly MethodInfo TargetMethod = AccessTools.Method(
            typeof(MyShipController),
            nameof(MyShipController.MoveAndRotate),
            [typeof(Vector3), typeof(Vector2), typeof(float)]);
        private static readonly MethodInfo PrefixMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(Prefix));
        private static readonly MethodInfo ZoomTargetMethod = AccessTools.DeclaredMethod(
            typeof(MyThirdPersonSpectator),
            nameof(MyThirdPersonSpectator.UpdateZoom),
            Type.EmptyTypes);
        private static readonly MethodInfo ZoomTranspilerMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ZoomTranspiler));
        private static readonly MethodInfo ZoomControlAnalogMethod = AccessTools.Method(
            typeof(MyControllerHelper),
            nameof(MyControllerHelper.IsControlAnalog),
            [typeof(MyStringId), typeof(MyStringId), typeof(bool)]);
        private static readonly MethodInfo ResolveZoomControlAnalogMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ResolveZoomControlAnalog));
        private static SpaceEngineersPlugin _plugin;
        private static bool _installed;

        internal static void Install(SpaceEngineersPlugin plugin)
        {
            if (_installed)
            {
                _plugin = plugin;
                return;
            }

            if (TargetMethod == null || PrefixMethod == null || ZoomTargetMethod == null || ZoomTranspilerMethod == null || ZoomControlAnalogMethod == null || ResolveZoomControlAnalogMethod == null)
                throw new MissingMethodException("Could not locate Space Engineers' ship-control or third-person camera zoom commit.");

            Harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PrefixMethod));
            Harmony.Patch(ZoomTargetMethod, transpiler: new HarmonyMethod(ZoomTranspilerMethod));
            var patchInfo = Harmony.GetPatchInfo(TargetMethod);
            var zoomPatchInfo = Harmony.GetPatchInfo(ZoomTargetMethod);
            if (patchInfo == null || !patchInfo.Prefixes.Any(prefix => prefix.owner == HarmonyId) ||
                zoomPatchInfo == null || !zoomPatchInfo.Transpilers.Any(transpiler => transpiler.owner == HarmonyId))
                throw new InvalidOperationException("Harmony did not register Kontrol's Space Engineers control patches.");

            _plugin = plugin;
            _installed = true;
        }

        internal static void Uninstall(SpaceEngineersPlugin plugin)
        {
            if (!ReferenceEquals(_plugin, plugin)) return;

            _plugin = null;
            if (_installed)
                Harmony.UnpatchAll(HarmonyId);
            _installed = false;
        }

        private static void Prefix(MyShipController __instance, ref Vector3 moveIndicator, ref Vector2 rotationIndicator, ref float rollIndicator)
        {
            var plugin = _plugin;
            if (plugin != null)
                plugin.MergeAtFinalControlCommit(__instance, ref moveIndicator, ref rotationIndicator, ref rollIndicator);
        }

        private static IEnumerable<CodeInstruction> ZoomTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(ZoomControlAnalogMethod))
                {
                    instruction.operand = ResolveZoomControlAnalogMethod;
                    replaced = true;
                }
                yield return instruction;
            }

            if (!replaced)
                throw new MissingMethodException("Could not replace Space Engineers' native third-person zoom analog reads.");
        }

        private static float ResolveZoomControlAnalog(MyStringId context, MyStringId control, bool joystick)
        {
            float nativeValue = MyControllerHelper.IsControlAnalog(context, control, joystick);
            var plugin = _plugin;
            return plugin == null ? nativeValue : plugin.MergeNativeCameraZoom(context, control, nativeValue);
        }
    }
}
