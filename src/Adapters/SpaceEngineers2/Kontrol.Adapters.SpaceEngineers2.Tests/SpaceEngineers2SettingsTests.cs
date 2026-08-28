using Kontrol.Adapters.SpaceEngineers2.Settings;
using Kontrol.Adapters.SpaceEngineers2.Patches;
using Kontrol.Sdk.Settings;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class SpaceEngineers2SettingsTests
{
    private SpaceEngineers2SettingsProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _provider = new SpaceEngineers2SettingsProvider();
    }

    [Test]
    public void Descriptors_ShouldHaveUniqueKeys_AndValidCategories()
    {
        var descriptors = _provider.Descriptors;
        descriptors.ShouldNotBeEmpty();

        var keys = descriptors.Select(d => d.Key).ToList();
        keys.Distinct().Count().ShouldBe(keys.Count, "All setting keys must be unique.");

        var categoryNames = _provider.Categories.Select(c => c.Name).ToHashSet();
        foreach (var desc in descriptors)
        {
            categoryNames.ShouldContain(desc.Category, $"Setting '{desc.Key}' must belong to a known category.");
            desc.DisplayName.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Test]
    public void DefaultSnapshot_ShouldContainAllDefaultValues_AndCorrectActiveKeys()
    {
        var snapshot = _provider.GetDefaultSnapshot();

        snapshot.ShouldNotBeNull();
        snapshot.GetString("speedDisplayUnit").ShouldBe("GameDefault");
        snapshot.GetString("flightModelMode").ShouldBe("DirectAngularFlight");
        snapshot.GetString("translationControlMode").ShouldBe("VelocityHold");
        snapshot.GetNumber("velocityHoldMaxTargetSpeed").ShouldBe(0f);
        snapshot.GetNumber("velocityHoldResponseGain").ShouldBe(12f);
        snapshot.GetNumber("directAngularAcceleration").ShouldBe(1.3f);
        snapshot.GetNumber("directAngularDeceleration").ShouldBe(1.0f);
        snapshot.GetNumber("directAngularMaxRate").ShouldBe(0.85f);

        // In default DirectAngularFlight mode, the angular settings are active
        snapshot.IsActive("directAngularAcceleration").ShouldBeTrue();
        snapshot.IsActive("directAngularDeceleration").ShouldBeTrue();
        snapshot.IsActive("directAngularMaxRate").ShouldBeTrue();
        snapshot.IsActive("velocityHoldMaxTargetSpeed").ShouldBeTrue();
        snapshot.IsActive("velocityHoldResponseGain").ShouldBeTrue();
        var translationMode = (StringSettingDescriptor)_provider.Descriptors.Single(descriptor => descriptor.Key == "translationControlMode");
        translationMode.AllowedValues!.First()!.Value.ShouldBe("VelocityHold");
    }

    [Test]
    public void VisibleWhen_ConditionEvaluation_ShouldSwitchActiveKeysAccurately()
    {
        // Switch to NativeReticleSteering mode
        var rawValues = new Dictionary<string, object?>
        {
            ["flightModelMode"] = "NativeReticleSteering"
        };

        var snapshot = _provider.CreateSnapshot(rawValues, sequenceNumber: 42);

        snapshot.SequenceNumber.ShouldBe(42UL);
        snapshot.GetString("flightModelMode").ShouldBe("NativeReticleSteering");

        // In NativeReticleSteering mode: direct angular settings are inactive
        snapshot.IsActive("directAngularAcceleration").ShouldBeFalse();
        snapshot.IsActive("directAngularDeceleration").ShouldBeFalse();
        snapshot.IsActive("directAngularMaxRate").ShouldBeFalse();
        snapshot.IsActive("velocityHoldMaxTargetSpeed").ShouldBeTrue();
        snapshot.IsActive("velocityHoldResponseGain").ShouldBeTrue();
    }

    [Test]
    public void TranslationControlMode_IsIndependentOfRotationalFlightMode()
    {
        var snapshot = _provider.CreateSnapshot(new Dictionary<string, object?>
        {
            ["flightModelMode"] = "NativeReticleSteering",
            ["translationControlMode"] = "VelocityHold",
            ["velocityHoldMaxTargetSpeed"] = 250f
        });

        snapshot.GetString("flightModelMode").ShouldBe("NativeReticleSteering");
        snapshot.GetString("translationControlMode").ShouldBe("VelocityHold");
        snapshot.GetNumber("velocityHoldMaxTargetSpeed").ShouldBe(250f);
        snapshot.IsActive("velocityHoldMaxTargetSpeed").ShouldBeTrue();
    }

    [Test]
    public void DescriptorOrder_GroupsRotationalPropertiesWithFlightControlModeBeforeTranslationControl()
    {
        _provider.Descriptors.Select(descriptor => descriptor.Key).ShouldBe([
            "speedDisplayUnit",
            "flightModelMode",
            "directAngularAcceleration",
            "directAngularDeceleration",
            "directAngularMaxRate",
            "translationControlMode",
            "velocityHoldMaxTargetSpeed",
            "velocityHoldResponseGain"
        ]);
    }

    [Test]
    public void Validation_ShouldDetectInvalidValues_AndEnforceConstraints()
    {
        var invalidValues = new Dictionary<string, object?>
        {
            ["directAngularAcceleration"] = 99f, // Max is 5.0
            ["flightModelMode"] = "InvalidArcadeMode", // Not in AllowedValues
            ["translationControlMode"] = "TargetPosition" // Not in AllowedValues
        };

        bool isValid = _provider.ValidateSettings(invalidValues, out var errors);

        isValid.ShouldBeFalse();
        errors.ContainsKey("directAngularAcceleration").ShouldBeTrue();
        errors.ContainsKey("flightModelMode").ShouldBeTrue();
        errors.ContainsKey("translationControlMode").ShouldBeTrue();
    }

    [Test]
    public void Sanitization_ShouldClampNumbers_AndFallbackToDefaults()
    {
        var outOfBounds = new Dictionary<string, object?>
        {
            ["directAngularAcceleration"] = 1500f, // Above max -> clamped to 5.0
            ["directAngularDeceleration"] = -10f, // Below min -> clamped to 0.1
            ["directAngularMaxRate"] = 100f, // Above max -> clamped to 3.0
            ["velocityHoldMaxTargetSpeed"] = 5000f, // Runtime range: retained until SE2 supplies its live cap
            ["velocityHoldResponseGain"] = 100f // Above max -> clamped to 20
        };

        var snapshot = _provider.CreateSnapshot(outOfBounds);

        snapshot.GetNumber("directAngularAcceleration").ShouldBe(5.0f);
        snapshot.GetNumber("directAngularDeceleration").ShouldBe(0.1f);
        snapshot.GetNumber("directAngularMaxRate").ShouldBe(3.0f);
        snapshot.GetNumber("velocityHoldMaxTargetSpeed").ShouldBe(5000f);
        snapshot.GetNumber("velocityHoldResponseGain").ShouldBe(20f);
    }

    [Test]
    public void SpeedDisplayUnit_OffersGameDefaultMetricAndImperialOptions()
    {
        var descriptor = (StringSettingDescriptor)_provider.Descriptors.First();

        descriptor.Key.ShouldBe("speedDisplayUnit");
        descriptor.AllowedValues!.Select(option => option.Value).ShouldBe([
            "GameDefault", "KilometersPerHour", "MilesPerHour"]);
    }

    [TestCase("KilometersPerHour", MeasurementUnit.KilometersPerHour, 3.6f)]
    [TestCase("MilesPerHour", MeasurementUnit.MilesPerHour, 2.2369363f)]
    [TestCase("GameDefault", MeasurementUnit.MetersPerSecond, 1f)]
    public void SpeedDisplayUnit_ResolvesFinalPresentationPerParameter(
        string preference,
        MeasurementUnit expectedUnit,
        float expectedMultiplier)
    {
        SpeedUnitPresentation.ResetForTests();
        var snapshot = _provider.CreateSnapshot(new Dictionary<string, object?>
        {
            ["speedDisplayUnit"] = preference
        });

        snapshot.TryGetNumberPresentation("velocityHoldMaxTargetSpeed", out var presentation).ShouldBeTrue();
        presentation.Unit.ShouldBe(expectedUnit);
        presentation.Multiplier.ShouldBe(expectedMultiplier, 0.00001f);
        presentation.Maximum.ShouldBe(0f);
        presentation.MidLabel.ShouldBe("Waiting for SE2 grid limit");
    }

    [Test]
    public void NumericDescriptors_UseWellKnownUnits()
    {
        ((NumberSettingDescriptor)_provider.Descriptors.Single(d => d.Key == "velocityHoldMaxTargetSpeed"))
            .CanonicalUnit.ShouldBe(MeasurementUnit.MetersPerSecond);
        ((NumberSettingDescriptor)_provider.Descriptors.Single(d => d.Key == "directAngularMaxRate"))
            .PresentationUnit.ShouldBe(MeasurementUnit.DegreesPerSecond);
    }
}
