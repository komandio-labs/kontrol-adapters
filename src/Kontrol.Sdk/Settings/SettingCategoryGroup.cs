namespace Kontrol.Sdk.Settings;

/// <summary>
/// Logical grouping metadata for a set of related adapter settings.
/// </summary>
public sealed record SettingCategoryGroup(
    string Name,
    SettingIcon Icon = SettingIcon.Settings,
    string? Description = null
);
