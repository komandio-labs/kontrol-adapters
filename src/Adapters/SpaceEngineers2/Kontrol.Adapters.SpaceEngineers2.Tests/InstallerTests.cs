using Kontrol.Adapters.SpaceEngineers2;
using Kontrol.Sdk.Attributes;
using NUnit.Framework;
using Shouldly;
using System.Reflection;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class InstallerTests
{
    [Test]
    public void NativePluginParameter_Uninstall_RemovesOnlyKontrolDeploymentArtifacts()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"Kontrol_SE2Installer_{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(testRoot, "Game2");
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(sourceDirectory);

        string sourcePlugin = Path.Combine(sourceDirectory, "Kontrol.Adapters.SpaceEngineers2.dll");
        File.WriteAllText(sourcePlugin, "test adapter");
        File.WriteAllText(Path.Combine(sourceDirectory, "0Harmony.dll"), "test harmony");
        File.WriteAllText(Path.Combine(sourceDirectory, "Kontrol.Sdk.dll"), "test sdk");
        string unrelatedGameFile = Path.Combine(gameDirectory, "VRage.Library.dll");
        File.WriteAllText(unrelatedGameFile, "original game file");

        try
        {
            var installer = new SpaceEngineers2Installer();

            installer.Install(testRoot, GameLaunchMethod.NativePluginParameter, sourcePlugin);
            installer.CheckIsInstalled(testRoot, GameLaunchMethod.NativePluginParameter).ShouldBeTrue();
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt")).ShouldBeTrue();

            installer.Uninstall(testRoot, GameLaunchMethod.NativePluginParameter);

            installer.CheckIsInstalled(testRoot, GameLaunchMethod.NativePluginParameter).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "Kontrol.Adapters.SpaceEngineers2.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "0Harmony.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "Kontrol.Sdk.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt")).ShouldBeFalse();
            File.Exists(unrelatedGameFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Test]
    public void AdapterMetadata_PresentsNativePluginAsDefaultAndRetainsProcessInjection()
    {
        var metadata = typeof(SpaceEngineers2Installer).Assembly.GetCustomAttribute<KontrolAdapterAttribute>();

        metadata.ShouldNotBeNull();
        metadata.DefaultDeploymentMethod.ShouldBe(GameLaunchMethod.NativePluginParameter);
        metadata.SupportedMethods.ShouldBe([GameLaunchMethod.NativePluginParameter, GameLaunchMethod.ProcessInjection]);
    }

    [Test]
    public void ProcessInjection_DeclaresTheManagedStartupEntryPoint()
    {
        var entryPoint = new SpaceEngineers2Installer().GetProcessInjectionEntryPoint();

        entryPoint.ShouldNotBeNull();
        entryPoint.TypeName.ShouldBe("Kontrol.Adapters.SpaceEngineers2.SpaceEngineers2StartupHook");
        entryPoint.MethodName.ShouldBe("Initialize");
    }

    [Test]
    public void AssemblyHooking_IsNotSupported()
    {
        new SpaceEngineers2Installer().GetCapabilities(Kontrol.Sdk.Attributes.GameLaunchMethod.AssemblyHooking)
            .CanLaunch.ShouldBeFalse();
    }

    [Test]
    public void NativePluginParameter_UsesThePluginLoaderArgumentSyntax()
    {
        string pluginPath = Path.Combine("test-game", "Game2", "Kontrol.Adapters.SpaceEngineers2.dll");

        SpaceEngineers2Installer.BuildNativePluginArgument(pluginPath)
            .ShouldBe($"-plugins:{pluginPath}");
    }
}
