using System.Diagnostics;
using Microsoft.Win32;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Inputs;

namespace Kontrol.Adapters.SpaceEngineers2;

public class SpaceEngineers2Installer : IAdapterInstaller
{
    public ProcessInjectionEntryPoint GetProcessInjectionEntryPoint() => new(
        "Kontrol.Adapters.SpaceEngineers2.SpaceEngineers2StartupHook");

    public AdapterInputSchema GetInputSchema() => new(9,
    [
        new("flight.pitch", "Pitch", "Nose up / nose down", "Flight controls", 10, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .10f, DefaultExponent: 1f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Nose up", "Nose down")),
        new("flight.roll", "Roll", "Bank left / right", "Flight controls", 20, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .10f, DefaultExponent: 1f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Bank left", "Bank right")),
        new("flight.yaw", "Yaw", "Turn left / right", "Flight controls", 30, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, DefaultExponent: 1f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Turn left", "Turn right")),
        new("movement.forward", "Forward thrust", "Forward / reverse translation", "Translation", 10, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, DefaultExponent: 1.5f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Reverse", "Forward")),
        new("movement.strafe", "Strafe", "Left / right translation", "Translation", 20, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, DefaultExponent: 1.5f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Left", "Right")),
        new("movement.lift", "Lift", "Up / down translation", "Translation", 30, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .05f, DefaultExponent: 1f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Down", "Up")),
        new("systems.dampeners", "Dampeners", "Toggle inertial dampeners", "Vehicle systems", 10, InputSignalKind.Discrete, DiscreteBehavior.Toggle, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Toggle, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("systems.lights", "Lights", "Cycle vehicle lights", "Vehicle systems", 20, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("systems.parking_brakes", "Parking brakes", "Toggle parking brakes", "Vehicle systems", 30, InputSignalKind.Discrete, DiscreteBehavior.Trigger),
        new("systems.power", "Power", "Toggle vehicle power", "Vehicle systems", 40, InputSignalKind.Discrete, DiscreteBehavior.Trigger),
        new("systems.exit_grid", "Exit grid", "Leave the controlled cockpit or seat", "Vehicle systems", 50, InputSignalKind.Discrete, DiscreteBehavior.Trigger),
        new("weapons.fire_primary", "Primary fire", "Fire the currently selected weapon", "Weapons", 10, InputSignalKind.Discrete, DiscreteBehavior.Momentary),
        new("weapons.reload", "Reload", "Reload the currently selected weapon (secondary/right-mouse action)", "Weapons", 20, InputSignalKind.Discrete, DiscreteBehavior.Momentary),
        new("camera.mode_switch", "Camera Mode Switch", "Switch between the available SE2 camera modes", "Camera", 10, InputSignalKind.Discrete, DiscreteBehavior.Trigger),
        new("flight.cruise_control_set", "Cruise Control Set", "Set the current forward speed as the cruise target. Double-click to reset Cruise Control.", "Flight controls", 40, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button]),
        new("flight.cruise_control_increase", "Cruise Control increase", "Increase Cruise Control by 1 displayed speed unit; hold to repeat at 1, then 10 displayed-unit steps. The 10-unit step rounds up to the next multiple of 10.", "Flight controls", 50, InputSignalKind.Discrete, DiscreteBehavior.Momentary, AllowedSourceKinds: [InputSourceKind.Button], ActionBehavior: DiscreteBehavior.Momentary, DeliveryMode: DiscreteDeliveryMode.State),
        new("flight.cruise_control_decrease", "Cruise Control decrease", "Decrease Cruise Control by 1 displayed speed unit; hold to repeat at 1, then 10 displayed-unit steps. The 10-unit step rounds down to the prior multiple of 10 without going below 0.", "Flight controls", 60, InputSignalKind.Discrete, DiscreteBehavior.Momentary, AllowedSourceKinds: [InputSourceKind.Button], ActionBehavior: DiscreteBehavior.Momentary, DeliveryMode: DiscreteDeliveryMode.State),
        new("belt.select_1", "Toolbar slot 1", "Select toolbar item in slot 1 while piloting a cockpit.", "Toolbar", 10, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_2", "Toolbar slot 2", "Select toolbar item in slot 2 while piloting a cockpit.", "Toolbar", 20, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_3", "Toolbar slot 3", "Select toolbar item in slot 3 while piloting a cockpit.", "Toolbar", 30, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_4", "Toolbar slot 4", "Select toolbar item in slot 4 while piloting a cockpit.", "Toolbar", 40, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_5", "Toolbar slot 5", "Select toolbar item in slot 5 while piloting a cockpit.", "Toolbar", 50, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_6", "Toolbar slot 6", "Select toolbar item in slot 6 while piloting a cockpit.", "Toolbar", 60, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_7", "Toolbar slot 7", "Select toolbar item in slot 7 while piloting a cockpit.", "Toolbar", 70, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_8", "Toolbar slot 8", "Select toolbar item in slot 8 while piloting a cockpit.", "Toolbar", 80, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_9", "Toolbar slot 9", "Select toolbar item in slot 9 while piloting a cockpit.", "Toolbar", 90, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new()),
        new("belt.select_0", "Toolbar slot 10", "Select toolbar item in slot 10 using key 0 while piloting a cockpit.", "Toolbar", 100, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button, InputSourceKind.Axis], ActionBehavior: DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event, AxisThresholdDefaults: new())
    ]);
    private const string HarmonyDllName = "0Harmony.dll";
    private const string SdkDllName = "Kontrol.Sdk.dll";
    private const string SteamAppIdFileName = "steam_appid.txt";
    private const string SteamAppIdBackupFileName = "steam_appid.txt.kontrol-backup";
    private const string SteamAppId = "1133870";
    private const string ExeName = "SpaceEngineers2.exe";
    private const string RuntimeConfigName = "SpaceEngineers2.runtimeconfig.json";
    private const string RelativeBinPath = "Game2";
    private const string PluginDllName = "Kontrol.Adapters.SpaceEngineers2.dll";
    private const string PulsarDirectoryEnvironmentVariable = "KONTROL_PULSAR_DIRECTORY";
    private const string PulsarDirectoryName = "Pulsar";
    private const string PulsarModernExecutableName = "Modern.exe";
    private const string PulsarModernDirectoryName = "Modern";
    private const string PulsarPluginFolderName = "Kontrol.Adapters.SpaceEngineers2.Pulsar";
    private const string PulsarPluginDllName = "Kontrol.Adapters.SpaceEngineers2.Pulsar.dll";
    private const string PulsarPluginMetadataName = "Kontrol.Adapters.SpaceEngineers2.Pulsar.xml";
    private static readonly string[] PulsarPayloadFiles = [PulsarPluginDllName, PulsarPluginMetadataName, PluginDllName, HarmonyDllName, SdkDllName];

    public AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Method switch
        {
            GameLaunchMethod.ProcessInjection => BuildProcessInjectionPlan(context),
            GameLaunchMethod.NativePluginParameter => BuildNativePluginPlan(context),
            GameLaunchMethod.BinPluginsFolder => BuildPulsarModernPlan(context),
            _ => throw new NotSupportedException($"SE2 does not support the {context.Method} deployment method.")
        };
    }

    private AdapterDeploymentPlan BuildPulsarModernPlan(AdapterDeploymentContext context)
    {
        GetPulsarModernPaths(out string modernExecutable, out string localPluginDirectory);
        string sourceDirectory = Path.GetDirectoryName(context.SourceDllPath) ?? string.Empty;
        string sourcePayloadPath = Path.Combine(sourceDirectory, PulsarPluginDllName);
        string sourceMetadataPath = Path.Combine(sourceDirectory, PulsarPluginMetadataName);
        string sourceAdapterPath = Path.Combine(sourceDirectory, PluginDllName);
        string sourceHarmonyPath = ResolveHarmonySourcePath(sourceDirectory);
        string sourceSdkPath = Path.Combine(sourceDirectory, SdkDllName);
        string gameExecutable = Path.Combine(GetGameExeDir(context.GameDirectory), ExeName);
        string destinationDirectory = Path.Combine(localPluginDirectory, PulsarPluginFolderName);
        bool pulsarAvailable = File.Exists(modernExecutable);
        bool gameAvailable = File.Exists(gameExecutable);
        bool payloadAvailable = File.Exists(sourcePayloadPath);
        bool metadataAvailable = File.Exists(sourceMetadataPath);
        bool adapterAvailable = File.Exists(sourceAdapterPath);
        bool harmonyAvailable = File.Exists(sourceHarmonyPath);
        bool sdkAvailable = File.Exists(sourceSdkPath);
        bool writeAvailable = false;
        if (pulsarAvailable)
        {
            try
            {
                Directory.CreateDirectory(localPluginDirectory);
                EnsureDirectoryCanBeWritten(localPluginDirectory);
                writeAvailable = true;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        bool deployed = PulsarPayloadFiles.All(file => File.Exists(Path.Combine(destinationDirectory, file)));
        bool ready = pulsarAvailable && gameAvailable && payloadAvailable && metadataAvailable && adapterAvailable && harmonyAvailable && sdkAvailable && writeAvailable;
        DeploymentState state = !payloadAvailable || !metadataAvailable || !adapterAvailable || !harmonyAvailable || !sdkAvailable || !gameAvailable
            ? DeploymentState.Failed
            : !pulsarAvailable
                ? DeploymentState.NotConfigured
                : deployed
                    ? DeploymentState.Deployed
                    : DeploymentState.NotDeployed;

        return new AdapterDeploymentPlan(
            context.Method,
            new DeploymentMethodCapabilities(CanInstall: true, CanUninstall: true, CanLaunch: true, CanCreateShortcut: false),
            "Pulsar Modern joystick / HOTAS / HOSAS plugin",
            "Kontrol deploys a separate .NET 10 Pulsar Modern entry plugin and its Kontrol runtime sidecars to Pulsar Modern's owned local-plugin folder, then starts Pulsar Modern with Space Engineers 2.",
            $"Only Kontrol-owned files are copied to {destinationDirectory}. No Space Engineers 2 or Steam file is changed.",
            "Confirm the external Pulsar Modern local-plugin write and the subsequent game launch.",
            [
                new DeploymentPrerequisite("pulsar-modern", "Pulsar Modern", "Pulsar Modern must be installed to load the .NET 10 Kontrol plugin.", pulsarAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, modernExecutable, $"Install Pulsar Modern under %APPDATA%\\Pulsar or set {PulsarDirectoryEnvironmentVariable} to its root directory."),
                new DeploymentPrerequisite("space-engineers-2-executable", "Space Engineers 2 executable", "The selected installation must contain SpaceEngineers2.exe under Game2.", gameAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, gameExecutable, "Select a Space Engineers 2 installation containing Game2\\SpaceEngineers2.exe."),
                new DeploymentPrerequisite("pulsar-modern-payload", "Packaged Pulsar Modern payload", "The package must contain the separate .NET 10 Pulsar Modern entry assembly.", payloadAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, sourcePayloadPath, $"Rebuild the adapter package with {PulsarPluginDllName} included."),
                new DeploymentPrerequisite("pulsar-modern-metadata", "Pulsar plugin metadata", "The package must contain the metadata that supplies the friendly name and description in Pulsar Modern.", metadataAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, sourceMetadataPath, $"Rebuild the adapter package with {PulsarPluginMetadataName} included."),
                new DeploymentPrerequisite("adapter-entry-assembly", "Kontrol adapter runtime", "The shared Kontrol adapter runtime must be packaged beside the Pulsar Modern entry plugin.", adapterAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, sourceAdapterPath, $"Rebuild the adapter package with {PluginDllName} included."),
                new DeploymentPrerequisite("harmony-dependency", "Harmony dependency", "The Kontrol adapter runtime requires its packaged Harmony dependency.", harmonyAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, sourceHarmonyPath, $"Rebuild the adapter package with {HarmonyDllName} included."),
                new DeploymentPrerequisite("sdk-assembly", "Kontrol SDK assembly", "The Kontrol adapter runtime requires its packaged SDK assembly.", sdkAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing, sourceSdkPath, $"Rebuild the adapter package with {SdkDllName} included."),
                new DeploymentPrerequisite("pulsar-modern-local-plugin-write-access", "Pulsar Modern local-plugin write access", "Kontrol verifies write access to Pulsar Modern's local-plugin directory immediately before deployment.", writeAvailable ? DeploymentPrerequisiteState.Satisfied : (pulsarAvailable ? DeploymentPrerequisiteState.Failed : DeploymentPrerequisiteState.Missing), localPluginDirectory, "Grant write access to Pulsar Modern's local-plugin directory, then deploy again.")
            ],
            [new DeploymentTarget("pulsar-modern-local", "Pulsar Modern local plugins", DeploymentTargetKind.ExternalLoader, destinationDirectory,
                PulsarPayloadFiles.Select(file => new DeploymentOwnedFile(file, "Kontrol-owned Pulsar Modern deployment file.")).ToArray(), Array.Empty<DeploymentConfigurationEffect>())],
            new DeploymentLaunchChain([new DeploymentLaunchStep("Pulsar Modern", DeploymentLaunchStepKind.ExternalLauncher, "Kontrol starts Pulsar Modern with the selected Space Engineers 2 executable. Custom launch arguments are appended by the installer.", modernExecutable, $"\"{gameExecutable}\"")]),
            [new DeploymentManualStep($"Enable {PulsarPluginDllName} in the active Pulsar Modern profile before launching.")],
            new DeploymentVerification(state, deployed ? $"The Pulsar Modern payload is installed at {destinationDirectory}." : ready ? "The Pulsar Modern deployment is ready but has not been installed." : "The Pulsar Modern deployment prerequisites are not satisfied.", DeploymentRuntimeState.NotRunning, "Runtime readiness is reported after Pulsar Modern loads the enabled Kontrol plugin and the adapter establishes its normal IPC connection."),
            new DeploymentRollbackPlan($"Remove only the Kontrol-owned Pulsar Modern payload from {destinationDirectory}.", [new DeploymentRollbackEffect(destinationDirectory, $"Delete {string.Join(", ", PulsarPayloadFiles)}; leave Pulsar Modern and Space Engineers 2 files unchanged.")]));
    }

    private AdapterDeploymentPlan BuildProcessInjectionPlan(AdapterDeploymentContext context)
    {
        string gameExeDir = GetGameExeDir(context.GameDirectory);
        string gameExecutable = Path.Combine(gameExeDir, ExeName);
        string runtimeConfig = Path.Combine(gameExeDir, RuntimeConfigName);
        bool gameAvailable = File.Exists(gameExecutable);
        bool runtimeAvailable = File.Exists(runtimeConfig);
        bool steamAvailable = TryFindSteamExecutable(out var steamExecutable);
        bool ready = gameAvailable && runtimeAvailable && steamAvailable;

        return new AdapterDeploymentPlan(
            context.Method,
            new DeploymentMethodCapabilities(CanInstall: false, CanUninstall: false, CanLaunch: false, CanCreateShortcut: false),
            "Process injection",
            "Kontrol launches Space Engineers 2 through Steam, validates Steam's actual game process, then attaches the native bootstrap and managed adapter.",
            "No Space Engineers 2 files are copied, modified, or backed up. The bootstrap and adapter remain in Kontrol-managed output.",
            "Confirm that Kontrol may launch Space Engineers 2 through Steam and attach the Kontrol bootstrap to its process.",
            [
                new DeploymentPrerequisite(
                    "space-engineers-2-executable",
                    "Space Engineers 2 executable",
                    "The selected installation must contain the CoreCLR Space Engineers 2 executable under Game2.",
                    gameAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    gameExecutable,
                    "Select a Space Engineers 2 installation containing Game2\\SpaceEngineers2.exe."),
                new DeploymentPrerequisite(
                    "space-engineers-2-runtime",
                    "Space Engineers 2 CoreCLR runtime",
                    "The selected installation must contain the runtime configuration used to attach the CoreCLR bootstrap.",
                    runtimeAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    runtimeConfig,
                    "Select a compatible CoreCLR Space Engineers 2 installation."),
                new DeploymentPrerequisite(
                    "steam",
                    "Steam client",
                    "Steam must be installed so Kontrol can launch Space Engineers 2 with its Steam application ID.",
                    steamAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    steamExecutable,
                    "Install Steam and sign in to the account that owns Space Engineers 2.")
            ],
            [new DeploymentTarget(
                "kontrol-managed-bootstrap",
                "Kontrol-managed process-injection runtime",
                DeploymentTargetKind.KontrolManaged,
                Path.GetDirectoryName(context.SourceDllPath) ?? string.Empty,
                Array.Empty<DeploymentOwnedFile>(),
                Array.Empty<DeploymentConfigurationEffect>())],
            new DeploymentLaunchChain([
                new DeploymentLaunchStep(
                    "Steam",
                    DeploymentLaunchStepKind.Steam,
                    "Kontrol starts Space Engineers 2 through Steam's recommended application launch URL.",
                    steamExecutable,
                    $"steam://run/{SteamAppId}/"),
                new DeploymentLaunchStep(
                    "Kontrol native bootstrap",
                    DeploymentLaunchStepKind.AdapterBootstrap,
                    "The host waits for Steam's actual SpaceEngineers2.exe process and attaches the source-built x64 bootstrap, which loads the managed adapter.")]),
            Array.Empty<DeploymentManualStep>(),
            new DeploymentVerification(
                ready ? DeploymentState.NoDeploymentRequired : DeploymentState.Failed,
                ready ? "No deployment is required; the selected game and Steam installation are ready." : "The selected game or Steam installation is not ready for process injection.",
                DeploymentRuntimeState.NotRunning,
                "Runtime readiness is reported after the host attaches the bootstrap and the adapter reports its connection."),
            new DeploymentRollbackPlan(
                "No deployment files or configuration are changed.",
                Array.Empty<DeploymentRollbackEffect>()));
    }

    private AdapterDeploymentPlan BuildNativePluginPlan(AdapterDeploymentContext context)
    {
        string gameExeDir = GetGameExeDir(context.GameDirectory);
        string gameExecutable = Path.Combine(gameExeDir, ExeName);
        string sourceDirectory = Path.GetDirectoryName(context.SourceDllPath) ?? string.Empty;
        string sourceHarmonyPath = ResolveHarmonySourcePath(sourceDirectory);
        string sourceSdkPath = Path.Combine(sourceDirectory, SdkDllName);
        string destinationPluginPath = Path.Combine(gameExeDir, PluginDllName);
        bool gameAvailable = Directory.Exists(gameExeDir) && File.Exists(gameExecutable);
        bool pluginAvailable = File.Exists(context.SourceDllPath);
        bool harmonyAvailable = File.Exists(sourceHarmonyPath);
        bool sdkAvailable = File.Exists(sourceSdkPath);
        bool steamAvailable = TryFindSteamExecutable(out var steamExecutable);
        bool deployed = gameAvailable && CheckIsInstalled(gameDirectory: gameExeDir, GameLaunchMethod.NativePluginParameter);
        bool deploymentReady = gameAvailable && pluginAvailable && harmonyAvailable && sdkAvailable;
        DeploymentState deploymentState = !deploymentReady
            ? DeploymentState.Failed
            : deployed
                ? DeploymentState.Deployed
                : DeploymentState.NotDeployed;

        return new AdapterDeploymentPlan(
            context.Method,
            DeploymentMethodCapabilities.Standard,
            "SE2 native plugin loader",
            "Kontrol copies the adapter and its required dependencies beside the Space Engineers 2 executable, then launches the game through Steam with its -plugins parameter.",
            $"{PluginDllName}, {HarmonyDllName}, and {SdkDllName} are adapter-owned deployment files in {gameExeDir}. {SteamAppIdFileName} is temporarily written and safely restored or removed during uninstall. No original Space Engineers 2 assembly is changed.",
            "Confirm the adapter-owned Game2 file writes and the Steam launch with the absolute native plugin parameter.",
            [
                new DeploymentPrerequisite(
                    "space-engineers-2-executable",
                    "Space Engineers 2 executable",
                    "The selected installation must contain SpaceEngineers2.exe under Game2.",
                    gameAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    gameExecutable,
                    "Select a Space Engineers 2 installation containing Game2\\SpaceEngineers2.exe."),
                new DeploymentPrerequisite(
                    "adapter-entry-assembly",
                    "Adapter entry assembly",
                    "The .NET 9 adapter entry assembly must be available as the installer source.",
                    pluginAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    context.SourceDllPath,
                    $"Build or package {PluginDllName} before deploying."),
                new DeploymentPrerequisite(
                    "harmony-dependency",
                    "Harmony dependency",
                    "The native plugin deployment requires the packaged Harmony dependency or the adapter output fallback.",
                    harmonyAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    harmonyAvailable ? sourceHarmonyPath : null,
                    $"Include {HarmonyDllName} beside the adapter entry assembly or in the adapter output."),
                new DeploymentPrerequisite(
                    "sdk-assembly",
                    "Kontrol SDK assembly",
                    "The native plugin deployment requires the packaged Kontrol SDK assembly.",
                    sdkAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    sourceSdkPath,
                    $"Include {SdkDllName} beside the adapter entry assembly."),
                new DeploymentPrerequisite(
                    "steam",
                    "Steam client",
                    "Steam must be installed so Kontrol can pass the native plugin parameter through Steam.",
                    steamAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    steamExecutable,
                    "Install Steam and sign in to the account that owns Space Engineers 2.",
                    IsBlocking: false)
            ],
            [new DeploymentTarget(
                "space-engineers-2-game2",
                "Space Engineers 2 Game2 directory",
                DeploymentTargetKind.GameInstallation,
                gameExeDir,
                [
                    new DeploymentOwnedFile(PluginDllName, "The Kontrol native plugin entry assembly copied beside Space Engineers 2."),
                    new DeploymentOwnedFile(HarmonyDllName, "The Harmony dependency copied for the native plugin loader."),
                    new DeploymentOwnedFile(SdkDllName, "The Kontrol SDK assembly copied for the native plugin loader.")
                ],
                [new DeploymentConfigurationEffect(
                    "Game2\\steam_appid.txt",
                    $"Temporarily writes Steam application ID {SteamAppId}; preserves and restores any pre-existing steam_appid.txt during uninstall.")])],
            new DeploymentLaunchChain([
                new DeploymentLaunchStep(
                    "Steam",
                    DeploymentLaunchStepKind.Steam,
                    "Kontrol starts Space Engineers 2 through Steam with the absolute adapter plugin path. Custom launch arguments are appended by the installer.",
                    steamExecutable,
                    $"-applaunch {SteamAppId} {BuildNativePluginArgument(Path.GetFullPath(destinationPluginPath))}")]),
            Array.Empty<DeploymentManualStep>(),
            new DeploymentVerification(
                deploymentState,
                deployed ? $"The native plugin and dependencies are installed in {gameExeDir}." : deploymentReady ? "The native plugin deployment is ready but has not been installed." : "The native plugin deployment prerequisites are not satisfied.",
                DeploymentRuntimeState.NotRunning,
                steamAvailable
                    ? "Runtime readiness is reported after Steam starts Space Engineers 2 and the native plugin bootstrap loads the adapter."
                    : "Deployment can proceed, but Steam must be installed and signed in before the native plugin can launch."),
            new DeploymentRollbackPlan(
                $"Remove only the adapter-owned native deployment artifacts from {gameExeDir}.",
                [
                    new DeploymentRollbackEffect(gameExeDir, $"Delete {PluginDllName}, {HarmonyDllName}, and {SdkDllName}; leave original game files unchanged."),
                    new DeploymentRollbackEffect(Path.Combine(gameExeDir, SteamAppIdFileName), $"Restore a pre-existing {SteamAppIdFileName}; otherwise delete the file written by Kontrol.")
                ]));
    }

    private string GetGameExeDir(string gameDirectory)
    {
        if (string.IsNullOrEmpty(gameDirectory)) return string.Empty;

        if (gameDirectory.EndsWith(RelativeBinPath, StringComparison.OrdinalIgnoreCase) ||
            gameDirectory.EndsWith(Path.DirectorySeparatorChar + RelativeBinPath, StringComparison.OrdinalIgnoreCase) ||
            gameDirectory.EndsWith(Path.AltDirectorySeparatorChar + RelativeBinPath, StringComparison.OrdinalIgnoreCase))
        {
            return gameDirectory;
        }

        string subDir = Path.Combine(gameDirectory, RelativeBinPath);
        if (Directory.Exists(subDir))
        {
            return subDir;
        }

        return gameDirectory;
    }

    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method)
    {
        if (method == GameLaunchMethod.BinPluginsFolder)
        {
            if (!TryGetPulsarModernPaths(out _, out string localPluginDirectory)) return false;
            string destinationDirectory = Path.Combine(localPluginDirectory, PulsarPluginFolderName);
            return PulsarPayloadFiles.All(file => File.Exists(Path.Combine(destinationDirectory, file)));
        }

        string gameExeDir = GetGameExeDir(gameDirectory);
        if (string.IsNullOrEmpty(gameExeDir) || !Directory.Exists(gameExeDir)) return false;
        if (method == GameLaunchMethod.ProcessInjection)
            return File.Exists(Path.Combine(gameExeDir, ExeName)) && File.Exists(Path.Combine(gameExeDir, RuntimeConfigName));
        if (method != GameLaunchMethod.NativePluginParameter)
            throw new NotSupportedException($"SE2 does not support the {method} deployment method.");

        string destPluginPath = Path.Combine(gameExeDir, PluginDllName);
        bool installed = File.Exists(destPluginPath);

        installed = installed && File.Exists(Path.Combine(gameExeDir, HarmonyDllName));
        installed = installed && File.Exists(Path.Combine(gameExeDir, SdkDllName));

        return installed;
    }

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        if (method != GameLaunchMethod.ProcessInjection && method != GameLaunchMethod.NativePluginParameter && method != GameLaunchMethod.BinPluginsFolder)
            throw new NotSupportedException($"SE2 does not support the {method} deployment method.");

        if (method == GameLaunchMethod.BinPluginsFolder)
        {
            InstallPulsarModernPayload(sourceDllPath);
            return;
        }

        string gameExeDir = GetGameExeDir(gameDirectory);
        if (!Directory.Exists(gameExeDir))
        {
            throw new DirectoryNotFoundException($"Could not find execution folder at: {gameExeDir}");
        }
        if (method == GameLaunchMethod.ProcessInjection)
        {
            if (!File.Exists(Path.Combine(gameExeDir, ExeName)) || !File.Exists(Path.Combine(gameExeDir, RuntimeConfigName)))
                throw new FileNotFoundException("The selected folder is not a compatible CoreCLR SE2 installation.");
            return;
        }

        string sourceDir = Path.GetDirectoryName(sourceDllPath) ?? string.Empty;
        string sourceHarmonyPath = Path.Combine(sourceDir, HarmonyDllName);
        string sourceSdkPath = Path.Combine(sourceDir, SdkDllName);

        string destPluginPath = Path.Combine(gameExeDir, PluginDllName);
        string destHarmonyPath = Path.Combine(gameExeDir, HarmonyDllName);
        string destSdkPath = Path.Combine(gameExeDir, SdkDllName);
        string steamAppIdPath = Path.Combine(gameExeDir, SteamAppIdFileName);

        // Copy Plugin DLL
        ClearReadOnlyAttribute(destPluginPath);
        File.Copy(sourceDllPath, destPluginPath, overwrite: true);

        // Copy Harmony Dependency DLL
        ClearReadOnlyAttribute(destHarmonyPath);
        if (File.Exists(sourceHarmonyPath))
        {
            File.Copy(sourceHarmonyPath, destHarmonyPath, overwrite: true);
        }
        else
        {
            string fallbackHarmony = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HarmonyDllName);
            if (File.Exists(fallbackHarmony))
            {
                File.Copy(fallbackHarmony, destHarmonyPath, overwrite: true);
            }
            else
            {
                throw new FileNotFoundException($"Required dependency '{HarmonyDllName}' was not found.");
            }
        }

        if (!File.Exists(sourceSdkPath))
            throw new FileNotFoundException($"Required dependency '{SdkDllName}' was not found.", sourceSdkPath);

        ClearReadOnlyAttribute(destSdkPath);
        File.Copy(sourceSdkPath, destSdkPath, overwrite: true);

        WriteSteamAppIdPreservingExisting(steamAppIdPath);
    }

    public void Uninstall(string gameDirectory, GameLaunchMethod method)
    {
        if (method == GameLaunchMethod.ProcessInjection) return;
        if (method == GameLaunchMethod.BinPluginsFolder)
        {
            UninstallPulsarModernPayload();
            return;
        }
        if (method != GameLaunchMethod.NativePluginParameter)
            throw new NotSupportedException($"SE2 does not support the {method} deployment method.");

        string gameExeDir = GetGameExeDir(gameDirectory);
        if (!Directory.Exists(gameExeDir)) return;

        string pluginPath = Path.Combine(gameExeDir, PluginDllName);
        if (File.Exists(pluginPath))
        {
            try { ClearReadOnlyAttribute(pluginPath); File.Delete(pluginPath); } catch {}
        }

        string harmonyPath = Path.Combine(gameExeDir, HarmonyDllName);
        if (File.Exists(harmonyPath))
        {
            try { ClearReadOnlyAttribute(harmonyPath); File.Delete(harmonyPath); } catch {}
        }

        string sdkPath = Path.Combine(gameExeDir, SdkDllName);
        if (File.Exists(sdkPath))
        {
            try { ClearReadOnlyAttribute(sdkPath); File.Delete(sdkPath); } catch {}
        }

        RestoreOrRemoveSteamAppId(Path.Combine(gameExeDir, SteamAppIdFileName));
    }

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        Launch(gameDirectory, method, sourceDllPath, null);

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath, string? customLaunchArguments)
    {
        string subDir = GetGameExeDir(gameDirectory);
        string gameExePath = Path.Combine(subDir, ExeName);

        if (method == GameLaunchMethod.ProcessInjection)
        {
            throw new NotSupportedException(
                "Process injection is owned by the Kontrol host so the game can be launched through Steam and the adapter can be attached to Steam's actual game process.");
        }

        if (method == GameLaunchMethod.BinPluginsFolder)
        {
            if (!CheckIsInstalled(gameDirectory, method))
                throw new InvalidOperationException("Deploy the Space Engineers 2 adapter to Pulsar Modern before launching.");
            if (!TryGetPulsarModernPaths(out string modernExecutable, out _))
                throw new DirectoryNotFoundException(BuildPulsarModernNotFoundMessage());

            var pulsarStartInfo = new ProcessStartInfo
            {
                FileName = modernExecutable,
                WorkingDirectory = Path.GetDirectoryName(modernExecutable),
                UseShellExecute = true,
                Arguments = $"\"{gameExePath}\""
            };
            if (!string.IsNullOrWhiteSpace(customLaunchArguments))
                pulsarStartInfo.Arguments += " " + customLaunchArguments.Trim();
            Process.Start(pulsarStartInfo);
            return;
        }

        if (method != GameLaunchMethod.NativePluginParameter)
            throw new NotSupportedException($"SE2 does not support the {method} launch method.");

        if (!File.Exists(gameExePath))
        {
            throw new FileNotFoundException($"Could not locate game executable at {gameExePath}.");
        }

        string steamExecutable = FindSteamExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = steamExecutable,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("-applaunch");
        startInfo.ArgumentList.Add(SteamAppId);
        startInfo.ArgumentList.Add(BuildNativePluginArgument(Path.GetFullPath(Path.Combine(subDir, PluginDllName))));
        if (!string.IsNullOrWhiteSpace(customLaunchArguments))
        {
            foreach (var arg in customLaunchArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                startInfo.ArgumentList.Add(arg);
            }
        }
        Process.Start(startInfo);
    }

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        CreateShortcut(gameDirectory, method, sourceDllPath, null);

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath, string? customLaunchArguments)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SE2 launch shortcuts are supported on Windows only.");
        if (method != GameLaunchMethod.NativePluginParameter)
            throw new NotSupportedException("Process injection and Pulsar Modern do not provide adapter-created launch shortcuts.");

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string pluginPath = Path.GetFullPath(Path.Combine(GetGameExeDir(gameDirectory), PluginDllName));
        string steamExecutable = FindSteamExecutable();
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new PlatformNotSupportedException("Windows Script Host is required to create a Steam launch shortcut.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Windows Script Host could not be started.");
        dynamic shortcut = shell.CreateShortcut(Path.Combine(desktopPath, "Space Engineers 2 (Kontrol).lnk"));
        shortcut.TargetPath = steamExecutable;
        string arguments = $"-applaunch {SteamAppId} {BuildNativePluginArgument(pluginPath)}";
        if (!string.IsNullOrWhiteSpace(customLaunchArguments))
        {
            arguments += $" {customLaunchArguments.Trim()}";
        }
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(steamExecutable);
        shortcut.Save();
    }

    internal static string BuildNativePluginArgument(string absolutePluginPath) => $"-plugins:{absolutePluginPath}";

    private static string FindSteamExecutable()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SE2 must be launched through Steam on Windows.");

        string? steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string
            ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string;
        if (string.IsNullOrWhiteSpace(steamPath))
            throw new FileNotFoundException("Steam's installation path could not be found in Windows registry.");

        string steamExecutable = Path.Combine(steamPath, "steam.exe");
        if (!File.Exists(steamExecutable))
            throw new FileNotFoundException("Steam executable was not found.", steamExecutable);

        return steamExecutable;
    }

