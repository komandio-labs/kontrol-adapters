using System.Globalization;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Continuous or stepped numeric range setting descriptor with live physical units and display multipliers.
/// </summary>
public sealed record NumberSettingDescriptor : AdapterSettingDescriptor
{
    public float DefaultValue { get; init; }
    public float Min { get; init; } = 0f;
    public float Max { get; init; } = 100f;
    public float Step { get; init; } = 1f;
    public string? Unit { get; init; }
    public float? DisplayMultiplier { get; init; }
    public string? DisplayUnit { get; init; }
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

        if (val < Min || val > Max)
        {
            errorMessage = $"Value {val} is out of range [{Min}, {Max}] {Unit}".Trim();
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

        return Math.Clamp(val, Min, Max);
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
