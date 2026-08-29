using System.Reflection;
using HarmonyLib;
using Kontrol.Sdk.Settings;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

/// <summary>Formats adapter telemetry using an explicit user preference or SE2's HUD setting.</summary>
internal static class SpeedUnitPresentation
{
    private const int MetersPerSecond = 0;
    private const int KilometersPerHour = 1;
    private const int MilesPerHour = 2;

    private static int _gameSpeedUnit = MetersPerSecond;
    private static int _hasGameSpeedUnit;

    internal static string Format(float metersPerSecond, string preference)
    {
        int unit = ResolveSpeedUnit(preference);

        return unit switch
        {
            KilometersPerHour => $"{metersPerSecond * 3.6f:F1} km/h",
            MilesPerHour => $"{metersPerSecond * 2.2369363f:F1} mph",
            _ => $"{metersPerSecond:F1} m/s"
        };
    }

    internal static string FormatHudTarget(float metersPerSecond, string preference)
    {
        int unit = ResolveSpeedUnit(preference);
        float value = unit switch
        {
            KilometersPerHour => metersPerSecond * 3.6f,
            MilesPerHour => metersPerSecond * 2.2369363f,
            _ => metersPerSecond
        };
        string symbol = unit switch
        {
            KilometersPerHour => "km/h",
            MilesPerHour => "mph",
            _ => "m/s"
        };
        return $"{MathF.Round(value, MidpointRounding.AwayFromZero):0} {symbol}";
    }

    /// <summary>
    /// Converts a Cruise Control adjustment expressed in the unit the player is
    /// currently seeing into the controller's canonical metres-per-second unit.
    /// </summary>
    internal static float ConvertDisplayedSpeedToMetersPerSecond(float displayedSpeed, string preference)
    {
        float multiplier = ResolveSpeedUnit(preference) switch
        {
            KilometersPerHour => 3.6f,
            MilesPerHour => 2.2369363f,
            _ => 1f
        };
        return displayedSpeed / multiplier;
    }

    /// <summary>Converts the controller's canonical target speed to the displayed HUD unit.</summary>
    internal static float ConvertMetersPerSecondToDisplayedSpeed(float metersPerSecond, string preference)
    {
        float multiplier = ResolveSpeedUnit(preference) switch
        {
            KilometersPerHour => 3.6f,
            MilesPerHour => 2.2369363f,
            _ => 1f
        };
        return metersPerSecond * multiplier;
    }

    /// <summary>
    /// Moves a displayed target to the next coarse ten-unit value in the requested
    /// direction. This intentionally moves away from an exact multiple as well.
    /// </summary>
    internal static float MoveToNextTenDisplayedSpeed(float displayedSpeed, float direction) =>
        direction > 0f
            ? (MathF.Floor(displayedSpeed / 10f) + 1f) * 10f
            : Math.Max((MathF.Ceiling(displayedSpeed / 10f) - 1f) * 10f, 0f);

    internal static NumberSettingPresentation ResolveTargetSpeedPresentation(string preference) =>
        ResolveTargetSpeedPresentation(preference, VelocityHoldSpeedLimitState.Current);

    internal static NumberSettingPresentation ResolveTargetSpeedPresentation(string preference, float maximumMetersPerSecond)
    {
        int unit = ResolveSpeedUnit(preference);
        bool available = float.IsFinite(maximumMetersPerSecond) && maximumMetersPerSecond > 0f;
        float multiplier = unit switch
        {
            KilometersPerHour => 3.6f,
            MilesPerHour => 2.2369363f,
            _ => 1f
        };
        string symbol = unit switch
        {
            KilometersPerHour => "km/h",
            MilesPerHour => "mph",
            _ => "m/s"
        };
        string runtimeLabel = available ? $"{maximumMetersPerSecond * multiplier:F0} {symbol}" : "Waiting for SE2 grid limit";
        return unit switch
        {
            KilometersPerHour => new NumberSettingPresentation
            {
                Unit = MeasurementUnit.KilometersPerHour,
                Multiplier = multiplier,
                DecimalPlaces = 0,
                MinLabel = "Runtime limit",
                MidLabel = runtimeLabel,
                MaxLabel = runtimeLabel,
                Minimum = 0f,
                Maximum = available ? maximumMetersPerSecond : 0f,
                Step = 1f
            },
            MilesPerHour => new NumberSettingPresentation
            {
                Unit = MeasurementUnit.MilesPerHour,
                Multiplier = multiplier,
                DecimalPlaces = 0,
                MinLabel = "Runtime limit",
                MidLabel = runtimeLabel,
                MaxLabel = runtimeLabel,
                Minimum = 0f,
                Maximum = available ? maximumMetersPerSecond : 0f,
                Step = 1f
            },
            _ => new NumberSettingPresentation
            {
                Unit = MeasurementUnit.MetersPerSecond,
                Multiplier = 1f,
                DecimalPlaces = 0,
                MinLabel = "Runtime limit",
                MidLabel = runtimeLabel,
                MaxLabel = runtimeLabel,
                Minimum = 0f,
                Maximum = available ? maximumMetersPerSecond : 0f,
                Step = 1f
            }
        };
    }

    internal static void CaptureGameSpeedUnit(object? guiOptions)
    {
        try
        {
            object? value = guiOptions?.GetType().GetProperty(
                "SpeedUnit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(guiOptions);
            if (value is null) return;

            int unit = Convert.ToInt32(value);
            if (unit is < MetersPerSecond or > MilesPerHour) return;
            Volatile.Write(ref _gameSpeedUnit, unit);
            Volatile.Write(ref _hasGameSpeedUnit, 1);
        }
        catch { }
    }

    internal static void ResetForTests()
    {
        Volatile.Write(ref _gameSpeedUnit, MetersPerSecond);
        Volatile.Write(ref _hasGameSpeedUnit, 0);
    }

    private static int ResolveSpeedUnit(string preference) => preference switch
    {
        "KilometersPerHour" => KilometersPerHour,
        "MilesPerHour" => MilesPerHour,
        _ => Volatile.Read(ref _hasGameSpeedUnit) != 0
            ? Volatile.Read(ref _gameSpeedUnit)
            : MetersPerSecond
    };
}

/// <summary>Observes SE2's private HUD options without mutating them.</summary>
[HarmonyPatch]
internal static class GameSpeedUnitCapturePatch
{
    private static MethodBase? TargetMethod()
    {
        Type? type = AccessTools.TypeByName("Keen.Game2.Client.UI.HUD.Movement.MovementHUDScreenViewModel");
        return type is null ? null : AccessTools.DeclaredMethod(type, "UpdateSpeedometer");
    }

    private static void Postfix(object ____guiOptions) => SpeedUnitPresentation.CaptureGameSpeedUnit(____guiOptions);
}
