using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Inputs;

namespace Kontrol.Sdk.Interfaces;

public interface IAdapterInstaller
{
    /// <summary>Optional adapter-owned discovery location for non-store targets such as development sandboxes.</summary>
    string? GetSuggestedGameDirectory() => null;

    /// <summary>
    /// Legacy deployment presentation API.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="GetDeploymentPlan"/> instead. This member remains so
    /// adapters compiled against SDK 1.3 and earlier can run in a newer host.
    /// </remarks>
    [Obsolete("Implement GetDeploymentPlan(GameLaunchMethod) instead. This legacy member is used only as a fallback for adapters compiled against SDK 1.3 and earlier.")]
    DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) => DeploymentMethodInformation.Generic(method);

    /// <summary>
    /// Legacy deployment action-capability API.
    /// </summary>
    /// <remarks>
    /// Set <see cref="AdapterDeploymentPlan.Capabilities"/> in
    /// <see cref="GetDeploymentPlan"/> instead. This member remains so adapters
    /// compiled against SDK 1.3 and earlier can run in a newer host.
    /// </remarks>
    [Obsolete("Set AdapterDeploymentPlan.Capabilities in GetDeploymentPlan(GameLaunchMethod) instead. This legacy member is used only as a fallback for adapters compiled against SDK 1.3 and earlier.")]
    DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) => DeploymentMethodCapabilities.Standard;

    /// <summary>
    /// Legacy non-contextual deployment-plan API.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="GetDeploymentPlan(AdapterDeploymentContext)"/>
    /// instead. This member remains as the bridge from contextual host calls to
    /// adapters compiled against SDK 1.3 and earlier.
    /// </remarks>
    [Obsolete("Implement GetDeploymentPlan(AdapterDeploymentContext) instead. This non-contextual member is used only as a fallback for adapters compiled against SDK 1.3 and earlier.")]
#pragma warning disable CS0618 // Deliberately dispatches legacy members for pre-1.4 adapters.
    AdapterDeploymentPlan GetDeploymentPlan(GameLaunchMethod method) =>
        AdapterDeploymentPlan.Generic(method, GetDeploymentInformation(method), GetCapabilities(method));
#pragma warning restore CS0618

    /// <summary>
    /// Returns adapter-owned, resolved deployment facts for the selected target.
    /// </summary>
    /// <remarks>
    /// New adapters should implement this overload when prerequisites, targets,
    /// verification, or launch details depend on the selected game directory or
    /// packaged adapter path. Hosts must invoke it from an I/O-owning service,
    /// not a UI property getter. Older adapters inherit the generic fallback.
    /// </remarks>
    AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
#pragma warning disable CS0618 // Deliberately dispatches the legacy bridge for pre-1.4 adapters.
        return GetDeploymentPlan(context.Method);
#pragma warning restore CS0618
    }
    AdapterInputSchema GetInputSchema() => AdapterInputSchema.Empty;
    ProcessInjectionEntryPoint? GetProcessInjectionEntryPoint() => null;
    void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
    void Uninstall(string gameDirectory, GameLaunchMethod method);
    bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method);
    void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
    void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath, string? customLaunchArguments) =>
        Launch(gameDirectory, method, sourceDllPath);
    void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
    void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath, string? customLaunchArguments) =>
        CreateShortcut(gameDirectory, method, sourceDllPath);
}

/// <summary>
/// Describes the managed adapter method invoked after Kontrol attaches its
/// native bootstrap to a store-launched target process.
/// </summary>
public sealed record ProcessInjectionEntryPoint(string TypeName, string MethodName = "Initialize");
