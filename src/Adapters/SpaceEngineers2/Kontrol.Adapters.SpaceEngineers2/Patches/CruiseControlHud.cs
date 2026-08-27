using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using HarmonyLib;
using Keen.VRage.UI.Shared.Controls.BeveledBorder;
using Kontrol.Adapters.SpaceEngineers2.Settings;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Hosts the cruise indicator in the game's Avalonia tree using SE2's native beveled styling.</summary>
internal sealed class CruiseControlHudOverlay : Canvas
{
    private const double IndicatorHeight = 32d;
    private const double IndicatorGap = 8d;

    private readonly Control _speedometerAnchor;
    private readonly BeveledBorder _backplate;
    private readonly CruiseControlIndicatorVisual _indicator;

    internal CruiseControlHudOverlay(Control speedometerAnchor)
    {
        _speedometerAnchor = speedometerAnchor;
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

        _indicator = new CruiseControlIndicatorVisual
        {
            IsHitTestVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        _backplate = new BeveledBorder
        {
            Child = _indicator,
            IsHitTestVisible = false,
            ClipToBounds = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Padding = new Thickness(0)
        };
        CopyNativeBorderAppearance(speedometerAnchor as BeveledBorder);
        Children.Add(_backplate);
    }

    internal void Update(bool isActive, string targetSpeedText, float offsetX, float offsetY)
    {
        _indicator.Update(isActive, targetSpeedText);
        IsVisible = isActive;
        if (isActive)
        {
            Reposition(offsetX, offsetY);
        }
    }

    internal void Reposition(float offsetX = 0f, float offsetY = 0f)
    {
        Point? anchorTopLeft;
        try
        {
            anchorTopLeft = _speedometerAnchor.TranslatePoint(new Point(0, 0), this);
        }
        catch
        {
            return;
        }

        if (!anchorTopLeft.HasValue || _speedometerAnchor.Bounds.Width <= 0 || _speedometerAnchor.Bounds.Height <= 0)
        {
            return;
        }

        double scale = Math.Clamp(_speedometerAnchor.Bounds.Height / 60d, 0.65d, 2.0d);
        // Match the native SPD/tachometer border width exactly. Do not use a
        // fixed HTML/Poc width here because SE2's actual width is the source
        // of truth for this in-game control.
        double width = _speedometerAnchor.Bounds.Width;
        double height = IndicatorHeight * scale;
        _backplate.Width = width;
        _backplate.Height = height;

        // Keep both beveled borders visibly separate. Positive Y moves down;
        // negative Y moves up, matching the setting labels.
        double requestedLeft = anchorTopLeft.Value.X + _speedometerAnchor.Bounds.Width - width + offsetX;
        double requestedTop = anchorTopLeft.Value.Y - height - IndicatorGap * scale + offsetY;
        double left = ClampToVisibleBounds(requestedLeft, width, Bounds.Width);
        double top = ClampToVisibleBounds(requestedTop, height, Bounds.Height);
        Canvas.SetLeft(_backplate, left);
        Canvas.SetTop(_backplate, top);
    }

    internal static double ClampToVisibleBounds(double requested, double contentLength, double availableLength)
    {
        if (!double.IsFinite(requested) || !double.IsFinite(contentLength) || !double.IsFinite(availableLength) ||
            contentLength <= 0d || availableLength <= 0d)
        {
            return requested;
        }

        return Math.Clamp(requested, 0d, Math.Max(0d, availableLength - contentLength));
    }

    private void CopyNativeBorderAppearance(BeveledBorder? nativeBorder)
    {
        if (nativeBorder is null)
        {
            _backplate.Bevel = new CornerRadius(0, 5, 5, 0);
            return;
        }

        _backplate.Bevel = nativeBorder.Bevel;
        _backplate.BorderThickness = nativeBorder.BorderThickness;
        _backplate.Padding = nativeBorder.Padding;
        if (nativeBorder.Background is not null)
        {
            _backplate.Background = nativeBorder.Background;
        }
        if (nativeBorder.BorderBrush is not null)
        {
            _backplate.BorderBrush = nativeBorder.BorderBrush;
        }
    }
}

/// <summary>Renders the compact circular cruise-control glyph and target text.</summary>
internal sealed class CruiseControlIndicatorVisual : Control
{
    private bool _isActive;
    private string _targetSpeedText = string.Empty;

