using Kontrol.Sdk.Attributes;

namespace Kontrol.Sdk.Interfaces;

public sealed record DeploymentMethodInformation(string Title, string Summary, string Effects)
{
    public static DeploymentMethodInformation Generic(GameLaunchMethod method) => method switch
    {
        GameLaunchMethod.ProcessInjection => new("Process injection", "Loads the adapter into the target process without changing target files.", "This method requires a compatible runtime bootstrapper."),
        GameLaunchMethod.NativePluginParameter => new("Native plugin loader", "Launches the target using its supported plugin-loading mechanism.", "The adapter is supplied at launch; no original game assembly is changed."),
        GameLaunchMethod.AssemblyHooking => new("Assembly hook", "Adds a startup hook to a target assembly so it loads the adapter.", "A target assembly may be backed up and restored during uninstall."),
        GameLaunchMethod.BinPluginsFolder => new("Plugin folder", "Deploys the adapter to the target's plugin folder.", "Adapter files are copied to the target installation."),
        _ => new("Direct launch", "Starts the target through its adapter-provided launcher.", "The adapter controls any target-specific launch behavior.")
    };
}
