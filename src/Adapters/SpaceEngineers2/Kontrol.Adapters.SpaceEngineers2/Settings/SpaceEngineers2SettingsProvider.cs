using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Settings;
using Kontrol.Adapters.SpaceEngineers2.Patches;
using Kontrol.Sdk.Diagnostics;

namespace Kontrol.Adapters.SpaceEngineers2.Settings;

/// <summary>
/// Authoritative setting schema provider for Space Engineers 2.
/// Exposes flight control modes and angular input mechanics.
/// </summary>
public sealed class SpaceEngineers2SettingsProvider : IAdapterSettingsProvider
{
    public string AdapterId => "space-engineers-2";
    public int SchemaVersion => 1;

    public IReadOnlyList<AdapterTraceDescriptor> SupportedTraces =>
#if DEBUG
        SpaceEngineers2DebugTraces.Supported;
#else
        [];
#endif

    public IReadOnlyList<SettingCategoryGroup> Categories { get; } = new List<SettingCategoryGroup>
    {
        new("Flight Controls", SettingIcon.Spacecraft, "Flight control translation mode and angular input mechanics.")
    };

    public IReadOnlyList<AdapterSettingDescriptor> Descriptors { get; } = new List<AdapterSettingDescriptor>
    {
        new StringSettingDescriptor
        {
            Key = "speedDisplayUnit",
            DisplayName = "Speed Display Units",
            Category = "Flight Controls",
            Icon = SettingIcon.Gauge,
            Layout = LayoutSpan.Full,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = "GameDefault",
            Description = "Selects units for adapter speed telemetry. Game Default follows SE2's HUD setting when available.",
            AllowedValues = new List<SettingOption>
            {
                new("GameDefault", "Game Default (Default)", "Follows the speed unit selected in Space Engineers 2's HUD."),
                new("KilometersPerHour", "Metric (km/h)", "Displays speed in kilometres per hour."),
                new("MilesPerHour", "Imperial (mph)", "Displays speed in miles per hour.")
            }
        },
        new StringSettingDescriptor
        {
            Key = "flightModelMode",
            DisplayName = "Flight Control Mode",
            Category = "Flight Controls",
            Icon = SettingIcon.Spacecraft,
            Layout = LayoutSpan.Full,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = "DirectAngularFlight",
            Description = "Selects how joystick inputs are translated to ship controls.",
            AllowedValues = new List<SettingOption>
            {
                new("DirectAngularFlight", "Direct Angular Flight (Default)", "Direct rate-controlled angular velocity with smooth acceleration ramping and glide easing."),
                new("NativeReticleSteering", "Native Reticle Steering", "Preserves Space Engineers 2's native virtual mouse-reticle steering and built-in crosshair dampening.")
            }
        },
        new NumberSettingDescriptor
        {
            Key = "directAngularAcceleration",
            DisplayName = "Rotational Acceleration Ramp",
            Category = "Flight Controls",
            Icon = SettingIcon.Clock,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 1.3f,
            Min = 0.1f,
            Max = 5.0f,
            Step = 0.1f,
            CanonicalUnit = MeasurementUnit.RadiansPerSecondSquared,
            MinLabel = "0.1 (Smooth Ramp)",
            MidLabel = "1.3 (Balanced)",
            MaxLabel = "5.0 (Instant)",
            Description = "Controls how smoothly rotation accelerates up to full commanded rate when deflecting the stick.",
            VisibleWhen = new SettingCondition("flightModelMode", ExpectedValue: "DirectAngularFlight")
        },
        new NumberSettingDescriptor
        {
            Key = "directAngularDeceleration",
            DisplayName = "Rotational Glide Deceleration",
            Category = "Flight Controls",
            Icon = SettingIcon.Inertia,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 1.0f,
            Min = 0.1f,
            Max = 5.0f,
            Step = 0.1f,
            CanonicalUnit = MeasurementUnit.RadiansPerSecondSquared,
            MinLabel = "0.1 (Long Glide)",
            MidLabel = "1.0 (Balanced)",
            MaxLabel = "5.0 (Quick Stop)",
            Description = "Controls rotational glide easing when releasing the stick to center before coming to rest.",
            VisibleWhen = new SettingCondition("flightModelMode", ExpectedValue: "DirectAngularFlight")
        },
        new NumberSettingDescriptor
        {
            Key = "directAngularMaxRate",
            DisplayName = "Maximum Turn Rate Scaling",
            Category = "Flight Controls",
            Icon = SettingIcon.Gyroscope,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 0.85f,
            Min = 0.1f,
            Max = 3.0f,
            Step = 0.05f,
            CanonicalUnit = MeasurementUnit.RadiansPerSecond,
            PresentationUnit = MeasurementUnit.DegreesPerSecond,
            DisplayMultiplier = 57.2957795f,
            MinLabel = "6 °/s",
            MidLabel = "49 °/s",
            MaxLabel = "172 °/s",
            Description = "Scales maximum target angular velocity achieved at 100% full stick deflection.",
            VisibleWhen = new SettingCondition("flightModelMode", ExpectedValue: "DirectAngularFlight")
        },
        new StringSettingDescriptor
        {
            Key = "translationControlMode",
            DisplayName = "Translation Control",
            Category = "Flight Controls",
            Icon = SettingIcon.Spacecraft,
            Layout = LayoutSpan.Full,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = "VelocityHold",
            Description = "Independent of Flight Control Mode. Selects whether translation axes command thrust directly or target local ship velocity.",
            AllowedValues = new List<SettingOption>
            {
                new("VelocityHold", "Velocity Hold (Default)", "Uses the current shaped axis as a local speed target and applies bounded signed feedback to reach it."),
                new("DirectThrust", "Direct Thrust", "Uses the current shaped axis value as proportional thrust.")
            }
        },
        new NumberSettingDescriptor
        {
            Key = "velocityHoldMaxTargetSpeed",
            DisplayName = "Velocity Hold Target-Speed Cap",
            Category = "Flight Controls",
            Icon = SettingIcon.Gauge,
            Layout = LayoutSpan.Full,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 0f,
            Min = 0f,
            Max = 0f,
            Step = 1f,
            RuntimeRange = true,
            CanonicalUnit = MeasurementUnit.MetersPerSecond,
            PresentationUnit = MeasurementUnit.MetersPerSecond,
            PresentationSourceKey = "speedDisplayUnit",
            PresentationVariants = new Dictionary<string, NumberSettingPresentation>
            {
                ["GameDefault"] = new() { Unit = MeasurementUnit.MetersPerSecond, Minimum = 0f, Maximum = 0f, Step = 1f, MinLabel = "Runtime limit", MidLabel = "Waiting for SE2 grid limit", MaxLabel = "Waiting for SE2 grid limit" },
                ["KilometersPerHour"] = new() { Unit = MeasurementUnit.KilometersPerHour, Multiplier = 3.6f, Minimum = 0f, Maximum = 0f, Step = 1f, MinLabel = "Runtime limit", MidLabel = "Waiting for SE2 grid limit", MaxLabel = "Waiting for SE2 grid limit" },
                ["MilesPerHour"] = new() { Unit = MeasurementUnit.MilesPerHour, Multiplier = 2.2369363f, Minimum = 0f, Maximum = 0f, Step = 1f, MinLabel = "Runtime limit", MidLabel = "Waiting for SE2 grid limit", MaxLabel = "Waiting for SE2 grid limit" }
            },
            MinLabel = "Runtime limit",
            MidLabel = "Waiting for SE2 grid limit",
            MaxLabel = "Waiting for SE2 grid limit",
            Description = "Optional cap for Velocity Hold targets; 0 uses SE2's active grid soft limit or velocity-limit provider.",
            VisibleWhen = new SettingCondition("translationControlMode", ExpectedValue: "VelocityHold")
        },
        new NumberSettingDescriptor
        {
            Key = "velocityHoldResponseGain",
            DisplayName = "Velocity Hold Response",
            Category = "Flight Controls",
            Icon = SettingIcon.Gauge,
            Layout = LayoutSpan.Half,
            UpdateScope = SettingUpdateScope.Realtime,
            DefaultValue = 12f,
            Min = 1f,
            Max = 20f,
            Step = 1f,
            CanonicalUnit = MeasurementUnit.Multiplier,
            MinLabel = "1 (Smooth)",
            MidLabel = "12 (Responsive)",
            MaxLabel = "20 (Aggressive)",
            Description = "Scales Velocity Hold feedback. Higher values keep thrust strong until closer to the target speed.",
            VisibleWhen = new SettingCondition("translationControlMode", ExpectedValue: "VelocityHold")
        }
    };

