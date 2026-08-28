using System.Text.Json.Serialization;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Well-known measurement units supported by adapter setting schemas.
/// Numeric setting values always remain in the descriptor's canonical unit.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeasurementUnit>))]
public enum MeasurementUnit
{
    None = 0,
    Multiplier,
    MetersPerSecond,
    KilometersPerHour,
    MilesPerHour,
    RadiansPerSecond,
    DegreesPerSecond,
    RadiansPerSecondSquared
}

public static class MeasurementUnitExtensions
{
    public static string GetSymbol(this MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Multiplier => "×",
        MeasurementUnit.MetersPerSecond => "m/s",
        MeasurementUnit.KilometersPerHour => "km/h",
        MeasurementUnit.MilesPerHour => "mph",
        MeasurementUnit.RadiansPerSecond => "rad/s",
        MeasurementUnit.DegreesPerSecond => "°/s",
        MeasurementUnit.RadiansPerSecondSquared => "rad/s²",
        _ => string.Empty
    };
}
