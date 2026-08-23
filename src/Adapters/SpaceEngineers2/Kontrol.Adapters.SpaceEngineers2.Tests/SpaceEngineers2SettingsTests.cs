using Kontrol.Adapters.SpaceEngineers2.Settings;
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
        snapshot.GetString("flightModelMode").ShouldBe("DirectAngularFlight");
        snapshot.GetNumber("directAngularAcceleration").ShouldBe(1.3f);
        snapshot.GetNumber("directAngularDeceleration").ShouldBe(1.0f);
        snapshot.GetNumber("directAngularMaxRate").ShouldBe(0.85f);

        // In default DirectAngularFlight mode, the angular settings are active
        snapshot.IsActive("directAngularAcceleration").ShouldBeTrue();
        snapshot.IsActive("directAngularDeceleration").ShouldBeTrue();
        snapshot.IsActive("directAngularMaxRate").ShouldBeTrue();
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
    }

    [Test]
    public void Validation_ShouldDetectInvalidValues_AndEnforceConstraints()
    {
        var invalidValues = new Dictionary<string, object?>
        {
            ["directAngularAcceleration"] = 99f, // Max is 5.0
            ["flightModelMode"] = "InvalidArcadeMode" // Not in AllowedValues
        };

        bool isValid = _provider.ValidateSettings(invalidValues, out var errors);

        isValid.ShouldBeFalse();
        errors.ContainsKey("directAngularAcceleration").ShouldBeTrue();
        errors.ContainsKey("flightModelMode").ShouldBeTrue();
    }

    [Test]
    public void Sanitization_ShouldClampNumbers_AndFallbackToDefaults()
    {
        var outOfBounds = new Dictionary<string, object?>
        {
            ["directAngularAcceleration"] = 1500f, // Above max -> clamped to 5.0
            ["directAngularDeceleration"] = -10f, // Below min -> clamped to 0.1
            ["directAngularMaxRate"] = 100f // Above max -> clamped to 3.0
        };

        var snapshot = _provider.CreateSnapshot(outOfBounds);

        snapshot.GetNumber("directAngularAcceleration").ShouldBe(5.0f);
        snapshot.GetNumber("directAngularDeceleration").ShouldBe(0.1f);
        snapshot.GetNumber("directAngularMaxRate").ShouldBe(3.0f);
    }
}
