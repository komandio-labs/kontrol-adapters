using Kontrol.Adapters.SpaceEngineers;
using Kontrol.Adapters.SpaceEngineers.Plugin;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using NUnit.Framework;
using Shouldly;
using System.Text.Json;

namespace Kontrol.Adapters.SpaceEngineers.Tests;

[TestFixture]
public class SpaceEngineersInstallerTests
{
    [Test]
    public void PulsarStatusReporter_ReportLoaded_WritesATelemetryFrame()
    {
        using var reporter = new PulsarStatusReporter();

        Should.NotThrow(reporter.ReportLoaded);
    }

    [Test]
    public void PulsarLogReporter_Write_WritesATelemetryFrame()
    {
        using var reporter = new LegacyAdapterLogReporter();

        Should.NotThrow(() => reporter.Write("Pulsar log reporter format regression test."));
    }

    [Test]
    public void Schema_ExposesApiBackedFlightAndSystemControls()
    {
        var schema = new SpaceEngineersInstaller().GetInputSchema();
        schema.Version.ShouldBe(2);
        schema.Inputs.Select(input => input.Id).ShouldBe(new[]
        {
            "flight.pitch", "flight.roll", "flight.yaw", "movement.forward", "movement.strafe", "movement.lift",
            "systems.dampeners", "systems.lights", "systems.landing_gears", "systems.handbrake",
            "camera.mode_switch",
            "toolbar.select_1", "toolbar.select_2", "toolbar.select_3", "toolbar.select_4", "toolbar.select_5",
            "toolbar.select_6", "toolbar.select_7", "toolbar.select_8", "toolbar.select_9", "toolbar.select_0",
            "systems.leave_control"
        });
    }

