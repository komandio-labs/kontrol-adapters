using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Settings;

namespace Kontrol.Adapters.SpaceEngineers.Settings;

/// <summary>
/// Space Engineers 1 settings rendered by Kontrol's generic Adapter Settings page.
/// Values are delivered live to the Pulsar payload through the standard settings IPC channel.
/// </summary>
public sealed class SpaceEngineersSettingsProvider : IAdapterSettingsProvider
{
    public string AdapterId => "space-engineers";
    public int SchemaVersion => 1;
    public IReadOnlyList<AdapterTraceDescriptor> SupportedTraces => [];

    public IReadOnlyList<SettingCategoryGroup> Categories { get; } =
    [
        new("Flight Controls", SettingIcon.Spacecraft, "Tune the final Space Engineers 1 ship-control response for your joystick, HOTAS, or HOSAS.")
        ,new("Camera", SettingIcon.Eye, "Tune third-person camera controls for your joystick, HOTAS, or HOSAS.")
    ];

    public IReadOnlyList<AdapterSettingDescriptor> Descriptors { get; } =
    [
        CreateSensitivity("flight.pitchSensitivity", "Pitch Sensitivity", "Scales nose-up and nose-down response at Space Engineers' final ship-control commit.", FlightSensitivityDefaults.Pitch),
        CreateSensitivity("flight.yawSensitivity", "Yaw Sensitivity", "Scales left and right yaw response at Space Engineers' final ship-control commit.", FlightSensitivityDefaults.Yaw),
        CreateSensitivity("flight.rollSensitivity", "Roll Sensitivity", "Scales bank-left and bank-right response at Space Engineers' final ship-control commit.", FlightSensitivityDefaults.Roll),
        CreateCameraSensitivity()
    ];

    public AdapterSettingsSnapshot GetDefaultSnapshot() => AdapterSettingsSnapshot.Create(Descriptors, new Dictionary<string, object?>(), 1);

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

    private static NumberSettingDescriptor CreateSensitivity(string key, string displayName, string description, float defaultValue) => new()
    {
        Key = key,
        DisplayName = displayName,
        Category = "Flight Controls",
        Icon = SettingIcon.Gyroscope,
        Layout = LayoutSpan.Half,
        UpdateScope = SettingUpdateScope.Realtime,
        DefaultValue = defaultValue,
        Min = 0.1f,
        Max = 40f,
        Step = 0.1f,
        CanonicalUnit = MeasurementUnit.Multiplier,
        MinLabel = "0.1× (Gentle)",
        MidLabel = defaultValue.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "× (Recommended)",
        MaxLabel = "40.0× (Fast)",
        Description = description
    };

    private static NumberSettingDescriptor CreateCameraSensitivity() => new()
    {
        Key = "camera.lookSensitivity",
        DisplayName = "Camera Look Sensitivity",
        Category = "Camera",
        Icon = SettingIcon.Eye,
        Layout = LayoutSpan.Half,
        UpdateScope = SettingUpdateScope.Realtime,
        DefaultValue = FlightSensitivityDefaults.CameraLook,
        Min = 0.1f,
        Max = 10f,
        Step = 0.1f,
        CanonicalUnit = MeasurementUnit.Multiplier,
        MinLabel = "0.1× (Gentle)",
        MidLabel = "2.0× (Recommended)",
        MaxLabel = "10.0× (Fast)",
        Description = "Scales horizontal, vertical, and zoom speed while look-around is active."
    };

    private static class FlightSensitivityDefaults
    {
        internal const float Pitch = 20f;
        internal const float Yaw = 20f;
        internal const float Roll = 1f;
        internal const float CameraLook = 2f;
    }
}
