using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Settings;

namespace Kontrol.Adapters.VtolVR.Settings;

public sealed class VtolAdapterSettingsProvider : IAdapterSettingsProvider
{
    public string AdapterId => "vtol-vr";
    public int SchemaVersion => 1;

    public IReadOnlyList<SettingCategoryGroup> Categories { get; } =
    [
        new("Flight Controls", SettingIcon.Spacecraft, "Configure VTOL VR flight control behavior.")
    ];

    public IReadOnlyList<AdapterSettingDescriptor> Descriptors { get; } =
    [
        new BooleanSettingDescriptor
        {
            Key = "throttle.rateControl",
            DisplayName = "Incremental Throttle",
            Category = "Flight Controls",
            Icon = SettingIcon.ThrottleQuadrant,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = false,
            Description = "When enabled, positive throttle-axis deflection increases throttle and negative deflection decreases it; a centered axis holds the current setting."
        },
        new NumberSettingDescriptor
        {
            Key = "throttle.ratePerSecond",
            DisplayName = "Throttle Change Rate",
            Category = "Flight Controls",
            Icon = SettingIcon.ThrottleQuadrant,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 0.5f,
            Min = 0.1f,
            Max = 2f,
            Step = 0.1f,
            MinLabel = "0.1 (Slow)",
            MidLabel = "0.5 (Default)",
            MaxLabel = "2.0 (Fast)",
            Description = "Maximum throttle change per second at full axis deflection; partial deflection changes throttle proportionally.",
            VisibleWhen = new SettingCondition("throttle.rateControl", ExpectedValue: true)
        }
    ];

    public AdapterSettingsSnapshot GetDefaultSnapshot() =>
        AdapterSettingsSnapshot.Create(Descriptors, new Dictionary<string, object?>(), 1);

    public bool ValidateSettings(IReadOnlyDictionary<string, object?> values, out IReadOnlyDictionary<string, string> errors)
    {
        var failures = new Dictionary<string, string>();
        foreach (var descriptor in Descriptors)
        {
            if (values.TryGetValue(descriptor.Key, out object? value) && !descriptor.Validate(value, out string? error))
                failures[descriptor.Key] = error ?? "Invalid value.";
        }

        errors = failures;
        return failures.Count == 0;
    }

    public AdapterSettingsSnapshot CreateSnapshot(IReadOnlyDictionary<string, object?> rawValues, ulong sequenceNumber = 1) =>
        AdapterSettingsSnapshot.Create(Descriptors, rawValues, sequenceNumber);
}
