namespace Kontrol.Sdk.Attributes;

public enum GameLaunchMethod
{
    /// <summary>
    /// Launches via native game plugin loading parameters (e.g. -plugins:path)
    /// </summary>
    NativePluginParameter,

    /// <summary>
    /// Deploys DLL to bin/plugins folder (standard plugins folder injection)
    /// </summary>
    BinPluginsFolder,

    /// <summary>
    /// Bootstrap-hooks a game assembly to load the DLL at startup without launch arguments
    /// </summary>
    AssemblyHooking,

    /// <summary>
    /// Loads an adapter into a running target process without modifying the target's files.
    /// The adapter supplies the target-specific injection configuration; Kontrol supplies the
    /// runtime-family bootstrapper.
    /// </summary>
    ProcessInjection,

}
