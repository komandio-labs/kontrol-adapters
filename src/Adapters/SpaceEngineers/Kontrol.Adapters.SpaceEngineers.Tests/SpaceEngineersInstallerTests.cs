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
    public void Schema_ExposesTheCompleteJoystickControlSet()
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
            "systems.leave_control", "systems.reactors", "interface.terminal", "interface.inventory",
            "weapons.primary", "weapons.secondary", "systems.broadcasting", "systems.local_power", "interface.hud",
            "communication.chat", "communication.voice", "camera.hold_look_around", "camera.toggle_look_around",
            "camera.look_horizontal", "camera.look_vertical", "camera.zoom"
        });

        schema.Inputs.Single(input => input.Id == "systems.landing_gears").DisplayName.ShouldBe("Park");
        schema.Inputs.Single(input => input.Id == "systems.reactors").DisplayName.ShouldBe("Power switch on / off");
        schema.Inputs.Single(input => input.Id == "systems.leave_control").DisplayName.ShouldBe("Use / Interact");
        schema.Inputs.Single(input => input.Id == "interface.terminal").DisplayName.ShouldBe("Terminal / Inventory");
        schema.Inputs.ShouldNotContain(input => input.Id == "interface.remote_access");
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
            .ShouldContain("Kontrol.Sdk.Pulsar.dll");
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
        payloadProject.ShouldContain("<Reference Include=\"VRage.Library\"");
        payloadProject.ShouldContain("<Reference Include=\"VRage.Input\"");
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
        xml.ShouldContain("Use tool / Fire weapon");
        xml.ShouldContain("Secondary mode");
        xml.ShouldContain("Inertia dampeners on / off");
        xml.ShouldContain("Broadcasting");
        xml.ShouldContain("camera look-around");
        xml.ShouldContain("third-person zoom");
        xml.ShouldContain("Chat screen");
        xml.ShouldContain("Voice Chat");
        xml.ShouldContain("toolbar slots 1-0");
        xml.ShouldNotContain("remote access");
        xml.ShouldContain("https://www.komandio.com/kontrol");
    }

    [Test]
    public void PulsarPlugin_AppliesAxesAtTheFinalShipControlCommit()
    {
        string adapterRoot = FindAdapterRoot();
        string plugin = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "SpaceEngineersPlugin.cs"));

        plugin.ShouldContain("ShipControlCommitHook.Install(this);");
        plugin.ShouldContain("internal void MergeAtFinalControlCommit(MyShipController controlled, ref Vector3 movement, ref Vector2 rotation, ref float roll)");
        plugin.ShouldContain("var nativeMovement = movement;");
        plugin.ShouldContain("var nativeRotation = rotation;");
        plugin.ShouldNotContain("controlled.MoveAndRotate(movement, rotation, roll);");
        plugin.ShouldContain("[InputTrace] Merged Kontrol with native SE1 input");
        plugin.ShouldContain("GamePlayScreenTypeName + \":SwitchCamera\"");
        plugin.ShouldContain("SwitchCameraMethod.Invoke(gamePlay, null)");
        plugin.ShouldContain("toolbarOwner.Toolbar.ActivateItemAtSlot(slot)");
        plugin.ShouldContain("controlled.Use()");
        plugin.ShouldContain("controlled.SwitchReactors()");
        plugin.ShouldContain("controlled.ShowTerminal()");
        plugin.ShouldContain("controlled.ShowInventory()");
        plugin.ShouldContain("ship.BeginShootSync(MyShootActionEnum.PrimaryAction)");
        plugin.ShouldContain("ship.BeginShootSync(MyShootActionEnum.SecondaryAction)");
        plugin.ShouldContain("ship.SwitchBroadcasting()");
        plugin.ShouldContain("ship.SwitchReactorsLocal()");
        plugin.ShouldContain("MyHud.ToggleGamepadHud()");
        plugin.ShouldContain("MyVoiceChatSessionComponent.Static");
        plugin.ShouldContain("ToggleChatScreen()");
        plugin.ShouldContain("ShipControlCommitHook.RotateCameraWithoutZoom(cameraController, new Vector2(");
        plugin.ShouldContain("CameraInputMath.ResolveLookAxis(");
        plugin.ShouldContain("CameraInputMath.ResolveShipAxis(nativeMovement.X, frame.ReadAnalog(4), false)");
        plugin.ShouldContain("CameraInputMath.ResolveShipAxis(nativeMovement.Y, frame.ReadAnalog(5), false)");
        plugin.ShouldContain("CameraInputMath.ResolveShipAxis(nativeMovement.Z, forwardAxis, cameraLookActive)");
        plugin.ShouldContain("CameraInputMath.ResolveShipAxis(nativeRoll, frame.ReadAnalog(1) * sensitivities.Roll, false)");
        plugin.ShouldContain("_lastCameraLookUtc = default(DateTime);");
        plugin.ShouldContain("MergeNativeCameraZoom");
        plugin.ShouldContain("Native camera zoom: mode=third-person-look-around;");
        plugin.ShouldContain("Camera routing: mode={0};");
        plugin.ShouldContain("rawKontrol[dedicated=");
        plugin.ShouldContain("nativeBefore=");
        plugin.ShouldContain("mergedAfter=");
        plugin.ShouldContain("forwardThrustSuppressed=");
        plugin.ShouldContain("MyControlsSpace.CAMERA_ZOOM_IN");
        plugin.ShouldNotContain("spectator.ResetViewerDistance(nextDistance)");
        plugin.ShouldNotContain("ThirdPersonLookAtField");

        string hook = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "ShipControlCommitHook.cs"));
        hook.ShouldContain("nameof(MyShipController.MoveAndRotate)");
        hook.ShouldContain("Harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PrefixMethod));");
        hook.ShouldContain("prefix: new HarmonyMethod(ZoomPrefixMethod)");
        hook.ShouldContain("postfix: new HarmonyMethod(ZoomPostfixMethod)");
        hook.ShouldContain("transpiler: new HarmonyMethod(ZoomTranspilerMethod)");
        hook.ShouldContain("lookAroundReplacements != 1 || zoomInReplacements != 1 || zoomOutReplacements != 1");
        hook.ShouldContain("instruction.opcode != OpCodes.Ldsfld");
        hook.ShouldContain("nameof(MyControlsSpace.CAMERA_ZOOM_IN)");
        hook.ShouldContain("nameof(MyControlsSpace.CAMERA_ZOOM_OUT)");
        hook.ShouldContain("input.IsLookAround() || _mergeKontrolZoomForCurrentUpdate");
        hook.ShouldContain("if (_suppressZoomUpdate)");
        hook.ShouldContain("Native third-person zoom hook verified: one look-around gate and two directional analog reads.");
        hook.ShouldContain("MyControllerHelper.IsControlAnalog(context, control, joystick)");
        plugin.ShouldContain("Final control merge rejected:");
        plugin.ShouldContain("Final control argument merge: nativeMove=");
    }

    [Test]
    public void MergeAxis_PreservesTheStrongerNativeOrKontrolInput()
    {
        InputMerge.StrongerAxis(0.75f, 0.2f).ShouldBe(0.75f);
        InputMerge.StrongerAxis(-0.4f, -0.8f).ShouldBe(-0.8f);
        InputMerge.StrongerAxis(0.6f, -0.6f).ShouldBe(0.6f);
        InputMerge.StrongerAxis(float.NaN, 0.3f).ShouldBe(0.3f);
    }

    [Test]
    public void CameraInputMath_UsesTheConfiguredHoldOrToggleStateAndZoomDirection()
    {
        ulong hold = 1UL << SpaceEngineersControlLayout.HoldLookAroundAction;
        ulong toggle = 1UL << SpaceEngineersControlLayout.ToggleLookAroundAction;

        CameraInputMath.IsCameraLookActive(hold).ShouldBeTrue();
        CameraInputMath.IsCameraLookActive(toggle).ShouldBeTrue();
        CameraInputMath.IsCameraLookActive(0).ShouldBeFalse();
        CameraInputMath.ResolveLookAxis(0f, 0.75f).ShouldBe(0.75f);
        CameraInputMath.ResolveLookAxis(-0.5f, 0.75f).ShouldBe(-0.5f);
        CameraInputMath.ApplyLookAxis(1f, 0.1f, 1d / 60d).ShouldBeInRange(0.499f, 0.501f);
        CameraInputMath.ApplyLookAxis(1f, 1f, 1d / 60d).ShouldBeInRange(4.999f, 5.001f);
        CameraInputMath.ApplyLookAxis(1f, 10f, 1d / 60d).ShouldBeInRange(49.99f, 50.01f);
        CameraInputMath.ApplyLookAxis(0f, 10f, 1d / 60d).ShouldBe(0f);
        CameraInputMath.ResolveShipAxis(0.2f, 0.9f, true).ShouldBe(0.2f);
        CameraInputMath.ResolveShipAxis(0.2f, 0.9f, false).ShouldBe(0.9f);
        CameraInputMath.ResolveZoomControlValue(0.25f, 2f, true).ShouldBe(0.25f);
        CameraInputMath.ResolveZoomControlValue(0.25f, 4f, true).ShouldBe(0.5f);
        CameraInputMath.ResolveZoomControlValue(0.75f, 10f, true).ShouldBe(1f);
        CameraInputMath.ResolveZoomControlValue(0.25f, 2f, false).ShouldBe(0f);
        CameraInputMath.ResolveZoomControlValue(-0.25f, 2f, false).ShouldBe(0.25f);
        CameraInputMath.ResolveZoomControlValue(-0.25f, 4f, false).ShouldBe(0.5f);
        CameraInputMath.ResolveZoomControlValue(-0.75f, 10f, false).ShouldBe(1f);
        CameraInputMath.ResolveZoomControlValue(float.NaN, 2f, true).ShouldBe(0f);
        CameraInputMath.ResolveZoomControlValue(float.PositiveInfinity, 2f, true).ShouldBe(0f);
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
            "flight.pitchSensitivity", "flight.yawSensitivity", "flight.rollSensitivity", "camera.lookSensitivity"]);
        provider.Descriptors.ShouldAllBe(descriptor => descriptor.UpdateScope == Kontrol.Sdk.Settings.SettingUpdateScope.Realtime);
        provider.GetDefaultSnapshot().GetNumber("flight.pitchSensitivity", 0f).ShouldBe(20f);
        provider.GetDefaultSnapshot().GetNumber("flight.yawSensitivity", 0f).ShouldBe(20f);
        provider.GetDefaultSnapshot().GetNumber("flight.rollSensitivity", 0f).ShouldBe(1f);
        provider.GetDefaultSnapshot().GetNumber("camera.lookSensitivity", 0f).ShouldBe(2f);
        var camera = provider.Descriptors.Single(descriptor => descriptor.Key == "camera.lookSensitivity");
        camera.ShouldBeOfType<Kontrol.Sdk.Settings.NumberSettingDescriptor>().Min.ShouldBe(0.1f);
        camera.ShouldBeOfType<Kontrol.Sdk.Settings.NumberSettingDescriptor>().Max.ShouldBe(10f);
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
        settings.ShouldContain("camera.lookSensitivity");
        settings.ShouldContain("return Math.Max(0.1f, Math.Min(40f, value));");
        plugin.ShouldContain("_settings.Refresh();");
        plugin.ShouldContain("frame.ReadAnalog(0) * sensitivities.Pitch");
        plugin.ShouldContain("frame.ReadAnalog(2) * sensitivities.Yaw");
        plugin.ShouldContain("frame.ReadAnalog(1) * sensitivities.Roll");
        plugin.ShouldContain("sensitivities.CameraLook");
        plugin.ShouldContain("CameraInputMath.ApplyLookAxis(");
        plugin.ShouldContain("CameraInputMath.ResolveZoomControlValue(");
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
        schema.Inputs[SpaceEngineersControlLayout.ReactorsAction].Id.ShouldBe("systems.reactors");
        schema.Inputs[SpaceEngineersControlLayout.ShowTerminalAction].Id.ShouldBe("interface.terminal");
        schema.Inputs[SpaceEngineersControlLayout.ShowInventoryAction].Id.ShouldBe("interface.inventory");
        schema.Inputs[SpaceEngineersControlLayout.PrimaryAction].Id.ShouldBe("weapons.primary");
        schema.Inputs[SpaceEngineersControlLayout.SecondaryAction].Id.ShouldBe("weapons.secondary");
        schema.Inputs[SpaceEngineersControlLayout.BroadcastingAction].Id.ShouldBe("systems.broadcasting");
        schema.Inputs[SpaceEngineersControlLayout.LocalPowerAction].Id.ShouldBe("systems.local_power");
        schema.Inputs[SpaceEngineersControlLayout.ToggleHudAction].Id.ShouldBe("interface.hud");
        schema.Inputs[SpaceEngineersControlLayout.ChatScreenAction].Id.ShouldBe("communication.chat");
        schema.Inputs[SpaceEngineersControlLayout.VoiceChatAction].Id.ShouldBe("communication.voice");
        schema.Inputs[SpaceEngineersControlLayout.HoldLookAroundAction].Id.ShouldBe("camera.hold_look_around");
        schema.Inputs[SpaceEngineersControlLayout.ToggleLookAroundAction].Id.ShouldBe("camera.toggle_look_around");
        schema.Inputs.Where(input => input.SignalKind == Kontrol.Sdk.Inputs.InputSignalKind.Analog)
            .ElementAt(SpaceEngineersControlLayout.CameraLookHorizontalAnalog).Id.ShouldBe("camera.look_horizontal");
        schema.Inputs.Where(input => input.SignalKind == Kontrol.Sdk.Inputs.InputSignalKind.Analog)
            .ElementAt(SpaceEngineersControlLayout.CameraLookVerticalAnalog).Id.ShouldBe("camera.look_vertical");
        schema.Inputs.Where(input => input.SignalKind == Kontrol.Sdk.Inputs.InputSignalKind.Analog)
            .ElementAt(SpaceEngineersControlLayout.CameraZoomAnalog).Id.ShouldBe("camera.zoom");
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
        "Kontrol.Sdk.Pulsar.dll"
    ];

    private static readonly string[] PulsarRuntimeTargetDependencies =
    [
        "Kontrol.Sdk.dll"
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
