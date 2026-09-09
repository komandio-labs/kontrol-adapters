using Kontrol.Sdk.Attributes;

namespace Kontrol.Sdk.Interfaces;

/// <summary>
/// Adapter-owned inputs used to resolve deployment facts for the currently
/// selected target.
/// </summary>
/// <remarks>
/// Hosts must evaluate this context from a service/background operation, never
/// from a UI property getter. The adapter can use it to report the exact game,
/// loader, and package paths involved in deployment.
/// </remarks>
public sealed record AdapterDeploymentContext(
    GameLaunchMethod Method,
    string GameDirectory,
    string SourceDllPath);

/// <summary>Adapter-owned deployment facts for one technical launch method.</summary>
/// <remarks>
/// This is descriptive contract data. The existing installer methods remain the
/// execution API, so a host can present exact loader-specific behavior without
/// inferring it from <see cref="GameLaunchMethod"/>.
/// </remarks>
public sealed record AdapterDeploymentPlan(
    GameLaunchMethod Method,
    DeploymentMethodCapabilities Capabilities,
    string Title,
    string Summary,
    string Effects,
    string Consent,
    IReadOnlyList<DeploymentPrerequisite> Prerequisites,
    IReadOnlyList<DeploymentTarget> Targets,
    DeploymentLaunchChain LaunchChain,
    IReadOnlyList<DeploymentManualStep> ManualSteps,
    DeploymentVerification Verification,
    DeploymentRollbackPlan Rollback)
{
#pragma warning disable CS0618 // Deliberately constructs fallback data for pre-1.4 adapters.
    /// <summary>Creates conservative presentation data for an older adapter.</summary>
    internal static AdapterDeploymentPlan Generic(GameLaunchMethod method) =>
        Generic(method, DeploymentMethodInformation.Generic(method), DeploymentMethodCapabilities.Standard);

    /// <summary>
    /// Creates conservative presentation data while preserving legacy adapter
    /// overrides for method information and action capabilities.
    /// </summary>
    internal static AdapterDeploymentPlan Generic(
        GameLaunchMethod method,
        DeploymentMethodInformation information,
        DeploymentMethodCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(capabilities);

        return new AdapterDeploymentPlan(
            method,
            capabilities,
            information.Title,
            information.Summary,
            information.Effects,
            "Review the deployment effects and confirm them before continuing.",
            Array.Empty<DeploymentPrerequisite>(),
            [DeploymentTarget.Generic()],
            DeploymentLaunchChain.Generic(),
            Array.Empty<DeploymentManualStep>(),
            DeploymentVerification.Generic(),
            DeploymentRollbackPlan.Generic());
    }
#pragma warning restore CS0618
}

/// <summary>Reports discovery and validation for a deployment prerequisite.</summary>
public sealed record DeploymentPrerequisite(
    string Id,
    string Title,
    string Description,
    DeploymentPrerequisiteState State,
    string? ResolvedValue,
    string Remediation,
    bool IsBlocking = true);

public enum DeploymentPrerequisiteState
{
    Unknown,
    Checking,
    Satisfied,
    Missing,
    Failed
}

/// <summary>Describes a location and the files/configuration owned by the adapter there.</summary>
public sealed record DeploymentTarget(
    string Id,
    string Title,
    DeploymentTargetKind Kind,
    string Location,
    IReadOnlyList<DeploymentOwnedFile> OwnedFiles,
    IReadOnlyList<DeploymentConfigurationEffect> ConfigurationEffects)
{
    internal static DeploymentTarget Generic() => new(
        "legacy-adapter-target",
        "Adapter-managed deployment target",
        DeploymentTargetKind.Other,
        "Detailed location unavailable from this legacy adapter.",
        Array.Empty<DeploymentOwnedFile>(),
        Array.Empty<DeploymentConfigurationEffect>());
}

public sealed record DeploymentOwnedFile(string Path, string Description);

public sealed record DeploymentConfigurationEffect(string Scope, string Description);

public enum DeploymentTargetKind
{
    GameInstallation,
    ExternalLoader,
    KontrolManaged,
    Other
}

/// <summary>Describes the ordered launcher/bootstrap chain used by a method.</summary>
public sealed record DeploymentLaunchChain(IReadOnlyList<DeploymentLaunchStep> Steps)
{
    internal static DeploymentLaunchChain Generic() => new(
        [new DeploymentLaunchStep(
            "Adapter-managed launch chain",
            DeploymentLaunchStepKind.Other,
            "Detailed launch behavior is unavailable from this legacy adapter.")]);
}

public sealed record DeploymentLaunchStep(
    string Title,
    DeploymentLaunchStepKind Kind,
    string Description,
    string? Executable = null,
    string? Arguments = null);

public enum DeploymentLaunchStepKind
{
    DirectGame,
    Steam,
    ExternalLauncher,
    AdapterBootstrap,
    Other
}

public sealed record DeploymentManualStep(string Instruction, bool Required = true);

/// <summary>Reports deployment state and runtime readiness for the plan.</summary>
public sealed record DeploymentVerification(
    DeploymentState DeploymentState,
    string DeploymentMessage,
    DeploymentRuntimeState RuntimeState,
    string RuntimeMessage)
{
    internal static DeploymentVerification Generic() => new(
        DeploymentState.NotConfigured,
        "Deployment readiness is provided by the adapter's existing installer behavior.",
        DeploymentRuntimeState.Unknown,
        "Runtime verification is provided by the adapter and target loader.");
}

public enum DeploymentRuntimeState
{
    Unknown,
    NotRunning,
    Starting,
    Ready,
    Failed
}

/// <summary>Describes the adapter-owned effects of uninstall and rollback.</summary>
public sealed record DeploymentRollbackPlan(string Summary, IReadOnlyList<DeploymentRollbackEffect> Effects)
{
    internal static DeploymentRollbackPlan Generic() => new(
        "The adapter's existing uninstall operation reverses its deployment.",
        Array.Empty<DeploymentRollbackEffect>());
}

public sealed record DeploymentRollbackEffect(string Target, string Description);
