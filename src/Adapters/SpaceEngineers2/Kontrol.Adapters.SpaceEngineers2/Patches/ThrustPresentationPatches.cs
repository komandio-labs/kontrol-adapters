using System.Reflection;
using HarmonyLib;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement;
using Keen.VRage.DCS.Accessors;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Client presentation hooks; SE2 physical VoluntaryThrustData is never written.</summary>
[HarmonyPatch]
internal static class ThrusterEffectsPresentationPatch
{
    private static MethodBase? TargetMethod() => FindCacheGetData("Keen.Game2.Client.WorldObjects.CubeBlocks.Effects.ThrusterEffectsComponent");
    private static void Postfix(DEntity grid, ref VoluntaryThrustData __result)
    {
        if (TranslationPresentationState.TryGet(grid, out var presentation)) __result = presentation;
    }
    private static MethodInfo? FindCacheGetData(string componentTypeName)
    {
        Type? component = AccessTools.TypeByName(componentTypeName);
        Type? context = component is null ? null : AccessTools.Inner(component, "ThrustDataCacheContext");
        Type? cache = context is null ? null : AccessTools.Inner(context, "ThrustDataCache");
        return cache is null ? null : AccessTools.DeclaredMethod(cache, "GetData");
    }
}

[HarmonyPatch]
internal static class ThrusterAnimatorPresentationPatch
{
    private static MethodBase? TargetMethod() => FindCacheGetData("Keen.Game2.Client.WorldObjects.CubeBlocks.Movement.ThrusterAnimatorComponent");
    private static void Postfix(DEntity grid, ref VoluntaryThrustData __result)
    {
        if (TranslationPresentationState.TryGet(grid, out var presentation)) __result = presentation;
    }
    private static MethodInfo? FindCacheGetData(string componentTypeName)
    {
        Type? component = AccessTools.TypeByName(componentTypeName);
        Type? context = component is null ? null : AccessTools.Inner(component, "ThrustDataCacheContext");
        Type? cache = context is null ? null : AccessTools.Inner(context, "ThrustDataCache");
        return cache is null ? null : AccessTools.DeclaredMethod(cache, "GetData");
    }
}

[HarmonyPatch]
internal static class ThrustAudioPresentationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.WorldObjects.Shared.Movement.Effects.ThrustEffectsComponent");
        return type is null
            ? Array.Empty<MethodBase>()
            : new MethodBase?[] { AccessTools.DeclaredMethod(type, "CheckThrustAudio"), AccessTools.DeclaredMethod(type, "UpdateDampening") }.OfType<MethodBase>();
    }
    private static void Prefix(DEntity entity, ref VoluntaryThrustData voluntaryThrustData)
    {
        if (TranslationPresentationState.TryGet(entity, out var presentation)) voluntaryThrustData = presentation;
    }
}
