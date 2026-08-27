using System.Globalization;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Continuous or stepped numeric range setting descriptor with canonical and adapter-selected presentation units.
/// </summary>
public sealed record NumberSettingDescriptor : AdapterSettingDescriptor
{
    public float DefaultValue { get; init; }
    public float Min { get; init; } = 0f;
    public float Max { get; init; } = 100f;
    public float Step { get; init; } = 1f;
    /// <summary>True when the adapter supplies the valid range at runtime.</summary>
    public bool RuntimeRange { get; init; }
    /// <summary>
    /// Legacy free-form canonical unit. Retained for binary and JSON-schema
    /// compatibility with adapters compiled against SDK 1.1.x.
    /// </summary>
    [Obsolete("Use CanonicalUnit instead.")]
    public string? Unit { get; init; }

    /// <summary>Typed canonical physical unit for this setting's stored value.</summary>
    public MeasurementUnit CanonicalUnit { get; init; } = MeasurementUnit.None;
    public float? DisplayMultiplier { get; init; }
    /// <summary>Legacy free-form presentation unit. Use PresentationUnit instead.</summary>
    [Obsolete("Use PresentationUnit instead.")]
    public string? DisplayUnit { get; init; }

    /// <summary>Typed static presentation unit when no runtime override applies.</summary>
    public MeasurementUnit? PresentationUnit { get; init; }
    /// <summary>
    /// Optional adapter setting key that selects one of <see cref="PresentationVariants"/>.
    /// This lets a declarative packaged schema resolve presentation immediately,
    /// without the host owning game-specific unit rules.
    /// </summary>
    public string? PresentationSourceKey { get; init; }
    /// <summary>Adapter-declared presentation variants indexed by source-setting value.</summary>
    public IReadOnlyDictionary<string, NumberSettingPresentation>? PresentationVariants { get; init; }
    public string? MinLabel { get; init; }
    public string? MidLabel { get; init; }
    public string? MaxLabel { get; init; }

    public NumberSettingDescriptor()
    {
        Type = SettingType.Number;
    }

    public override bool Validate(object? value, out string? errorMessage)
    {
        if (value is null)
        {
            errorMessage = "Value cannot be null.";
            return false;
        }

        if (!TryConvertToSingle(value, out float val))
        {
            errorMessage = $"Value '{value}' is not a valid number.";
            return false;
        }

        if (float.IsNaN(val) || float.IsInfinity(val))
        {
            errorMessage = "Value cannot be NaN or Infinity.";
            return false;
        }

        if (val < Min || (!RuntimeRange && val > Max))
        {
            errorMessage = $"Value {val} is out of range [{Min}, {Max}] {CanonicalUnit.GetSymbol()}".Trim();
            return false;
        }

        errorMessage = null;
        return true;
    }

    public override object? Sanitize(object? value)
    {
        if (value is null || !TryConvertToSingle(value, out float val))
            return DefaultValue;

        if (float.IsNaN(val) || float.IsInfinity(val))
            return DefaultValue;

        return RuntimeRange ? Math.Max(val, Min) : Math.Clamp(val, Min, Max);
    }

    private static bool TryConvertToSingle(object value, out float result)
    {
        switch (value)
        {
            case float f: result = f; return true;
            case double d: result = (float)d; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case decimal dec: result = (float)dec; return true;
            case byte b: result = b; return true;
            case short s: result = s; return true;
            case System.Text.Json.JsonElement je:
                if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetSingle(out float singleVal))
                {
                    result = singleVal;
                    return true;
                }
                if (je.ValueKind == System.Text.Json.JsonValueKind.String && float.TryParse(je.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedJe))
                {
                    result = parsedJe;
                    return true;
                }
                result = 0f;
                return false;
            case string str when float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed):
                result = parsed; return true;
            default:
                try { result = Convert.ToSingle(value, CultureInfo.InvariantCulture); return true; }
                catch { result = 0f; return false; }
        }
    }
}
