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
    [TestCase(0f)]
    [TestCase(-42.5f)]
    public void Set_IgnoresNonPositiveForwardSpeed(float speed)
    {
        var cruise = new Patches.CruiseControlState();

        cruise.SetOrReset(speed, nowTick: 1_000).ShouldBe(Patches.CruiseSetResult.Ignored);

        cruise.IsActive.ShouldBeFalse();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }

    [Test]
    public void Set_DoubleClickStillResetsCruiseEvenWhenTheCurrentSpeedIsZero()
    {
        var cruise = new Patches.CruiseControlState();
        cruise.SetOrReset(42.5f, nowTick: 1_000).ShouldBe(Patches.CruiseSetResult.Set);

        cruise.SetOrReset(0f, nowTick: 1_000 + Patches.CruiseControlState.DoubleClickWindowMilliseconds)
            .ShouldBe(Patches.CruiseSetResult.Reset);

        cruise.IsActive.ShouldBeFalse();
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
    public void TargetAdjustments_UseTheRequestedStepAndNeverGoBelowZero()
    {
        var cruise = new Patches.CruiseControlState();
        cruise.SetOrReset(15f, nowTick: 1_000);

        cruise.AdjustTarget(1f).ShouldBeTrue();
        cruise.TargetSpeedMetersPerSecond.ShouldBe(16f);
        cruise.IsFastRetargeting.ShouldBeTrue();
        cruise.AdjustTarget(-10f).ShouldBeTrue();
        cruise.AdjustTarget(-10f).ShouldBeTrue();

        cruise.TargetSpeedMetersPerSecond.ShouldBe(0f);
    }

    [Test]
    public void AdjustmentRepeater_StartsAtOneThenAcceleratesToFiveAndTen()
    {
        var repeater = new Patches.CruiseAdjustmentRepeater();

        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 0).ShouldBe(1f);
        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 349).ShouldBe(0f);
        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 350).ShouldBe(1f);
        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 750).ShouldBe(5f);
        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 1_500).ShouldBe(10f);
    }

    [Test]
    public void AdjustmentRepeater_ResetsOnReleaseAndChangesDirectionImmediately()
    {
        var repeater = new Patches.CruiseAdjustmentRepeater();

        repeater.Update(increaseHeld: true, decreaseHeld: false, nowTick: 0).ShouldBe(1f);
        repeater.Update(increaseHeld: false, decreaseHeld: false, nowTick: 100).ShouldBe(0f);
        repeater.Update(increaseHeld: false, decreaseHeld: true, nowTick: 200).ShouldBe(-1f);
        repeater.Update(increaseHeld: true, decreaseHeld: true, nowTick: 300).ShouldBe(0f);
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
