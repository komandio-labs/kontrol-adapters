using System.Reflection;
using HarmonyLib;

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
        int unit = preference switch
        {
            "KilometersPerHour" => KilometersPerHour,
            "MilesPerHour" => MilesPerHour,
            _ => Volatile.Read(ref _hasGameSpeedUnit) != 0
                ? Volatile.Read(ref _gameSpeedUnit)
                : MetersPerSecond
        };

        return unit switch
        {
            KilometersPerHour => $"{metersPerSecond * 3.6f:F1} km/h",
            MilesPerHour => $"{metersPerSecond * 2.2369363f:F1} mph",
            _ => $"{metersPerSecond:F1} m/s"
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
