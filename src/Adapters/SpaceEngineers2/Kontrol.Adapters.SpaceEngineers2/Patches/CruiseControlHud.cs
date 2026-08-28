using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using HarmonyLib;
using Keen.Game2.Client.UI.Library.Controls;
using Keen.VRage.UI.Shared.Controls.BeveledBorder;
using Kontrol.Adapters.SpaceEngineers2.Settings;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Extends live SE2 flight speedometers with a native-layout Cruise Control row.</summary>
internal static class CruiseControlHudManager
{
    private sealed class Host
    {
        internal required Control Speedometer { get; init; }
        internal required StackPanel NativeContent { get; init; }
        internal required ShadowedTextBlock CruiseRow { get; init; }
    }

    private static readonly Lock StateLock = new();
    private static readonly List<Host> Hosts = new();
    private static bool _cockpitActive;
    private static bool _uiRefreshQueued;
    private static string? _lastVisibilityTrace;

    internal static void Attach(Control speedometer, StackPanel nativeContent, ShadowedTextBlock nativeSpeedText)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Attach(speedometer, nativeContent, nativeSpeedText));
            return;
        }

        RemoveHostsExcept(speedometer);
        RemoveForSpeedometer(speedometer);
        var cruiseRow = new ShadowedTextBlock
        {
            Text = string.Empty,
            IsVisible = false,
            IsHitTestVisible = false,
            Focusable = false,
            ClipToBounds = false,
            HorizontalAlignment = nativeSpeedText.HorizontalAlignment,
            TextAlignment = nativeSpeedText.TextAlignment,
            FontFamily = nativeSpeedText.FontFamily,
            FontSize = nativeSpeedText.FontSize,
            FontStyle = nativeSpeedText.FontStyle,
            FontWeight = nativeSpeedText.FontWeight,
            Foreground = nativeSpeedText.Foreground,
            ShadowColor = nativeSpeedText.ShadowColor,
            ShadowOffset = nativeSpeedText.ShadowOffset
        };
        nativeContent.Children.Add(cruiseRow);
        Hosts.Add(new Host
        {
            Speedometer = speedometer,
            NativeContent = nativeContent,
            CruiseRow = cruiseRow
        });
        SpaceEngineers2AdapterDiagnostics.WriteDebug(
            $"[CruiseHudTrace] Extended native speedometer. hosts={Hosts.Count}; " +
            $"nativeRows={nativeContent.Children.Count}; speedometer={speedometer.Bounds.Width:F0}x{speedometer.Bounds.Height:F0}.");
        Refresh();
    }

    internal static void Refresh(bool? cockpitActive = null)
    {
        lock (StateLock)
        {
            if (cockpitActive.HasValue)
            {
                _cockpitActive = cockpitActive.Value;
            }

            if (_uiRefreshQueued)
            {
                return;
            }

            _uiRefreshQueued = true;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPendingRefresh();
            return;
        }

        try
        {
            Dispatcher.UIThread.Post(ApplyPendingRefresh);
        }
        catch (Exception ex)
        {
            lock (StateLock)
            {
                _uiRefreshQueued = false;
            }

            SpaceEngineers2AdapterDiagnostics.WriteDebug($"[CruiseHudTrace] Could not queue HUD refresh on the UI thread: {ex}");
        }
    }

    internal static bool ShouldShow(bool cockpitActive, bool settingEnabled, bool cruiseActive) =>
        cockpitActive && settingEnabled && cruiseActive;

    internal static void Hide() => Refresh(false);

    // A missing IPC frame can be a transient condition during a camera/HUD
    // transition. Preserve the current state instead of hiding the Cruise row;
    // the native Flight HUD parent still controls whether it is rendered.
    internal static void HideTransient() => Refresh();

    internal static void Clear()
    {
        lock (StateLock)
        {
            _cockpitActive = false;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ClearVisuals);
            return;
        }

        ClearVisuals();
    }

    private static void ApplyPendingRefresh()
    {
        bool cockpitActive;
        lock (StateLock)
        {
            _uiRefreshQueued = false;
            cockpitActive = _cockpitActive;
        }

        var settings = SpaceEngineers2SettingsManager.Instance;
        bool cruiseActive = CockpitInputPatch.IsCruiseControlActiveForHud;
        bool isActive = ShouldShow(cockpitActive, settings.ShowCruiseControlHudIndicator, cruiseActive);
        string visibilityTrace = $"hosts={Hosts.Count}; cockpitActive={cockpitActive}; settingEnabled={settings.ShowCruiseControlHudIndicator}; " +
                                 $"cruiseActive={cruiseActive}; visible={isActive}; uiThread={Dispatcher.UIThread.CheckAccess()}";
        if (!string.Equals(_lastVisibilityTrace, visibilityTrace, StringComparison.Ordinal))
        {
            _lastVisibilityTrace = visibilityTrace;
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"[CruiseHudTrace] {visibilityTrace}.");
        }
        string targetSpeed = isActive
            ? SpeedUnitPresentation.FormatHudTarget(CockpitInputPatch.CruiseControlTargetSpeedForHud, settings.SpeedDisplayUnit)
            : string.Empty;
        foreach (var host in Hosts)
        {
            host.CruiseRow.Text = isActive ? $"CRUISE {targetSpeed}" : string.Empty;
            host.CruiseRow.IsVisible = isActive;
        }
    }

    private static void ClearVisuals()
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            RemoveAt(i);
        }
        _lastVisibilityTrace = null;
    }

    private static void RemoveForSpeedometer(Control speedometer)
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(Hosts[i].Speedometer, speedometer))
            {
                RemoveAt(i);
            }
        }
    }

    private static void RemoveHostsExcept(Control speedometer)
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(Hosts[i].Speedometer, speedometer))
            {
                RemoveAt(i);
            }
        }
    }

    private static void RemoveAt(int index)
    {
        var host = Hosts[index];
        host.NativeContent.Children.Remove(host.CruiseRow);
        Hosts.RemoveAt(index);
    }
}

