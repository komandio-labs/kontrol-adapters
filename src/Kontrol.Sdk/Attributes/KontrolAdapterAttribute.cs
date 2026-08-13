using System;

namespace Kontrol.Sdk.Attributes;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class KontrolAdapterAttribute : Attribute
{
    public string GameId { get; }
    public string DisplayName { get; }
    public string DefaultSubFolder { get; }
    public string ExeName { get; }
    public string RelativeBinPath { get; }
    public string SteamAppId { get; }
    public bool RequiresHarmony { get; }
    public bool RequiresCore { get; }
    public GameLaunchMethod[] SupportedMethods { get; }
    public GameLaunchMethod DefaultDeploymentMethod { get; }

    public KontrolAdapterAttribute(
        string gameId,
        string displayName,
        string defaultSubFolder,
        string exeName,
        string relativeBinPath,
        string steamAppId,
        bool requiresHarmony,
        bool requiresCore,
        GameLaunchMethod[]? supportedMethods = null,
        GameLaunchMethod defaultDeploymentMethod = GameLaunchMethod.NativePluginParameter)
    {
        GameId = gameId;
        DisplayName = displayName;
        DefaultSubFolder = defaultSubFolder;
        ExeName = exeName;
        RelativeBinPath = relativeBinPath;
        SteamAppId = steamAppId;
        RequiresHarmony = requiresHarmony;
        RequiresCore = requiresCore;
        SupportedMethods = supportedMethods ?? new[] { GameLaunchMethod.NativePluginParameter };
        DefaultDeploymentMethod = defaultDeploymentMethod;
    }
}
