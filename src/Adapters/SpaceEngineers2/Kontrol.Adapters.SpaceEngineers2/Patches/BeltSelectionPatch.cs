using Keen.Game2.Client.UI.HUD.Toolbar;
using Keen.Game2.Client.UI.InGame;
using HarmonyLib;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>
/// Bridges Kontrol's cockpit belt actions to SE2's existing toolbar selection path.
/// </summary>
[HarmonyPatch]
internal static class BeltSelectionPatch
{
    internal const int FirstActionBit = 17;
    internal const int ActionCount = 10;
    private static readonly object Gate = new();
    private static readonly List<WeakReference<ToolbarScreenViewModel>> Toolbars = [];

    internal static bool TryGetTileIndex(int actionBit, out int tileIndex)
    {
        tileIndex = actionBit - FirstActionBit;
        return (uint)tileIndex < ActionCount;
    }

    internal static void Process(ulong newActions)
    {
        for (int actionBit = FirstActionBit; actionBit < FirstActionBit + ActionCount; actionBit++)
        {
            if ((newActions & (1UL << actionBit)) == 0) continue;
            if (!TryGetTileIndex(actionBit, out int tileIndex)) continue;

            if (!TrySelectTile(tileIndex))
            {
                SpaceEngineers2AdapterDiagnostics.WriteDebug(
                    $"SE2 belt action bit {actionBit} was received, but no active in-game toolbar could select slot {tileIndex + 1}.");
            }
        }
    }

    internal static bool TrySelectTile(int tileIndex)
    {
        if (tileIndex < 0) return false;

        lock (Gate)
        {
            for (int i = Toolbars.Count - 1; i >= 0; i--)
            {
                if (!Toolbars[i].TryGetTarget(out ToolbarScreenViewModel? toolbar))
                {
                    Toolbars.RemoveAt(i);
                    continue;
                }

                if (!toolbar.Visible || toolbar.ToolbarTileViewModels.Count <= tileIndex) continue;

                toolbar.SelectTile(tileIndex, explicitSelection: true);
                return true;
            }
        }

        return false;
    }

    internal static void Reset()
    {
        lock (Gate)
        {
            Toolbars.Clear();
        }
    }

    private static void Register(ToolbarScreenViewModel toolbar)
    {
        lock (Gate)
        {
            for (int i = Toolbars.Count - 1; i >= 0; i--)
            {
                if (!Toolbars[i].TryGetTarget(out ToolbarScreenViewModel? existing))
                {
                    Toolbars.RemoveAt(i);
                }
                else if (ReferenceEquals(existing, toolbar))
                {
                    return;
                }
            }

            Toolbars.Add(new WeakReference<ToolbarScreenViewModel>(toolbar));
        }
    }

    private static void Unregister(ToolbarScreenViewModel toolbar)
    {
        lock (Gate)
        {
            for (int i = Toolbars.Count - 1; i >= 0; i--)
            {
                if (!Toolbars[i].TryGetTarget(out ToolbarScreenViewModel? existing) || ReferenceEquals(existing, toolbar))
                {
                    Toolbars.RemoveAt(i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SessionInGameUISessionComponent), nameof(SessionInGameUISessionComponent.OpenToolbar))]
    [HarmonyPostfix]
    private static void OpenToolbarPostfix(ref IToolbarController controller)
    {
        if (controller is ToolbarScreenViewModel toolbar)
        {
            Register(toolbar);
        }
    }

    [HarmonyPatch(typeof(ToolbarScreenViewModel), nameof(ToolbarScreenViewModel.Dispose))]
    [HarmonyPrefix]
    private static void DisposePrefix(ToolbarScreenViewModel __instance)
    {
        Unregister(__instance);
    }
}