/// <summary>Attaches to the same native Avalonia tree as SE2's SPD indicator.</summary>
[HarmonyPatch]
internal static class CruiseControlHudTemplatePatch
{
    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.UI.HUD.Movement.HUDSpeedometer");
        return type is null ? null : AccessTools.DeclaredMethod(type, "OnApplyTemplate", [typeof(TemplateAppliedEventArgs)]);
    }

    private static void Postfix(object __instance, TemplateAppliedEventArgs e)
    {
        if (__instance is not Control speedometer)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug(
                $"[CruiseHudTrace] HUD speedometer template ignored: instance type {__instance.GetType().FullName} is not an Avalonia Control.");
            return;
        }

        bool isFlightSpeedometer = speedometer.Classes.Contains("Flight");
        BeveledBorder? tachometer = e.NameScope.Find<BeveledBorder>("PART_Tachometer");
        StackPanel? nativeContent = tachometer?.Child as StackPanel;
        ShadowedTextBlock? nativeSpeedText = e.NameScope.Find<ShadowedTextBlock>("PART_SpeedText");
        if (!ShouldAttach(
                isFlightSpeedometer,
                tachometer is not null,
                nativeContent is not null,
                nativeSpeedText is not null))
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug(
                $"[CruiseHudTrace] HUD template ignored: flightClass={isFlightSpeedometer}; tachometerFound={tachometer is not null}; " +
                $"nativeContentFound={nativeContent is not null}; nativeSpeedTextFound={nativeSpeedText is not null}; " +
                $"classes=[{string.Join(',', speedometer.Classes)}].");
            return;
        }

        // Extend the existing SPD border's vertical content stack. The native
        // speedometer now owns Cruise layout, clipping, visibility, and lifetime.
        CruiseControlHudManager.Attach(speedometer, nativeContent!, nativeSpeedText!);
    }

    internal static bool ShouldAttach(
        bool isFlightSpeedometer,
        bool tachometerFound,
        bool nativeContentFound,
        bool nativeSpeedTextFound) =>
        isFlightSpeedometer && tachometerFound && nativeContentFound && nativeSpeedTextFound;
}

/// <summary>Refreshes the native speedometer extension when SE2 reinitializes the reusable cockpit HUD screen.</summary>
[HarmonyPatch]
internal static class CruiseControlHudCleanPatch
{
    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.UI.HUD.Cockpit.CockpitHUDScreen");
        return type is null ? null : AccessTools.DeclaredMethod(type, "Clean");
    }

    // Clean() is also part of the first-person/external-camera transition.
    // Do not treat it as leaving the cockpit: the Cruise row is part of the
    // native HUDSpeedometer and therefore follows the native HUD visibility.
    private static void Postfix() => CruiseControlHudManager.Refresh();
}
