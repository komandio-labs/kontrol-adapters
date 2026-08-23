namespace Kontrol.Sdk.Settings;

/// <summary>
/// A discrete choice option for String or Array settings.
/// </summary>
public sealed record SettingOption(
    string Value,
    string DisplayName,
    string? Description = null,
    SettingIcon? Icon = null
);
