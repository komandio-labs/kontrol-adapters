using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Inputs;

[assembly: KontrolAdapter("space-engineers", "Space Engineers", "space-engineers", "SpaceEngineers.exe", "Bin64", "244850", false, false,
    supportedMethods: [GameLaunchMethod.BinPluginsFolder], defaultDeploymentMethod: GameLaunchMethod.BinPluginsFolder)]

namespace Kontrol.Adapters.SpaceEngineers;

public sealed class SpaceEngineersInstaller : IAdapterInstaller
{
    private const string PulsarDirectoryEnvironmentVariable = "KONTROL_PULSAR_DIRECTORY";
    private const string PulsarDirectoryName = "Pulsar";
    private const string LegacyExecutableName = "Legacy.exe";
    private const string PluginPayloadName = "Kontrol.Adapters.SpaceEngineers.Plugin.dll";
    private const string LocalPluginDirectoryName = "Local";
    private const string StatusMapName = @"Local\Kontrol_AdapterStatus_space-engineers";
    private const int StatusFrameCapacity = 512;
    private const long ActiveHeartbeatMaximumAgeMilliseconds = 5_000;

    public AdapterInputSchema GetInputSchema() => new(1,
    [
        new("flight.pitch", "Pitch", "Nose up / nose down", "Flight controls", 10, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .10f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Nose up", "Nose down")),
        new("flight.roll", "Roll", "Bank left / right", "Flight controls", 20, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .10f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Bank left", "Bank right")),
        new("flight.yaw", "Yaw", "Turn left / right", "Flight controls", 30, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Turn left", "Turn right")),
        new("movement.forward", "Forward thrust", "Forward / reverse translation", "Translation", 10, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, DefaultExponent: 1.5f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Reverse", "Forward")),
        new("movement.strafe", "Strafe", "Left / right translation", "Translation", 20, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .08f, DefaultExponent: 1.5f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Left", "Right")),
        new("movement.lift", "Lift", "Up / down translation", "Translation", 30, InputSignalKind.Analog, AllowInvert: true, DefaultDeadzone: .05f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair], DirectionLabels: new("Down", "Up")),
        new("systems.dampeners", "Dampeners", "Toggle inertial dampeners", "Vehicle systems", 10, InputSignalKind.Discrete, DiscreteBehavior.Toggle, DeliveryMode: DiscreteDeliveryMode.Event),
        new("systems.lights", "Lights", "Toggle vehicle lights", "Vehicle systems", 20, InputSignalKind.Discrete, DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event),
        new("systems.landing_gears", "Landing gear", "Toggle landing gear", "Vehicle systems", 30, InputSignalKind.Discrete, DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event),
        new("systems.handbrake", "Handbrake", "Toggle handbrake", "Vehicle systems", 40, InputSignalKind.Discrete, DiscreteBehavior.Trigger, DeliveryMode: DiscreteDeliveryMode.Event)
    ]);

    public AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsurePulsarMethod(context.Method);

        GetPulsarPlanPaths(out var legacyExecutable, out var localPluginDirectory);
        string sourcePayloadPath = Path.Combine(Path.GetDirectoryName(context.SourceDllPath) ?? string.Empty, PluginPayloadName);
        string gameExecutable = GetGameExecutablePath(context.GameDirectory);
        bool pulsarAvailable = File.Exists(legacyExecutable);
        bool payloadAvailable = File.Exists(sourcePayloadPath);
        bool gameAvailable = File.Exists(gameExecutable);
        bool deployed = pulsarAvailable && File.Exists(Path.Combine(localPluginDirectory, PluginPayloadName));
        var runtimeVerification = GetPulsarRuntimeVerification();

        DeploymentState deploymentState = !payloadAvailable
            ? DeploymentState.Failed
            : !pulsarAvailable
                ? DeploymentState.NotConfigured
                : !gameAvailable
                    ? DeploymentState.Failed
                    : deployed
                        ? DeploymentState.Deployed
                        : DeploymentState.NotDeployed;
        string deploymentMessage = deploymentState switch
        {
            DeploymentState.Deployed => $"The net48 Pulsar payload is installed at {Path.Combine(localPluginDirectory, PluginPayloadName)}.",
            DeploymentState.NotConfigured => "Pulsar Legacy is not configured or installed.",
            DeploymentState.Failed when !payloadAvailable => $"The packaged Pulsar payload was not found at {sourcePayloadPath}.",
            DeploymentState.Failed => $"Space Engineers was not found at {gameExecutable}.",
            _ => "The Pulsar payload is ready to deploy."
        };

        bool writeAccessAvailable = false;
        if (pulsarAvailable)
        {
            try
            {
                Directory.CreateDirectory(localPluginDirectory);
                EnsureDirectoryCanBeWritten(localPluginDirectory);
                writeAccessAvailable = true;
            }
            catch
            {
                writeAccessAvailable = false;
            }
        }

        return new AdapterDeploymentPlan(
            context.Method,
            new DeploymentMethodCapabilities(CanInstall: true, CanUninstall: true, CanLaunch: true, CanCreateShortcut: false),
            "Pulsar Legacy plugin",
            "Kontrol deploys the separate .NET Framework payload to Pulsar Legacy's local-plugin folder, then starts Pulsar Legacy with Space Engineers.",
            $"Only {PluginPayloadName} is copied to Pulsar Legacy at {localPluginDirectory}. No Space Engineers file or Steam launch setting is changed.",
            "Confirm the external Pulsar Legacy local-plugin write and the subsequent game launch.",
            [
                new DeploymentPrerequisite(
                    "pulsar-legacy",
                    "Pulsar Legacy",
                    "Pulsar Legacy must be installed so Kontrol can deploy and launch the adapter payload.",
                    pulsarAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    legacyExecutable,
                    $"Install Pulsar Legacy under %APPDATA%\\Pulsar or set {PulsarDirectoryEnvironmentVariable} to its root directory."),
                new DeploymentPrerequisite(
                    "space-engineers-executable",
                    "Space Engineers executable",
                    "The selected installation must contain SpaceEngineers.exe under Bin64.",
                    gameAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    gameExecutable,
                    "Select a Space Engineers installation containing Bin64\\SpaceEngineers.exe."),
                new DeploymentPrerequisite(
                    "pulsar-payload",
                    "Packaged Pulsar payload",
                    "The package must contain the separate net48 Pulsar Legacy payload beside the .NET 9 Kontrol entry assembly.",
                    payloadAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                    sourcePayloadPath,
                    $"Rebuild the adapter package with {PluginPayloadName} included."),
                new DeploymentPrerequisite(
                    "pulsar-local-plugin-write-access",
                    "Pulsar local-plugin write access",
                    "Kontrol verifies write access to Pulsar Legacy's local-plugin directory immediately before deployment.",
                    writeAccessAvailable ? DeploymentPrerequisiteState.Satisfied : (pulsarAvailable ? DeploymentPrerequisiteState.Failed : DeploymentPrerequisiteState.Missing),
                    localPluginDirectory,
                    "Grant write access to the Pulsar Legacy local-plugin directory, then deploy again.")
            ],
            [new DeploymentTarget(
                "pulsar-legacy-local",
                "Pulsar Legacy local plugins",
                DeploymentTargetKind.ExternalLoader,
                localPluginDirectory,
                [new DeploymentOwnedFile(
                    PluginPayloadName,
                    "The net48 Pulsar Legacy payload copied by the SE1 installer.")],
                Array.Empty<DeploymentConfigurationEffect>())],
            new DeploymentLaunchChain([
                new DeploymentLaunchStep(
                    "Pulsar Legacy",
                    DeploymentLaunchStepKind.ExternalLauncher,
                    "Kontrol starts Pulsar Legacy with the selected Space Engineers executable. Custom launch arguments are appended by the installer.",
                    legacyExecutable,
                    $"\"{gameExecutable}\"")]),
            [new DeploymentManualStep("Enable Kontrol.Adapters.SpaceEngineers.Plugin.dll in the active Pulsar Legacy profile before launching.")],
            new DeploymentVerification(
                deploymentState,
                deploymentMessage,
                runtimeVerification.State,
                runtimeVerification.Message),
            new DeploymentRollbackPlan(
                $"Remove only the SE1 payload owned by Kontrol from {localPluginDirectory}.",
                [new DeploymentRollbackEffect(
                    localPluginDirectory,
                    $"Delete {PluginPayloadName}; leave Pulsar Legacy and all Space Engineers files unchanged.")
                ]));
    }

    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method) =>
        method == GameLaunchMethod.BinPluginsFolder &&
        TryGetPulsarPaths(out _, out var localPluginDirectory) &&
        File.Exists(Path.Combine(localPluginDirectory, PluginPayloadName));

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        EnsurePulsarMethod(method);
        if (!TryGetPulsarPaths(out _, out var localPluginDirectory))
            throw new DirectoryNotFoundException(BuildPulsarNotFoundMessage());

        string sourcePluginPath = Path.Combine(Path.GetDirectoryName(sourceDllPath) ?? string.Empty, PluginPayloadName);
        if (!File.Exists(sourcePluginPath))
            throw new FileNotFoundException($"The packaged Pulsar payload '{PluginPayloadName}' was not found beside the Kontrol adapter entry assembly.", sourcePluginPath);

        Directory.CreateDirectory(localPluginDirectory);
        EnsureDirectoryCanBeWritten(localPluginDirectory);
        File.Copy(sourcePluginPath, Path.Combine(localPluginDirectory, PluginPayloadName), overwrite: true);
    }

    public void Uninstall(string gameDirectory, GameLaunchMethod method)
    {
        EnsurePulsarMethod(method);
        if (!TryGetPulsarPaths(out _, out var localPluginDirectory)) return;
        string payloadPath = Path.Combine(localPluginDirectory, PluginPayloadName);
        if (File.Exists(payloadPath)) File.Delete(payloadPath);
    }

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath) => Launch(gameDirectory, method, sourceDllPath, null);

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath, string? customLaunchArguments)
    {
        EnsurePulsarMethod(method);
        if (!CheckIsInstalled(gameDirectory, method))
            throw new InvalidOperationException("Deploy the Space Engineers adapter to Pulsar Legacy before launching.");
        if (!TryGetPulsarPaths(out var legacyExecutable, out _))
            throw new DirectoryNotFoundException(BuildPulsarNotFoundMessage());

        string gameExecutable = ResolveGameExecutable(gameDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = legacyExecutable,
            WorkingDirectory = Path.GetDirectoryName(legacyExecutable),
            UseShellExecute = true
        };
        startInfo.Arguments = $"\"{gameExecutable}\"";
        if (!string.IsNullOrWhiteSpace(customLaunchArguments))
            startInfo.Arguments += " " + customLaunchArguments.Trim();
        Process.Start(startInfo);
    }

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotSupportedException("Use Kontrol's normal Launch action for Pulsar Legacy.");

    private static void EnsurePulsarMethod(GameLaunchMethod method)
    {
        if (method != GameLaunchMethod.BinPluginsFolder)
            throw new NotSupportedException($"Space Engineers supports only {GameLaunchMethod.BinPluginsFolder} deployment through Pulsar Legacy.");
    }

    private static bool TryGetPulsarPaths(out string legacyExecutable, out string localPluginDirectory)
    {
        foreach (string root in GetPulsarRoots())
        {
            string executable = Path.Combine(root, LegacyExecutableName);
            if (!File.Exists(executable)) continue;
            legacyExecutable = executable;
            localPluginDirectory = Path.Combine(root, "Legacy", LocalPluginDirectoryName);
            return true;
        }
        legacyExecutable = string.Empty;
        localPluginDirectory = string.Empty;
        return false;
    }

    private static void GetPulsarPlanPaths(out string legacyExecutable, out string localPluginDirectory)
    {
        if (TryGetPulsarPaths(out legacyExecutable, out localPluginDirectory)) return;

        string root = GetPulsarRoots().FirstOrDefault() ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PulsarDirectoryName);
        legacyExecutable = Path.Combine(root, LegacyExecutableName);
        localPluginDirectory = Path.Combine(root, "Legacy", LocalPluginDirectoryName);
    }

    private static IEnumerable<string> GetPulsarRoots()
    {
        string? configured = Environment.GetEnvironmentVariable(PulsarDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) yield return configured;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), PulsarDirectoryName);
    }

    private static string ResolveGameExecutable(string gameDirectory)
    {
        string executable = GetGameExecutablePath(gameDirectory);
        if (!File.Exists(executable))
            throw new FileNotFoundException("Space Engineers.exe was not found in the selected installation.", executable);
        return executable;
    }

    private static string GetGameExecutablePath(string gameDirectory)
    {
        string root = gameDirectory.EndsWith("Bin64", StringComparison.OrdinalIgnoreCase)
            ? gameDirectory
            : Path.Combine(gameDirectory, "Bin64");
        return Path.Combine(root, "SpaceEngineers.exe");
    }

    private static string BuildPulsarNotFoundMessage() =>
        $"Pulsar Legacy was not found. Install Pulsar under %APPDATA%\\Pulsar or set {PulsarDirectoryEnvironmentVariable} to its root directory.";

    private static void EnsureDirectoryCanBeWritten(string directory)
    {
        string probePath = Path.Combine(directory, $".kontrol-write-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Open(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Pulsar Legacy's local-plugin directory is not writable: {directory}", exception);
        }
        finally
        {
            try { if (File.Exists(probePath)) File.Delete(probePath); } catch { }
        }
    }

    private static (DeploymentRuntimeState State, string Message) GetPulsarRuntimeVerification()
    {
        if (!OperatingSystem.IsWindows())
            return (DeploymentRuntimeState.NotRunning, "Pulsar Legacy runtime verification is available on Windows after the game starts.");

        try
        {
            using var map = MemoryMappedFile.OpenExisting(StatusMapName, MemoryMappedFileRights.Read);
            using var view = map.CreateViewAccessor(0, StatusFrameCapacity, MemoryMappedFileAccess.Read);
            var buffer = new byte[StatusFrameCapacity];
            view.ReadArray(0, buffer, 0, buffer.Length);
            int length = Array.IndexOf(buffer, (byte)0);
            string json = Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            string state = root.GetProperty("state").GetString() ?? string.Empty;
            long timestamp = root.GetProperty("timestampUnixMilliseconds").GetInt64();
            long age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;

            if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase))
                return (DeploymentRuntimeState.Failed, root.TryGetProperty("errorMessage", out var error)
                    ? error.GetString() ?? "Pulsar reported an adapter error."
                    : "Pulsar reported an adapter error.");
            if (age > ActiveHeartbeatMaximumAgeMilliseconds)
                return (DeploymentRuntimeState.NotRunning, "The Pulsar adapter heartbeat is stale; launch Space Engineers through Pulsar Legacy.");
            if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
                return (DeploymentRuntimeState.Ready, "Pulsar Legacy loaded the Kontrol plugin and its heartbeat is active.");
            if (string.Equals(state, "Loaded", StringComparison.OrdinalIgnoreCase))
                return (DeploymentRuntimeState.Starting, "Pulsar Legacy loaded the Kontrol plugin and is waiting for its first active heartbeat.");
        }
        catch (Exception exception) when (exception is FileNotFoundException or JsonException or IOException or UnauthorizedAccessException)
        {
            // No current Pulsar runtime status is expected before the game starts.
        }

        return (DeploymentRuntimeState.NotRunning, "Runtime readiness is reported after Pulsar Legacy starts and loads the enabled plugin.");
    }
}
