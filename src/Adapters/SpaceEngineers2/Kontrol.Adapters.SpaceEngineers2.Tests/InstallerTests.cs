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
        SpaceEngineers2Installer.BuildNativePluginArgument(@"D:\Games\SE2\Game2\Kontrol.Adapters.SpaceEngineers2.dll")
            .ShouldBe(@"-plugins:D:\Games\SE2\Game2\Kontrol.Adapters.SpaceEngineers2.dll");
    }
}
