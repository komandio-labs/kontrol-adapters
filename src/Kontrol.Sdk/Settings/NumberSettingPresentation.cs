namespace Kontrol.Sdk.Settings;

/// <summary>
/// Adapter-resolved presentation for one numeric setting. The host renders this
/// metadata verbatim and never selects a game-specific unit itself.
/// </summary>
public sealed record NumberSettingPresentation
{
    public required MeasurementUnit Unit { get; init; }
    public float Multiplier { get; init; } = 1f;
    public int DecimalPlaces { get; init; } = 1;
    public float? Minimum { get; init; }
    public float? Maximum { get; init; }
    public float? Step { get; init; }
    public string? MinLabel { get; init; }
    public string? MidLabel { get; init; }
    public string? MaxLabel { get; init; }
}
