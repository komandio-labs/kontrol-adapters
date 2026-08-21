using System.Runtime.CompilerServices;
using Kontrol.Sdk.Attributes;

[assembly: InternalsVisibleTo("Kontrol.Adapters.SpaceEngineers2.Tests")]
[assembly: KontrolAdapter(
    "space-engineers-2",
    "Space Engineers 2",
    "space-engineers-2",
    "SpaceEngineers2.exe",
    "Game2",
    "1133870",
    requiresHarmony: true,
    requiresCore: true,
    supportedMethods: [GameLaunchMethod.NativePluginParameter, GameLaunchMethod.ProcessInjection],
    defaultDeploymentMethod: GameLaunchMethod.NativePluginParameter)]
