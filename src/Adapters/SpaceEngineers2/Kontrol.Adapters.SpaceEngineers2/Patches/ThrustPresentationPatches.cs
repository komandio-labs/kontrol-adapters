using System.Reflection;
using HarmonyLib;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Physics.Data;

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
    // ThrustAudioData is a presence gate for SE2's per-frame velocity audio
    // update, not the audio intensity itself. Keep the gate present while the
    // controlled grid has presentation state so UpdateThrustAudio can drive
    // the live event all the way down to zero after input release.
    private const float AudioUpdateKeepAliveMagnitude = .0001f;

    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.WorldObjects.Shared.Movement.Effects.ThrustEffectsComponent");
        return type is null ? null : AccessTools.DeclaredMethod(type, "CheckThrustAudio");
    }

    private static void Prefix(
        DEntity entity,
        ref VoluntaryThrustData voluntaryThrustData,
        ref RigidBodyData rigidBodyData)
    {
        if (!TranslationPresentationState.TryGet(entity, out var presentation)) return;

        presentation.VoluntaryThrust = KeepAudioUpdateActive(presentation.VoluntaryThrust);
        voluntaryThrustData = presentation;
        rigidBodyData.LinearVelocity = KeepAudioUpdateActive(rigidBodyData.LinearVelocity);
    }

    internal static Vector3 KeepAudioUpdateActive(Vector3 value) =>
        value.LengthSquared() > 0f ? value : new Vector3(AudioUpdateKeepAliveMagnitude, 0f, 0f);
}

[HarmonyPatch]
internal static class ThrustDampeningPresentationPatch
{
    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.WorldObjects.Shared.Movement.Effects.ThrustEffectsComponent");
        return type is null ? null : AccessTools.DeclaredMethod(type, "UpdateDampening");
    }

    private static void Prefix(DEntity entity, ref VoluntaryThrustData voluntaryThrustData)
    {
        if (!TranslationPresentationState.TryGet(entity, out var presentation)) return;

        presentation.VoluntaryThrust = ThrustAudioPresentationPatch.KeepAudioUpdateActive(
            presentation.VoluntaryThrust);
        voluntaryThrustData = presentation;
    }
}
