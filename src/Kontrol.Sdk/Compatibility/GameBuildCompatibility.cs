namespace Kontrol.Sdk.Compatibility;

/// <summary>Result of comparing a claimed compatibility record with local evidence.</summary>
public enum GameBuildCompatibilityMatch
{
    InsufficientEvidence,
    IdentityMismatch,
    FingerprintMismatch,
    Verified
}

/// <summary>Shared exact-match rules for game-build compatibility evidence.</summary>
public static class GameBuildCompatibility
{
    public static GameBuildCompatibilityMatch Evaluate(GameBuildEvidence expected, GameBuildEvidence actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        string? expectedIdentity = expected.Identity.CanonicalIdentifier;
        string? actualIdentity = actual.Identity.CanonicalIdentifier;
        if (string.IsNullOrWhiteSpace(expectedIdentity) || string.IsNullOrWhiteSpace(actualIdentity) ||
            expected.RelevantAssemblies.Count == 0 || actual.RelevantAssemblies.Count == 0)
            return GameBuildCompatibilityMatch.InsufficientEvidence;

        if (!string.Equals(expectedIdentity, actualIdentity, StringComparison.OrdinalIgnoreCase))
            return GameBuildCompatibilityMatch.IdentityMismatch;

        var actualByFile = actual.RelevantAssemblies.ToDictionary(assembly => assembly.FileName, StringComparer.OrdinalIgnoreCase);
        foreach (GameAssemblyFingerprint required in expected.RelevantAssemblies)
        {
            if (!actualByFile.TryGetValue(required.FileName, out GameAssemblyFingerprint? observed) ||
                !string.Equals(required.Sha256, observed.Sha256, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(required.Mvid) && !string.Equals(required.Mvid, observed.Mvid, StringComparison.OrdinalIgnoreCase)))
                return GameBuildCompatibilityMatch.FingerprintMismatch;
        }

        return GameBuildCompatibilityMatch.Verified;
    }
}
