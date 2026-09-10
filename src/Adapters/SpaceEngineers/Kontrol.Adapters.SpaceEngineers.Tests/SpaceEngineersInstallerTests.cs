using Kontrol.Adapters.SpaceEngineers;
using Kontrol.Adapters.SpaceEngineers.Plugin;
using Kontrol.Adapters.SpaceEngineers.Settings;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.IPC;
using NUnit.Framework;
using Shouldly;
using System.Text.Json;

namespace Kontrol.Adapters.SpaceEngineers.Tests;

[TestFixture]
public class SpaceEngineersInstallerTests
{
    [Test]
    public void SdkStatusReporter_ReportLoaded_WritesATelemetryFrame()
    {
        string adapterId = $"space-engineers-{Guid.NewGuid():N}";
        using var receiver = new MmfChannel<TelemetryData>($"Local\\Kontrol_AdapterStatus_{adapterId}");
        receiver.CreateOrOpen();
        using var reporter = new AdapterConnectionReporter(adapterId);

        reporter.ReportLoaded();
        receiver.Read(out var frame);
        frame.GetJson().ShouldContain("\"state\":\"Loaded\"");
        frame.GetJson().ShouldContain("\"timestampUnixMilliseconds\":");
    }

    [Test]
    public void SdkLogReporter_Write_WritesATelemetryFrame()
    {
        string adapterId = $"space-engineers-{Guid.NewGuid():N}";
        using var receiver = new MmfChannel<TelemetryData>($"Local\\Kontrol_Logs_{adapterId}");
        receiver.CreateOrOpen();
        using var reporter = new AdapterLogReporter(adapterId);

        reporter.Write("Pulsar log reporter format regression test.");
        receiver.Read(out var frame);
        frame.GetJson().ShouldContain("\"sequence\":1");
        frame.GetJson().ShouldContain("\"message\":\"Pulsar log reporter format regression test.\"");
    }

