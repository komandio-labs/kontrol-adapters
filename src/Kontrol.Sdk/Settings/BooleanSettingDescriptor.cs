namespace Kontrol.Sdk.Settings;

/// <summary>
/// Boolean toggle setting descriptor.
/// </summary>
public sealed record BooleanSettingDescriptor : AdapterSettingDescriptor
{
    public bool DefaultValue { get; init; } = false;

    public BooleanSettingDescriptor()
    {
        Type = SettingType.Boolean;
    }

    public override bool Validate(object? value, out string? errorMessage)
    {
        if (value is bool || (value is string s && bool.TryParse(s, out _)))
        {
            errorMessage = null;
            return true;
        }

        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            {
                errorMessage = null;
                return true;
            }
            if (je.ValueKind == System.Text.Json.JsonValueKind.String && bool.TryParse(je.GetString(), out _))
            {
                errorMessage = null;
                return true;
            }
        }

        errorMessage = $"Value '{value}' is not a valid boolean (true/false).";
        return false;
    }

    public override object? Sanitize(object? value)
    {
        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out bool parsed)) return parsed;
        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.True) return true;
            if (je.ValueKind == System.Text.Json.JsonValueKind.False) return false;
            if (je.ValueKind == System.Text.Json.JsonValueKind.String && bool.TryParse(je.GetString(), out bool parsedJe)) return parsedJe;
        }
        return DefaultValue;
    }
}