    public AdapterSettingsSnapshot GetDefaultSnapshot()
    {
        return AdapterSettingsSnapshot.Create(Descriptors, new Dictionary<string, object?>(), 1);
    }

    public bool ValidateSettings(IReadOnlyDictionary<string, object?> values, out IReadOnlyDictionary<string, string> errors)
    {
        var errorDict = new Dictionary<string, string>();

        foreach (var desc in Descriptors)
        {
            if (values.TryGetValue(desc.Key, out var val) && val != null)
            {
                if (!desc.Validate(val, out var error))
                {
                    errorDict[desc.Key] = error ?? "Invalid value.";
                }
            }
        }

        errors = errorDict;
        return errorDict.Count == 0;
    }

    public AdapterSettingsSnapshot CreateSnapshot(IReadOnlyDictionary<string, object?> rawValues, ulong sequenceNumber = 1)
    {
        var preference = rawValues.TryGetValue("speedDisplayUnit", out var rawPreference)
            ? rawPreference?.ToString() ?? "GameDefault"
            : "GameDefault";

        return AdapterSettingsSnapshot.Create(
            Descriptors,
            rawValues,
            sequenceNumber,
            new Dictionary<string, NumberSettingPresentation>
            {
                ["velocityHoldMaxTargetSpeed"] = SpeedUnitPresentation.ResolveTargetSpeedPresentation(preference)
            });
    }
}
