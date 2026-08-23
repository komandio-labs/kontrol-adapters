using System.Text.RegularExpressions;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// String setting descriptor supporting discrete choice enums, regex pattern matching, length bounds, and format hints.
/// </summary>
public sealed record StringSettingDescriptor : AdapterSettingDescriptor
{
    public string DefaultValue { get; init; } = string.Empty;
    public IReadOnlyList<SettingOption>? AllowedValues { get; init; }
    public string? Pattern { get; init; }
    public string? PatternErrorMessage { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? FormatHint { get; init; }
    public string? SampleValue { get; init; }

    public StringSettingDescriptor()
    {
        Type = SettingType.String;
    }

    public override bool Validate(object? value, out string? errorMessage)
    {
        string str;
        if (value is System.Text.Json.JsonElement je)
        {
            str = je.ValueKind == System.Text.Json.JsonValueKind.String ? (je.GetString() ?? string.Empty) : je.ToString();
        }
        else
        {
            str = value?.ToString() ?? string.Empty;
        }

        if (AllowedValues != null && AllowedValues.Count > 0)
        {
            if (!AllowedValues.Any(opt => string.Equals(opt.Value, str, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"Value '{str}' is not in the allowed options list.";
                return false;
            }
            errorMessage = null;
            return true;
        }

        if (MinLength.HasValue && str.Length < MinLength.Value)
        {
            errorMessage = $"Text length ({str.Length}) is shorter than minimum allowed ({MinLength.Value}).";
            return false;
        }

        if (MaxLength.HasValue && str.Length > MaxLength.Value)
        {
            errorMessage = $"Text length ({str.Length}) exceeds maximum allowed ({MaxLength.Value}).";
            return false;
        }

        if (!string.IsNullOrEmpty(Pattern))
        {
            if (!Regex.IsMatch(str, Pattern))
            {
                errorMessage = PatternErrorMessage ?? $"Value does not match required format '{FormatHint ?? Pattern}'.";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    public override object? Sanitize(object? value)
    {
        var str = value?.ToString() ?? DefaultValue;
        return Validate(str, out _) ? str : DefaultValue;
    }
}
