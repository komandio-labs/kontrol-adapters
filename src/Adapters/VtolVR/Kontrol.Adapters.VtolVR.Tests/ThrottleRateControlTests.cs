using Kontrol.Adapters.VtolVR;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.VtolVR.Tests;

[TestFixture]
public sealed class ThrottleRateControlTests
{
    [TestCase(0.4f, 0f, 0.5f, 1f, 0.4f)]
    [TestCase(0.4f, 1f, 0.5f, 1f, 0.9f)]
    [TestCase(0.4f, -1f, 0.5f, 1f, 0f)]
    [TestCase(0.4f, 0.5f, 0.5f, 1f, 0.65f)]
    [TestCase(0.9f, 1f, 0.5f, 1f, 1f)]
    [TestCase(0.1f, -1f, 0.5f, 1f, 0f)]
    public void Advance_AppliesSignedProportionalRateAndClamps(
        float current, float axis, float rate, float deltaTime, float expected)
    {
        ThrottleRateControl.Advance(current, axis, rate, deltaTime).ShouldBe(expected, 0.0001f);
    }
}
