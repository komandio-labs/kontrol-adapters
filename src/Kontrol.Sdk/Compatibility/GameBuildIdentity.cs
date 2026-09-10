using System.Text.Json.Serialization;

namespace Kontrol.Sdk.Compatibility;

/// <summary>
/// A platform-neutral identity for a game build. A platform build is preferred
/// over a publisher version when both are available because it identifies the
/// exact distributed build.
/// </summary>
public sealed record GameBuildIdentity(
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("platformAppId")] string? PlatformAppId,
    [property: JsonPropertyName("platformBuildId")] string? PlatformBuildId,
    [property: JsonPropertyName("productVersion")] string? ProductVersion,
    [property: JsonPropertyName("isProductVersionMeaningful")] bool IsProductVersionMeaningful,
    [property: JsonPropertyName("fingerprintBuildId")] string? FingerprintBuildId = null)
{
    [JsonIgnore]
    public string? CanonicalIdentifier
    {
        get
        {
            if (TryGetText(Platform, out string platform) && TryGetText(PlatformBuildId, out string platformBuildId))
                return $"{NormalizePlatform(platform)}-build-{platformBuildId}";
            if (IsProductVersionMeaningful && TryGetText(ProductVersion, out string productVersion))
                return $"version-{productVersion}";
            return TryGetText(FingerprintBuildId, out string fingerprintBuildId)
                ? $"fingerprint-{fingerprintBuildId}"
                : null;
        }
    }

    [JsonIgnore]
    public GameBuildIdentitySource Source =>
        TryGetText(Platform, out _) && TryGetText(PlatformBuildId, out _)
            ? GameBuildIdentitySource.PlatformBuild
            : IsProductVersionMeaningful && TryGetText(ProductVersion, out _)
                ? GameBuildIdentitySource.ProductVersion
                : TryGetText(FingerprintBuildId, out _)
                    ? GameBuildIdentitySource.Fingerprint
                    : GameBuildIdentitySource.Unavailable;

    public static GameBuildIdentity Steam(string appId, string buildId, string? productVersion = null, bool isProductVersionMeaningful = false) =>
        new("steam", appId, buildId, productVersion, isProductVersionMeaningful);

    private static bool TryGetText(string? value, out string text)
    {
        text = value?.Trim() ?? string.Empty;
        return text.Length > 0;
    }

    private static string NormalizePlatform(string platform) => platform.ToLowerInvariant();
}
