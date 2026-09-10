using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Kontrol.Sdk.Compatibility;

/// <summary>Exact binary evidence used to validate a game-build identity.</summary>
public sealed record GameAssemblyFingerprint(
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("mvid")] string? Mvid = null);

/// <summary>Identity plus all binary evidence required for compatibility validation.</summary>
public sealed record GameBuildEvidence(
    [property: JsonPropertyName("identity")] GameBuildIdentity Identity,
    [property: JsonPropertyName("relevantAssemblies")] IReadOnlyList<GameAssemblyFingerprint> RelevantAssemblies)
{
    /// <summary>
    /// Produces a stable, non-user-facing identifier for fingerprint-only
    /// fallback. File-name comparison is case-insensitive and ordering does not
    /// affect the result.
    /// </summary>
    public string CreateFingerprintBuildId()
    {
        var canonical = RelevantAssemblies
            .OrderBy(assembly => assembly.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => $"{assembly.FileName.Trim().ToLowerInvariant()}|{assembly.Sha256.Trim().ToUpperInvariant()}|{assembly.Mvid?.Trim().ToUpperInvariant()}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", canonical))));
    }

    /// <summary>
    /// Returns this identity with a deterministic fingerprint fallback when no
    /// platform build or meaningful publisher version is available.
    /// </summary>
    public GameBuildIdentity CreateFingerprintFallbackIdentity() =>
        Identity.CanonicalIdentifier is null
            ? Identity with { FingerprintBuildId = CreateFingerprintBuildId() }
            : Identity;
}
