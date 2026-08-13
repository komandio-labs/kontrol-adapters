using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Inputs;

namespace Kontrol.Sdk.Interfaces;

public interface IAdapterInstaller
{
    /// <summary>Optional adapter-owned discovery location for non-store targets such as development sandboxes.</summary>
    string? GetSuggestedGameDirectory() => null;
    DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) => DeploymentMethodInformation.Generic(method);
    DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) => DeploymentMethodCapabilities.Standard;
    AdapterInputSchema GetInputSchema() => AdapterInputSchema.Empty;
    ProcessInjectionEntryPoint? GetProcessInjectionEntryPoint() => null;
    void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
    void Uninstall(string gameDirectory, GameLaunchMethod method);
    bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method);
    void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
    void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath);
}

/// <summary>
/// Describes the managed adapter method invoked after Kontrol attaches its
/// native bootstrap to a store-launched target process.
/// </summary>
public sealed record ProcessInjectionEntryPoint(string TypeName, string MethodName = "Initialize");
