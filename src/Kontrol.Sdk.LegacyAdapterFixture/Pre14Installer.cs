using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;

namespace Kontrol.Sdk.LegacyAdapterFixture;

/// <summary>
/// This project intentionally compiles against the published 1.3.0 SDK package.
/// It must not reference deployment-plan types introduced in SDK 1.4.
/// </summary>
public sealed class Pre14Installer : IAdapterInstaller
{
    public DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) =>
        new("Legacy title", "Legacy summary", "Legacy effects");

    public DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) =>
        new(false, true, false, false);

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
    public void Uninstall(string gameDirectory, GameLaunchMethod method) { }
    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method) => false;
    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
}
