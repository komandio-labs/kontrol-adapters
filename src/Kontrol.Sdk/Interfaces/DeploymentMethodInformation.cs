using Kontrol.Sdk.Attributes;

namespace Kontrol.Sdk.Interfaces;

[Obsolete("Use AdapterDeploymentPlan returned by IAdapterInstaller.GetDeploymentPlan(AdapterDeploymentContext) instead. This type remains for fallback support of adapters compiled against SDK 1.3 and earlier.")]
public sealed record DeploymentMethodInformation(string Title, string Summary, string Effects)
{
    public static DeploymentMethodInformation Generic(GameLaunchMethod method)
    {
        _ = method;
        return new(
            "Legacy adapter deployment",
            "This adapter was built against an earlier Kontrol SDK and does not provide detailed deployment facts.",
            "Review the adapter's documentation before deploying, launching, or removing it.");
    }
}
