using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MyShipController = Sandbox.Game.Entities.MyShipController;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    /// <summary>
    /// Applies Kontrol's latest frame at SE1's final ship-control commit, after native input has populated the controller.
    /// </summary>
    internal static class ShipControlCommitHook
    {
        private const string HarmonyId = "Kontrol.Adapters.SpaceEngineers";
        private static readonly Harmony Harmony = new Harmony(HarmonyId);
        private static readonly MethodInfo TargetMethod = AccessTools.DeclaredMethod(
            typeof(MyShipController),
            nameof(MyShipController.MoveAndRotate),
            Type.EmptyTypes);
        private static readonly MethodInfo PrefixMethod = AccessTools.DeclaredMethod(
            typeof(ShipControlCommitHook),
            nameof(Prefix));
        private static SpaceEngineersPlugin _plugin;
        private static bool _installed;

        internal static void Install(SpaceEngineersPlugin plugin)
        {
            if (_installed)
            {
                _plugin = plugin;
                return;
            }

            if (TargetMethod == null || PrefixMethod == null)
                throw new MissingMethodException("Could not locate Space Engineers' final MyShipController.MoveAndRotate() control commit.");

            Harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PrefixMethod));
            var patchInfo = Harmony.GetPatchInfo(TargetMethod);
            if (patchInfo == null || !patchInfo.Prefixes.Any(prefix => prefix.owner == HarmonyId))
                throw new InvalidOperationException("Harmony did not register Kontrol's Space Engineers ship-control prefix.");

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

        private static void Prefix(MyShipController __instance)
        {
            var plugin = _plugin;
            if (plugin != null)
                plugin.ApplyAtFinalControlCommit(__instance);
        }
    }
}