    internal void Update(bool isActive, string targetSpeedText)
    {
        bool changed = _isActive != isActive
                       || !string.Equals(_targetSpeedText, targetSpeedText, StringComparison.Ordinal);
        _isActive = isActive;
        _targetSpeedText = targetSpeedText;
        IsVisible = isActive;
        if (changed)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (!_isActive || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        double scale = Math.Clamp(Bounds.Height / 32d, 0.65d, 2.0d);
        var center = new Point(Bounds.Width - 23d * scale, Bounds.Height / 2d);
        double radius = 9d * scale;
        var color = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
        var typeface = new Typeface(new FontFamily("Share Tech Mono, Consolas, monospace"), FontStyle.Normal, FontWeight.Bold);
        var text = new FormattedText(_targetSpeedText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 16d * scale, color);
        double textRight = center.X - radius - 8d * scale;
        context.DrawText(text, new Point(textRight - text.Width, center.Y - text.Height / 2d - scale));

        var pen = new Pen(color, 1.8d * scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        DrawArc(context, pen, center, radius, Math.PI * 0.75d, Math.PI * 2.25d, 18);
        var tickPen = new Pen(color, 1.2d * scale, lineCap: PenLineCap.Round);
        foreach (double angle in new[] { 0.85d, 1.15d, 1.5d, 1.85d, 2.15d })
        {
            DrawRadialLine(context, tickPen, center, radius - 3d * scale, radius, Math.PI * angle);
        }

        DrawRadialLine(context, pen, center, radius - 3d * scale, 0d, Math.PI * 1.25d);
        context.DrawEllipse(color, null, center, 1.8d * scale, 1.8d * scale);

        double lockAngle = Math.PI * 1.15d;
        Point tip = PointOnCircle(center, radius + 1.5d * scale, lockAngle);
        Point arrowBase = PointOnCircle(center, radius + 6.5d * scale, lockAngle);
        Vector perpendicular = new Vector(-Math.Sin(lockAngle), Math.Cos(lockAngle)) * (3d * scale);
        var arrow = new StreamGeometry();
        using (var geometry = arrow.Open())
        {
            geometry.BeginFigure(tip, true);
            geometry.LineTo(arrowBase + perpendicular);
            geometry.LineTo(arrowBase - perpendicular);
            geometry.EndFigure(true);
        }
        context.DrawGeometry(color, null, arrow);
    }

    private static void DrawArc(DrawingContext context, Pen pen, Point center, double radius, double startAngle, double endAngle, int segments)
    {
        Point previous = PointOnCircle(center, radius, startAngle);
        for (int i = 1; i <= segments; i++)
        {
            double angle = startAngle + (endAngle - startAngle) * i / segments;
            Point next = PointOnCircle(center, radius, angle);
            context.DrawLine(pen, previous, next);
            previous = next;
        }
    }

    private static void DrawRadialLine(DrawingContext context, Pen pen, Point center, double startRadius, double endRadius, double angle) =>
        context.DrawLine(pen, PointOnCircle(center, startRadius, angle), PointOnCircle(center, endRadius, angle));

    private static Point PointOnCircle(Point center, double radius, double angle) =>
        new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
}

/// <summary>Owns overlays attached to live SE2 flight speedometer templates.</summary>
internal static class CruiseControlHudManager
{
    private sealed class Host
    {
        internal required Grid Root { get; init; }
        internal required Control Anchor { get; init; }
        internal required CruiseControlHudOverlay Overlay { get; init; }
    }

    private static readonly List<Host> Hosts = new();
    private static bool _cockpitActive;
    private static string? _lastVisibilityTrace;

    internal static void Attach(Grid root, Control anchor)
    {
        // SE2 can recreate the flight HUD tree after a cockpit view change.
        // Keep only the current native speedometer host so a detached tree
        // cannot consume later refreshes or accumulate indefinitely.
        RemoveHostsExcept(root);
        RemoveForRoot(root);
        var overlay = new CruiseControlHudOverlay(anchor);
        Grid.SetColumn(overlay, 0);
        Grid.SetColumnSpan(overlay, Math.Max(1, root.ColumnDefinitions.Count));
        Grid.SetRow(overlay, 0);
        Grid.SetRowSpan(overlay, Math.Max(1, root.RowDefinitions.Count));
        overlay.ZIndex = 1000;
        root.Children.Add(overlay);
        root.LayoutUpdated += RootOnLayoutUpdated;
        Hosts.Add(new Host { Root = root, Anchor = anchor, Overlay = overlay });
        SpaceEngineers2AdapterDiagnostics.WriteDebug(
            $"[CruiseHudTrace] Attached overlay. hosts={Hosts.Count}; root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}; " +
            $"anchor={anchor.Bounds.Width:F0}x{anchor.Bounds.Height:F0}.");
        Refresh();
    }

    internal static void Refresh(bool? cockpitActive = null)
    {
        if (cockpitActive.HasValue)
        {
            _cockpitActive = cockpitActive.Value;
        }

        var settings = SpaceEngineers2SettingsManager.Instance;
        bool isActive = _cockpitActive
                        && settings.ShowCruiseControlHudIndicator
                        && CockpitInputPatch.IsCruiseControlActiveForHud;
        string visibilityTrace = $"hosts={Hosts.Count}; cockpitActive={_cockpitActive}; settingEnabled={settings.ShowCruiseControlHudIndicator}; " +
                                 $"cruiseActive={CockpitInputPatch.IsCruiseControlActiveForHud}; visible={isActive}";
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
            host.Overlay.Update(isActive, targetSpeed, settings.CruiseControlHudOffsetX, settings.CruiseControlHudOffsetY);
        }
    }

    internal static void Hide() => Refresh(false);

    // A missing IPC frame can be a transient condition during a camera/HUD
    // transition. Preserve the current state instead of hiding the overlay;
    // the native Flight HUD parent still controls whether it is rendered.
    internal static void HideTransient() => Refresh();

    internal static void Clear()
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            RemoveAt(i);
        }
        _cockpitActive = false;
        _lastVisibilityTrace = null;
    }

