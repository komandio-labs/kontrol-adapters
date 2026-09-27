using Kontrol.Adapters.VtolVR.Settings;
using Kontrol.Sdk.Settings;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.VtolVR.Tests;

[TestFixture]
public sealed class VtolAdapterSettingsTests
{
    private readonly VtolAdapterSettingsProvider _provider = new();

    [Test]
    public void DefaultsToPositionThrottleAndExposesRateSettingOnlyInRateMode()
    {
        var snapshot = _provider.GetDefaultSnapshot();
        snapshot.GetBoolean("throttle.rateControl").ShouldBeFalse();
        snapshot.GetNumber("throttle.ratePerSecond").ShouldBe(0.5f);

        var rateSetting = _provider.Descriptors.OfType<NumberSettingDescriptor>().Single();
        rateSetting.VisibleWhen.ShouldBe(new SettingCondition("throttle.rateControl", ExpectedValue: true));
    }

    [Test]
    public void ValidateSettings_RejectsInvalidRate()
    {
        _provider.ValidateSettings(new Dictionary<string, object?> { ["throttle.ratePerSecond"] = 3f }, out var errors).ShouldBeFalse();
        errors.ContainsKey("throttle.ratePerSecond").ShouldBeTrue();
    }
}
