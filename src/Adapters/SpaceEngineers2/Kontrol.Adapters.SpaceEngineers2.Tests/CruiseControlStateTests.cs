using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public sealed class CruiseControlStateTests
{
    [TestCase(.05f, 100f, 300f, .3333333f)]
    [TestCase(.50f, 100f, 300f, .50f)]
    [TestCase(.10f, 0f, 300f, .10f)]
    public void ManualCruiseThrottle_UsesTheHigherOfCruiseAndThrottleVelocityTargets(
        float throttle, float cruiseTarget, float maximumSpeed, float expectedAxis)
    {
        Patches.TranslationVelocityController.ComputeCruiseForwardVelocityHoldAxis(throttle, cruiseTarget, maximumSpeed)
            .ShouldBe(expectedAxis, .0001f);
    }

    [Test]
    public void SmallThrottleAtCruiseSpeed_DoesNotAccelerateOrBrake()
    {
        float axis = Patches.TranslationVelocityController.ComputeCruiseForwardVelocityHoldAxis(
            manualThrottle: .05f, cruiseTargetSpeedMetersPerSecond: 100f, maximumTargetSpeedMetersPerSecond: 300f);

        Patches.TranslationVelocityController.ComputeAxis(axis, actualVelocity: 100f, maximumTargetSpeedMetersPerSecond: 300f)
            .ShouldBe(0f, .0001f);
    }

    [Test]
    public void CruiseVelocityHold_RemainsSignedWhenShipIsAboveItsPositiveTarget()
    {
        Patches.TranslationVelocityController.ComputeCruiseAxis(
            input: .2f, actualVelocity: 300f, maximumTargetSpeedMetersPerSecond: 300f)
            .ShouldBe(-.2f, .0001f);
    }

    [TestCase(100f, 90f, 300f, .0333333f)]
    [TestCase(100f, 100f, 300f, 0f)]
    [TestCase(100f, 110f, 300f, 0f)]
    [TestCase(-100f, 0f, 300f, 0f)]
    public void MinimumSpeedController_AddsOnlyForwardThrustBelowTheTarget(
        float targetSpeed, float actualSpeed, float maximumSpeed, float expectedThrust)
    {
        Patches.TranslationVelocityController.ComputeMinimumForwardSpeedThrust(targetSpeed, actualSpeed, maximumSpeed)
            .ShouldBe(expectedThrust, .0001f);
    }

    [Test]
    public void Set_CapturesTheCurrentForwardSpeed()
    {
        var cruise = new Patches.CruiseControlState();

        cruise.SetOrReset(42.5f, nowTick: 1_000).ShouldBe(Patches.CruiseSetResult.Set);

        cruise.IsActive.ShouldBeTrue();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(42.5f);
    }

    [Test]
    public void Set_ClampsReverseSpeedToZero()
    {
        var cruise = new Patches.CruiseControlState();

        cruise.SetOrReset(-42.5f, nowTick: 1_000);

        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }

    [Test]
    public void SetAgainAfterTheDoubleClickWindow_ReplacesTheTarget()
    {
        var cruise = new Patches.CruiseControlState();

        cruise.SetOrReset(42.5f, nowTick: 1_000);
        cruise.SetOrReset(65f, nowTick: 1_000 + Patches.CruiseControlState.DoubleClickWindowMilliseconds + 1)
            .ShouldBe(Patches.CruiseSetResult.Set);

        cruise.IsActive.ShouldBeTrue();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(65f);
    }

    [Test]
    public void DoubleClick_ResetsCruise()
    {
        var cruise = new Patches.CruiseControlState();

        cruise.SetOrReset(42.5f, nowTick: 1_000);
        cruise.SetOrReset(65f, nowTick: 1_000 + Patches.CruiseControlState.DoubleClickWindowMilliseconds)
            .ShouldBe(Patches.CruiseSetResult.Reset);

        cruise.IsActive.ShouldBeFalse();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }

    [Test]
    public void TargetAdjustments_UseTenMeterStepsAndNeverGoBelowZero()
    {
        var cruise = new Patches.CruiseControlState();
        cruise.SetOrReset(15f, nowTick: 1_000);

        cruise.IncreaseTarget();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(25f);
        cruise.DecreaseTarget();
        cruise.DecreaseTarget();
        cruise.DecreaseTarget();

        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }

    [Test]
    public void BrakeCancel_ClearsTheTarget()
    {
        var cruise = new Patches.CruiseControlState();
        cruise.SetOrReset(42.5f, nowTick: 1_000);

        cruise.CancelForBrake();

        cruise.IsActive.ShouldBeFalse();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }
}