    private static void RemoveForRoot(Grid root)
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(Hosts[i].Root, root))
            {
                RemoveAt(i);
            }
        }
    }

    private static void RemoveHostsExcept(Grid root)
    {
        for (int i = Hosts.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(Hosts[i].Root, root))
            {
                RemoveAt(i);
            }
        }
    }

    private static void RemoveAt(int index)
    {
        var host = Hosts[index];
        host.Root.LayoutUpdated -= RootOnLayoutUpdated;
        host.Root.Children.Remove(host.Overlay);
        Hosts.RemoveAt(index);
    }

    private static void RootOnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is Grid root)
        {
            var settings = SpaceEngineers2SettingsManager.Instance;
            foreach (var host in Hosts.Where(host => ReferenceEquals(host.Root, root)))
            {
                host.Overlay.Reposition(settings.CruiseControlHudOffsetX, settings.CruiseControlHudOffsetY);
            }
        }
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
        Grid? root = e.NameScope.Find<Grid>("PART_Root");
        Control? anchor = e.NameScope.Find<Control>("PART_Tachometer");
        if (!ShouldAttach(isFlightSpeedometer, root is not null, anchor is not null))
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug(
                $"[CruiseHudTrace] HUD template ignored: flightClass={isFlightSpeedometer}; rootFound={root is not null}; tachometerFound={anchor is not null}; " +
                $"classes=[{string.Join(',', speedometer.Classes)}].");
            return;
        }

        CruiseControlHudManager.Attach(root!, anchor!);
    }

    internal static bool ShouldAttach(bool isFlightSpeedometer, bool rootFound, bool tachometerFound) =>
        isFlightSpeedometer && rootFound && tachometerFound;
}

/// <summary>Refreshes the overlay when SE2 reinitializes the reusable cockpit HUD screen.</summary>
[HarmonyPatch]
internal static class CruiseControlHudCleanPatch
{
    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.UI.HUD.Cockpit.CockpitHUDScreen");
        return type is null ? null : AccessTools.DeclaredMethod(type, "Clean");
    }

    // Clean() is also part of the first-person/external-camera transition.
    // Do not treat it as leaving the cockpit: the overlay is already inside
    // FlightHUDControl and therefore follows the native HUD visibility.
    private static void Postfix() => CruiseControlHudManager.Refresh();
}