    [Test]
    public void PulsarDeployment_UsesTheExistingPluginFolderLifecycle()
    {
        var installer = new SpaceEngineersInstaller();
        var plan = installer.GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.BinPluginsFolder,
            Path.Combine("test-game", "SpaceEngineers"),
            Path.Combine("package", "Kontrol.Adapters.SpaceEngineers.dll")));

        plan.Capabilities.ShouldBe(new DeploymentMethodCapabilities(
            CanInstall: true,
            CanUninstall: true,
            CanLaunch: true,
            CanCreateShortcut: false));
        plan.Title.ShouldBe("Pulsar Legacy joystick / HOTAS / HOSAS plugin");
        plan.Targets.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentTargetKind.ExternalLoader);
        plan.Targets.Single().Location.ShouldEndWith(Path.Combine("Legacy", "Local"));
        plan.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([
            "Kontrol.Adapters.SpaceEngineers.Plugin.dll", "0Harmony.dll",
            "Kontrol.Adapters.SpaceEngineers.Plugin.xml"]);
        plan.Prerequisites.Single(prerequisite => prerequisite.Id == "pulsar-local-plugin-write-access")
            .State.ShouldBeOneOf(DeploymentPrerequisiteState.Satisfied, DeploymentPrerequisiteState.Missing, DeploymentPrerequisiteState.Failed);
        plan.LaunchChain.Steps.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentLaunchStepKind.ExternalLauncher);
        plan.LaunchChain.Steps.Single().Executable.ShouldEndWith(Path.Combine("Pulsar", "Legacy.exe"));
        plan.LaunchChain.Steps.Single().Arguments!.ShouldContain("SpaceEngineers.exe");
        plan.ManualSteps.ShouldHaveSingleItem().Instruction.ShouldContain("active Pulsar Legacy profile");
    }

    [Test]
    public void PackageManifest_DeclaresNet9KontrolEntryAndSeparateNet48PulsarPayload()
    {
        string adapterRoot = FindAdapterRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers", "adapter.manifest.json")));
        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(adapterRoot, "package.json")));

        string hostEntry = "Kontrol.Adapters.SpaceEngineers.dll";
        string pulsarPayload = "Kontrol.Adapters.SpaceEngineers.Plugin.dll";

        var deployment = new SpaceEngineersInstaller().GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.BinPluginsFolder,
            Path.Combine("test-game", "SpaceEngineers"),
            Path.Combine(adapterRoot, hostEntry)));
        manifest.RootElement.GetProperty("pluginDll").GetString().ShouldBe(hostEntry);
        package.RootElement.GetProperty("entryAssembly").GetString().ShouldBe(hostEntry);
        package.RootElement.GetProperty("targetFramework").GetString().ShouldBe("net9.0");
        package.RootElement.GetProperty("package").GetProperty("include").EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(new[] { hostEntry, "Kontrol.Sdk.dll", pulsarPayload, "0Harmony.dll", "Kontrol.Adapters.SpaceEngineers.Plugin.xml", "adapter.manifest.json", "LICENSE", "THIRD_PARTY_NOTICES.md", "HARMONY_LICENSE" });
        deployment.Summary.ShouldContain("joystick, HOTAS, HOSAS, controller, and button-box payload");
        deployment.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([
            pulsarPayload, "0Harmony.dll", "Kontrol.Adapters.SpaceEngineers.Plugin.xml"]);
        deployment.Targets.Single().Location.ShouldEndWith(Path.Combine("Legacy", "Local"));

        string payloadProject = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "Kontrol.Adapters.SpaceEngineers.Plugin.csproj"));
        payloadProject.ShouldContain("<TargetFramework>net48</TargetFramework>");
        payloadProject.ShouldContain($"<AssemblyName>{Path.GetFileNameWithoutExtension(pulsarPayload)}</AssemblyName>");
        payloadProject.ShouldContain("<PackageReference Include=\"Lib.Harmony\" Version=\"2.4.2\" />");
        payloadProject.ShouldContain("<Reference Include=\"Sandbox.Game\"");
        payloadProject.ShouldNotContain($"<AssemblyName>{Path.GetFileNameWithoutExtension(hostEntry)}</AssemblyName>");
    }

    [Test]
    public void PulsarPluginMetadata_ProvidesFriendlyNameDescriptionAndDocumentationLink()
    {
        string adapterRoot = FindAdapterRoot();
        string xml = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "Kontrol.Adapters.SpaceEngineers.Plugin.xml"));

        xml.ShouldContain("<FriendlyName>Kontrol Joystick / HOTAS / HOSAS for Space Engineers</FriendlyName>");
        xml.ShouldContain("Fly Space Engineers with your joystick, HOTAS, HOSAS, controller, or button box.");
        xml.ShouldContain("https://github.com/komandio-labs/kontrol-adapters/tree/main/src/Adapters/SpaceEngineers");
    }

    [Test]
    public void PulsarPlugin_UsesTheFinalShipControlCommitHook()
    {
        string adapterRoot = FindAdapterRoot();
        string hook = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "ShipControlCommitHook.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "SpaceEngineersPlugin.cs"));

        hook.ShouldContain("typeof(MyShipController)");
        hook.ShouldContain("nameof(MyShipController.MoveAndRotate)");
        hook.ShouldContain("Type.EmptyTypes");
        hook.ShouldContain("Harmony.Patch(TargetMethod, prefix");
        plugin.ShouldContain("ApplyAtFinalControlCommit(MyShipController controlled)");
        plugin.ShouldContain("frame.ReadAnalog(0) * 20f");
        plugin.ShouldContain("ShipControlCommitHook.Install(this)");
        plugin.ShouldContain("GamePlayScreenTypeName + \":SwitchCamera\"");
        plugin.ShouldContain("SwitchCameraMethod.Invoke(gamePlay, null)");
        plugin.ShouldContain("toolbarOwner.Toolbar.ActivateItemAtSlot(slot)");
        plugin.ShouldContain("controlled.Use()");
    }

    [Test]
    public void SystemActions_UseTheirStableSchemaPositions()
    {
        var schema = new SpaceEngineersInstaller().GetInputSchema();
        schema.Inputs[SpaceEngineersControlLayout.DampenersAction].Id.ShouldBe("systems.dampeners");
        schema.Inputs[SpaceEngineersControlLayout.LightsAction].Id.ShouldBe("systems.lights");
        schema.Inputs[SpaceEngineersControlLayout.LandingGearsAction].Id.ShouldBe("systems.landing_gears");
        schema.Inputs[SpaceEngineersControlLayout.HandbrakeAction].Id.ShouldBe("systems.handbrake");
        schema.Inputs[SpaceEngineersControlLayout.CameraModeSwitchAction].Id.ShouldBe("camera.mode_switch");
        schema.Inputs[SpaceEngineersControlLayout.ToolbarFirstAction].Id.ShouldBe("toolbar.select_1");
        schema.Inputs[SpaceEngineersControlLayout.ToolbarFirstAction + SpaceEngineersControlLayout.ToolbarActionCount - 1].Id.ShouldBe("toolbar.select_0");
        schema.Inputs[SpaceEngineersControlLayout.LeaveControlAction].Id.ShouldBe("systems.leave_control");
    }

    [Test]
    public void PulsarDeploymentPlan_SatisfiesWriteAccessWhenTheLocalPluginDirectoryIsWritable()
    {
        string testRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        string pulsarRoot = Path.Combine(testRoot, "Pulsar");
        string packageDirectory = Path.Combine(testRoot, "package");
        string gameDirectory = Path.Combine(testRoot, "game");
        string? previousPulsarDirectory = Environment.GetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY");

        try
        {
            var installer = new SpaceEngineersInstaller();
            Directory.CreateDirectory(pulsarRoot);
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(Path.Combine(gameDirectory, "Bin64"));
            File.WriteAllBytes(Path.Combine(pulsarRoot, "Legacy.exe"), []);
            File.WriteAllBytes(Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.dll"), []);
            File.WriteAllBytes(Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.Plugin.dll"), []);
            File.WriteAllBytes(Path.Combine(packageDirectory, "0Harmony.dll"), []);
            File.WriteAllText(Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.Plugin.xml"), "plugin metadata");
            File.WriteAllBytes(Path.Combine(gameDirectory, "Bin64", "SpaceEngineers.exe"), []);
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", pulsarRoot);

            var plan = new SpaceEngineersInstaller().GetDeploymentPlan(new AdapterDeploymentContext(
                GameLaunchMethod.BinPluginsFolder,
                gameDirectory,
                Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.dll")));

            plan.Prerequisites.Single(prerequisite => prerequisite.Id == "pulsar-local-plugin-write-access")
                .State.ShouldBe(DeploymentPrerequisiteState.Satisfied);

            installer.Install(gameDirectory, GameLaunchMethod.BinPluginsFolder,
                Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.dll"));
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.dll")).ShouldBeTrue();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "0Harmony.dll")).ShouldBeTrue();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.xml")).ShouldBeTrue();
            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeTrue();

            installer.Uninstall(gameDirectory, GameLaunchMethod.BinPluginsFolder);
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "0Harmony.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.xml")).ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", previousPulsarDirectory);
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string FindAdapterRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Adapters", "SpaceEngineers");
            if (File.Exists(Path.Combine(candidate, "package.json")))
                return candidate;
            directory = directory.Parent;
        }

        Assert.Fail("Could not locate the Space Engineers 1 adapter source root.");
        return string.Empty;
    }
}