    [Test]
    public void Schema_ExposesApiBackedFlightAndSystemControls()
    {
        var schema = new SpaceEngineersInstaller().GetInputSchema();
        schema.Version.ShouldBe(1);
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
            "Kontrol.Sdk.dll and JSON runtime dependencies",
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
            .ShouldContain("Kontrol.Sdk.dll");
        package.RootElement.GetProperty("package").GetProperty("include").EnumerateArray()
            .Select(item => item.GetString())
            .ShouldContain("System.Text.Json.dll");
        deployment.Summary.ShouldContain("joystick, HOTAS, HOSAS, controller, and button-box payload");
        deployment.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([
            pulsarPayload, "0Harmony.dll", "Kontrol.Sdk.dll and JSON runtime dependencies", "Kontrol.Adapters.SpaceEngineers.Plugin.xml"]);
        deployment.Targets.Single().Location.ShouldEndWith(Path.Combine("Legacy", "Local"));

        string payloadProject = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "Kontrol.Adapters.SpaceEngineers.Plugin.csproj"));
        payloadProject.ShouldContain("<TargetFramework>net48</TargetFramework>");
        payloadProject.ShouldContain($"<AssemblyName>{Path.GetFileNameWithoutExtension(pulsarPayload)}</AssemblyName>");
        payloadProject.ShouldContain("<PackageReference Include=\"Lib.Harmony\" Version=\"2.4.2\" />");
        payloadProject.ShouldContain("<ProjectReference Include=\"..\\..\\..\\Kontrol.Sdk\\Kontrol.Sdk.csproj\" />");
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
        xml.ShouldContain("pitch, roll, yaw, forward/reverse thrust, strafe, lift, dampeners, lights, landing gear, handbrake, camera-mode switch, toolbar slots 1-10, and leave vehicle/cockpit (F)");
        xml.ShouldContain("https://www.komandio.com/kontrol");
    }

    [Test]
    public void PulsarPlugin_AppliesAxesAtTheFinalShipControlCommit()
    {
        string adapterRoot = FindAdapterRoot();
        string plugin = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "SpaceEngineersPlugin.cs"));

        plugin.ShouldContain("ShipControlCommitHook.Install(this);");
        plugin.ShouldContain("internal void ApplyAtFinalControlCommit(MyShipController controlled)");
        plugin.ShouldContain("controlled.MoveAndRotate(movement, rotation, frame.ReadAnalog(1) * sensitivities.Roll);");
        plugin.ShouldContain("[InputTrace] Committed pitch=");
        plugin.ShouldContain("GamePlayScreenTypeName + \":SwitchCamera\"");
        plugin.ShouldContain("SwitchCameraMethod.Invoke(gamePlay, null)");
        plugin.ShouldContain("toolbarOwner.Toolbar.ActivateItemAtSlot(slot)");
        plugin.ShouldContain("controlled.Use()");

        string hook = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "ShipControlCommitHook.cs"));
        hook.ShouldContain("MyShipController.UpdateControls() control commit");
        hook.ShouldContain("Harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PrefixMethod), postfix: new HarmonyMethod(PostfixMethod));");
        plugin.ShouldContain("Final control prefix rejected:");
        plugin.ShouldContain("Final control prefix prepared indicators:");
        plugin.ShouldContain("Final control postfix observed:");
    }

    [Test]
    public void PulsarPlugin_UsesTheSharedSdkIpcAndRecordsIndependentStartupDiagnostics()
    {
        string adapterRoot = FindAdapterRoot();
        string trace = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "PulsarStartupTrace.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "SpaceEngineersPlugin.cs"));

        trace.ShouldContain("pulsar-startup-trace.log");
        plugin.ShouldContain("new MmfChannel<InputFrame>(InputMapName)");
        plugin.ShouldContain("new AdapterConnectionReporter(\"space-engineers\")");
        plugin.ShouldContain("new AdapterLogReporter(\"space-engineers\")");
        plugin.ShouldNotContain("private unsafe struct InputFrame");
        plugin.ShouldContain("Plugin Init opening input channel.");
        plugin.ShouldContain("Plugin Init final ship-control hook installed.");
        plugin.ShouldContain("Plugin Init completed.");
    }

    [Test]
    public void AdapterSettings_ExposeRealtimePerAxisFlightSensitivityWithWorkingDefaults()
    {
        var provider = new SpaceEngineersSettingsProvider();

        provider.AdapterId.ShouldBe("space-engineers");
        provider.Descriptors.Select(descriptor => descriptor.Key).ShouldBe([
            "flight.pitchSensitivity", "flight.yawSensitivity", "flight.rollSensitivity"]);
        provider.Descriptors.ShouldAllBe(descriptor => descriptor.UpdateScope == Kontrol.Sdk.Settings.SettingUpdateScope.Realtime);
        provider.GetDefaultSnapshot().GetNumber("flight.pitchSensitivity", 0f).ShouldBe(20f);
        provider.GetDefaultSnapshot().GetNumber("flight.yawSensitivity", 0f).ShouldBe(20f);
        provider.GetDefaultSnapshot().GetNumber("flight.rollSensitivity", 0f).ShouldBe(1f);
    }

    [Test]
    public void LegacyFlightSettingsReader_ParsesAndClampsHostSettingsPayload()
    {
        string adapterRoot = FindAdapterRoot();
        string settings = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "LegacyFlightSettings.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "SpaceEngineersPlugin.cs"));

        settings.ShouldContain("Local\\Kontrol_Settings_space-engineers");
        settings.ShouldContain("flight.pitchSensitivity");
        settings.ShouldContain("return Math.Max(0.1f, Math.Min(40f, value));");
        plugin.ShouldContain("_settings.Refresh();");
        plugin.ShouldContain("frame.ReadAnalog(0) * sensitivities.Pitch");
        plugin.ShouldContain("frame.ReadAnalog(2) * sensitivities.Yaw");
        plugin.ShouldContain("frame.ReadAnalog(1) * sensitivities.Roll");
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
            foreach (string dependency in PulsarRuntimeSourceDependencies)
                File.WriteAllBytes(Path.Combine(packageDirectory, dependency), []);
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
            foreach (string dependency in PulsarRuntimeTargetDependencies)
                File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", dependency)).ShouldBeTrue();
            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeTrue();

            installer.Uninstall(gameDirectory, GameLaunchMethod.BinPluginsFolder);
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "0Harmony.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", "Kontrol.Adapters.SpaceEngineers.Plugin.xml")).ShouldBeFalse();
            foreach (string dependency in PulsarRuntimeTargetDependencies)
                File.Exists(Path.Combine(pulsarRoot, "Legacy", "Local", dependency)).ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", previousPulsarDirectory);
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    private static readonly string[] PulsarRuntimeSourceDependencies =
    [
        "Kontrol.Sdk.Pulsar.dll", "Microsoft.Bcl.AsyncInterfaces.dll", "System.Buffers.dll", "System.IO.Pipelines.dll",
        "System.Memory.dll", "System.Numerics.Vectors.dll", "System.Runtime.CompilerServices.Unsafe.dll",
        "System.Text.Encodings.Web.dll", "System.Text.Json.dll", "System.Threading.Tasks.Extensions.dll", "System.ValueTuple.dll"
    ];

    private static readonly string[] PulsarRuntimeTargetDependencies =
    [
        "Kontrol.Sdk.dll", "Microsoft.Bcl.AsyncInterfaces.dll", "System.Buffers.dll", "System.IO.Pipelines.dll",
        "System.Memory.dll", "System.Numerics.Vectors.dll", "System.Runtime.CompilerServices.Unsafe.dll",
        "System.Text.Encodings.Web.dll", "System.Text.Json.dll", "System.Threading.Tasks.Extensions.dll", "System.ValueTuple.dll"
    ];

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
