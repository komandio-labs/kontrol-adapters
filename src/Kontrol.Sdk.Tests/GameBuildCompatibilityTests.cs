using Kontrol.Sdk.Compatibility;
using NUnit.Framework;
using Shouldly;
using System.Text.Json;

namespace Kontrol.Sdk.Tests;

[TestFixture]
public class GameBuildCompatibilityTests
{
    [Test]
    public void SteamIdentity_PrefersCanonicalSteamBuildOverGenericProductVersion()
    {
        var identity = GameBuildIdentity.Steam("244850", "24675677", "1.0.0");

        identity.CanonicalIdentifier.ShouldBe("steam-build-24675677");
        identity.Source.ShouldBe(GameBuildIdentitySource.PlatformBuild);
    }

    [Test]
    public void ProductVersionIdentity_IsAvailableOnlyWhenDeclaredMeaningful()
    {
        new GameBuildIdentity(null, null, null, "2.4.0.95", true).CanonicalIdentifier.ShouldBe("version-2.4.0.95");
        new GameBuildIdentity(null, null, null, "1.0.0", false).CanonicalIdentifier.ShouldBeNull();
    }

    [Test]
    public void FingerprintFallback_IsStableRegardlessOfAssemblyOrder()
    {
        var first = new GameBuildEvidence(new GameBuildIdentity(null, null, null, null, false), [
            new("VRage.dll", "ABC", "A"), new("Sandbox.Game.dll", "DEF", "B")]);
        var second = new GameBuildEvidence(new GameBuildIdentity(null, null, null, null, false), [
            new("Sandbox.Game.dll", "DEF", "B"), new("vrage.dll", "ABC", "A")]);

        first.CreateFingerprintBuildId().ShouldBe(second.CreateFingerprintBuildId());
        first.CreateFingerprintFallbackIdentity().CanonicalIdentifier.ShouldBe("fingerprint-" + first.CreateFingerprintBuildId());
    }

    [Test]
    public void Identity_SerializesOnlyStableContractFields()
    {
        string json = JsonSerializer.Serialize(GameBuildIdentity.Steam("244850", "24675677", "1.0.0"));

        json.ShouldContain("\"platform\":\"steam\"");
        json.ShouldContain("\"platformBuildId\":\"24675677\"");
        json.ShouldNotContain("canonicalIdentifier");
        JsonSerializer.Deserialize<GameBuildIdentity>(json)!.CanonicalIdentifier.ShouldBe("steam-build-24675677");
    }

    [Test]
    public void Evaluate_RequiresMatchingIdentityAndEveryRequiredFingerprint()
    {
        var expected = new GameBuildEvidence(GameBuildIdentity.Steam("244850", "24675677"), [new("VRage.dll", "ABC", "A")]);
        var verified = new GameBuildEvidence(GameBuildIdentity.Steam("244850", "24675677"), [new("VRage.dll", "ABC", "A")]);
        var mismatchedIdentity = new GameBuildEvidence(GameBuildIdentity.Steam("244850", "24675678"), [new("VRage.dll", "ABC", "A")]);
        var mismatchedFingerprint = new GameBuildEvidence(GameBuildIdentity.Steam("244850", "24675677"), [new("VRage.dll", "DEF", "A")]);

        GameBuildCompatibility.Evaluate(expected, verified).ShouldBe(GameBuildCompatibilityMatch.Verified);
        GameBuildCompatibility.Evaluate(expected, mismatchedIdentity).ShouldBe(GameBuildCompatibilityMatch.IdentityMismatch);
        GameBuildCompatibility.Evaluate(expected, mismatchedFingerprint).ShouldBe(GameBuildCompatibilityMatch.FingerprintMismatch);
    }
}
