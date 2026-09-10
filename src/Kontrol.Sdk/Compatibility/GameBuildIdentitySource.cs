namespace Kontrol.Sdk.Compatibility;

/// <summary>The strongest available source for a game-build identity.</summary>
public enum GameBuildIdentitySource
{
    Unavailable,
    Fingerprint,
    ProductVersion,
    PlatformBuild
}