    private static bool TryFindSteamExecutable(out string? steamExecutable)
    {
        try
        {
            steamExecutable = FindSteamExecutable();
            return true;
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            steamExecutable = null;
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            steamExecutable = null;
            return false;
        }
    }

    private static void InstallPulsarModernPayload(string sourceDllPath)
    {
        if (!TryGetPulsarModernPaths(out _, out string localPluginDirectory))
            throw new DirectoryNotFoundException(BuildPulsarModernNotFoundMessage());

        string sourceDirectory = Path.GetDirectoryName(sourceDllPath) ?? string.Empty;
        var sourceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PulsarPluginDllName] = Path.Combine(sourceDirectory, PulsarPluginDllName),
            [PulsarPluginMetadataName] = Path.Combine(sourceDirectory, PulsarPluginMetadataName),
            [PluginDllName] = Path.Combine(sourceDirectory, PluginDllName),
            [HarmonyDllName] = ResolveHarmonySourcePath(sourceDirectory),
            [SdkDllName] = Path.Combine(sourceDirectory, SdkDllName)
        };
        foreach ((string name, string path) in sourceFiles)
            if (!File.Exists(path)) throw new FileNotFoundException($"The packaged Pulsar Modern file '{name}' was not found.", path);

        Directory.CreateDirectory(localPluginDirectory);
        EnsureDirectoryCanBeWritten(localPluginDirectory);
        string destinationDirectory = Path.Combine(localPluginDirectory, PulsarPluginFolderName);
        Directory.CreateDirectory(destinationDirectory);
        EnsureDirectoryCanBeWritten(destinationDirectory);
        foreach ((string name, string source) in sourceFiles)
        {
            string destination = Path.Combine(destinationDirectory, name);
            ClearReadOnlyAttribute(destination);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void UninstallPulsarModernPayload()
    {
        if (!TryGetPulsarModernPaths(out _, out string localPluginDirectory)) return;
        string destinationDirectory = Path.Combine(localPluginDirectory, PulsarPluginFolderName);
        foreach (string file in PulsarPayloadFiles)
        {
            string path = Path.Combine(destinationDirectory, file);
            try
            {
                ClearReadOnlyAttribute(path);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        try
        {
            if (Directory.Exists(destinationDirectory) && !Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
                Directory.Delete(destinationDirectory);
        }
        catch { }
    }

    private static bool TryGetPulsarModernPaths(out string modernExecutable, out string localPluginDirectory)
    {
        foreach (string root in GetPulsarRoots())
        {
            string executable = Path.Combine(root, PulsarModernExecutableName);
            if (!File.Exists(executable)) continue;
            modernExecutable = executable;
            localPluginDirectory = Path.Combine(root, PulsarModernDirectoryName, "Local");
            return true;
        }

        modernExecutable = string.Empty;
        localPluginDirectory = string.Empty;
        return false;
    }

    private static void GetPulsarModernPaths(out string modernExecutable, out string localPluginDirectory)
    {
        if (TryGetPulsarModernPaths(out modernExecutable, out localPluginDirectory)) return;
        string root = GetPulsarRoots().FirstOrDefault() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), PulsarDirectoryName);
        modernExecutable = Path.Combine(root, PulsarModernExecutableName);
        localPluginDirectory = Path.Combine(root, PulsarModernDirectoryName, "Local");
    }

    private static IEnumerable<string> GetPulsarRoots()
    {
        string? configured = Environment.GetEnvironmentVariable(PulsarDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) yield return configured;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), PulsarDirectoryName);
    }

    private static string BuildPulsarModernNotFoundMessage() =>
        $"Pulsar Modern was not found. Install Pulsar under %APPDATA%\\Pulsar or set {PulsarDirectoryEnvironmentVariable} to its root directory.";

    private static void EnsureDirectoryCanBeWritten(string directory)
    {
        string probePath = Path.Combine(directory, $".kontrol-write-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Open(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Pulsar Modern's local-plugin directory is not writable: {directory}", exception);
        }
        finally
        {
            try { if (File.Exists(probePath)) File.Delete(probePath); } catch { }
        }
    }

    private static string ResolveHarmonySourcePath(string sourceDirectory)
    {
        string packagedHarmonyPath = Path.Combine(sourceDirectory, HarmonyDllName);
        if (File.Exists(packagedHarmonyPath)) return packagedHarmonyPath;
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HarmonyDllName);
    }

    private static void WriteSteamAppIdPreservingExisting(string steamAppIdPath)
    {
        string backupPath = Path.Combine(Path.GetDirectoryName(steamAppIdPath) ?? string.Empty, SteamAppIdBackupFileName);
        if (File.Exists(backupPath))
        {
            bool alreadyDeployed = File.Exists(steamAppIdPath) &&
                string.Equals(File.ReadAllText(steamAppIdPath).Trim(), SteamAppId, StringComparison.Ordinal);
            if (!alreadyDeployed)
                throw new IOException($"Cannot safely deploy because the Kontrol backup file already exists: {backupPath}");
        }
        else if (File.Exists(steamAppIdPath))
        {
            File.Copy(steamAppIdPath, backupPath);
        }

        ClearReadOnlyAttribute(steamAppIdPath);
        File.WriteAllText(steamAppIdPath, SteamAppId);
    }

    private static void RestoreOrRemoveSteamAppId(string steamAppIdPath)
    {
        string backupPath = Path.Combine(Path.GetDirectoryName(steamAppIdPath) ?? string.Empty, SteamAppIdBackupFileName);
        try
        {
            ClearReadOnlyAttribute(steamAppIdPath);
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, steamAppIdPath, overwrite: true);
                ClearReadOnlyAttribute(backupPath);
                File.Delete(backupPath);
                return;
            }

            if (File.Exists(steamAppIdPath) &&
                string.Equals(File.ReadAllText(steamAppIdPath).Trim(), SteamAppId, StringComparison.Ordinal))
            {
                File.Delete(steamAppIdPath);
            }
        }
        catch { }
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                }
            }
        }
        catch {}
    }
}
