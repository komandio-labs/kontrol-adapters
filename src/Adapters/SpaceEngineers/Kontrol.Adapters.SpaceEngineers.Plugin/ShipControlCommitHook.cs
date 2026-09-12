using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sandbox.Game;
using Sandbox.Engine.Utils;
using MyShipController = Sandbox.Game.Entities.MyShipController;
using VRage.Game.ModAPI.Interfaces;
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
        private static readonly MethodInfo ZoomPrefixMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ZoomPrefix));
        private static readonly MethodInfo ZoomPostfixMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ZoomPostfix));
        private static readonly MethodInfo ZoomLookAroundMethod = AccessTools.Method(
            typeof(MyInputExtensions),
            nameof(MyInputExtensions.IsLookAround),
            [typeof(IMyInput)]);
        private static readonly MethodInfo ResolveZoomLookAroundMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ResolveZoomLookAround));
        private static readonly MethodInfo ZoomControlAnalogMethod = AccessTools.Method(
            typeof(MyControllerHelper),
            nameof(MyControllerHelper.IsControlAnalog),
            [typeof(MyStringId), typeof(MyStringId), typeof(bool)]);
        private static readonly MethodInfo ResolveZoomControlAnalogMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(ResolveZoomControlAnalog));
        private static readonly FieldInfo ZoomInControlField = AccessTools.Field(
            typeof(MyControlsSpace),
            nameof(MyControlsSpace.CAMERA_ZOOM_IN));
        private static readonly FieldInfo ZoomOutControlField = AccessTools.Field(
            typeof(MyControlsSpace),
            nameof(MyControlsSpace.CAMERA_ZOOM_OUT));
        [ThreadStatic]
        private static bool _mergeKontrolZoomForCurrentUpdate;
        [ThreadStatic]
        private static bool _suppressZoomUpdate;
        private static SpaceEngineersPlugin _plugin;
        private static bool _installed;

        internal static void Install(SpaceEngineersPlugin plugin)
        {
            if (_installed)
            {
                _plugin = plugin;
                return;
            }

            if (TargetMethod == null || PrefixMethod == null || ZoomTargetMethod == null ||
                ZoomTranspilerMethod == null || ZoomPrefixMethod == null || ZoomPostfixMethod == null ||
                ZoomLookAroundMethod == null || ResolveZoomLookAroundMethod == null ||
                ZoomControlAnalogMethod == null || ResolveZoomControlAnalogMethod == null ||
                ZoomInControlField == null || ZoomOutControlField == null)
                throw new MissingMethodException("Could not locate Space Engineers' ship-control or third-person camera zoom commit.");

            Harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PrefixMethod));
            Harmony.Patch(
                ZoomTargetMethod,
                prefix: new HarmonyMethod(ZoomPrefixMethod),
                postfix: new HarmonyMethod(ZoomPostfixMethod),
                transpiler: new HarmonyMethod(ZoomTranspilerMethod));
            var patchInfo = Harmony.GetPatchInfo(TargetMethod);
            var zoomPatchInfo = Harmony.GetPatchInfo(ZoomTargetMethod);
            if (patchInfo == null || !patchInfo.Prefixes.Any(prefix => prefix.owner == HarmonyId) ||
                zoomPatchInfo == null ||
                !zoomPatchInfo.Prefixes.Any(prefix => prefix.owner == HarmonyId) ||
                !zoomPatchInfo.Postfixes.Any(postfix => postfix.owner == HarmonyId) ||
                !zoomPatchInfo.Transpilers.Any(transpiler => transpiler.owner == HarmonyId))
                throw new InvalidOperationException("Harmony did not register Kontrol's Space Engineers control patches.");

            PulsarStartupTrace.Write("Native third-person zoom hook verified: one look-around gate and two directional analog reads.");
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

        internal static void RotateCameraWithoutZoom(
            IMyCameraController cameraController,
            Vector2 rotationIndicator)
        {
            // MyCockpit.Rotate calls MyThirdPersonSpectator.Rotate, whose only
            // operation in this SE1 build is another UpdateZoom. Suppress that
            // nested update so one gameplay frame applies zoom exactly once.
            bool previousSuppression = _suppressZoomUpdate;
            _suppressZoomUpdate = true;
            try
            {
                cameraController.Rotate(rotationIndicator, 0f);
            }
            finally
            {
                _suppressZoomUpdate = previousSuppression;
            }
        }

        private static void Prefix(MyShipController __instance, ref Vector3 moveIndicator, ref Vector2 rotationIndicator, ref float rollIndicator)
        {
            var plugin = _plugin;
            if (plugin != null)
                plugin.MergeAtFinalControlCommit(__instance, ref moveIndicator, ref rotationIndicator, ref rollIndicator);
        }

        private static IEnumerable<CodeInstruction> ZoomTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var rewritten = instructions.ToList();
            int lookAroundReplacements = 0;
            int zoomInReplacements = 0;
            int zoomOutReplacements = 0;
            for (int index = 0; index < rewritten.Count; index++)
            {
                var instruction = rewritten[index];
                if (instruction.Calls(ZoomLookAroundMethod))
                {
                    instruction.operand = ResolveZoomLookAroundMethod;
                    lookAroundReplacements++;
                    continue;
                }

                if (instruction.Calls(ZoomControlAnalogMethod))
                {
                    if (index < 2 || rewritten[index - 1].opcode != OpCodes.Ldc_I4_0 ||
                        !IsZoomControlField(rewritten[index - 2]))
                        throw new MissingMethodException("Space Engineers' third-person zoom analog call shape changed.");

                    instruction.operand = ResolveZoomControlAnalogMethod;
                    if (Equals(rewritten[index - 2].operand, ZoomInControlField))
                        zoomInReplacements++;
                    else
                        zoomOutReplacements++;
                }
            }

            if (lookAroundReplacements != 1 || zoomInReplacements != 1 || zoomOutReplacements != 1)
                throw new MissingMethodException(string.Format(
                    "Expected one look-around gate and one native analog read per zoom direction; found look-around={0}, zoom-in={1}, zoom-out={2}.",
                    lookAroundReplacements,
                    zoomInReplacements,
                    zoomOutReplacements));

            return rewritten;
        }

        private static bool ZoomPrefix()
        {
            _mergeKontrolZoomForCurrentUpdate = false;
            if (_suppressZoomUpdate)
                return false;

            var plugin = _plugin;
            _mergeKontrolZoomForCurrentUpdate = plugin != null && plugin.ShouldMergeKontrolCameraZoom();
            return true;
        }

        private static void ZoomPostfix()
        {
            _mergeKontrolZoomForCurrentUpdate = false;
        }

        private static bool ResolveZoomLookAround(IMyInput input)
        {
            return input.IsLookAround() || _mergeKontrolZoomForCurrentUpdate;
        }

        private static float ResolveZoomControlAnalog(MyStringId context, MyStringId control, bool joystick)
        {
            float nativeValue = MyControllerHelper.IsControlAnalog(context, control, joystick);
            var plugin = _plugin;
            return plugin == null || !_mergeKontrolZoomForCurrentUpdate
                ? nativeValue
                : plugin.MergeNativeCameraZoom(context, control, nativeValue);
        }

        private static bool IsZoomControlField(CodeInstruction instruction)
        {
            if (instruction.opcode != OpCodes.Ldsfld)
                return false;

            return Equals(instruction.operand, ZoomInControlField) ||
                   Equals(instruction.operand, ZoomOutControlField);
        }
    }
}
