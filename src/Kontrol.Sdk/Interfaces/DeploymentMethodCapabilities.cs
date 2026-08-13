namespace Kontrol.Sdk.Interfaces;

public sealed record DeploymentMethodCapabilities(bool CanInstall, bool CanUninstall, bool CanLaunch, bool CanCreateShortcut)
{
    public static readonly DeploymentMethodCapabilities Standard = new(true, true, true, true);
    public static readonly DeploymentMethodCapabilities NoDeploymentRequired = new(false, false, true, false);
    public static readonly DeploymentMethodCapabilities Unavailable = new(false, false, false, false);
}
