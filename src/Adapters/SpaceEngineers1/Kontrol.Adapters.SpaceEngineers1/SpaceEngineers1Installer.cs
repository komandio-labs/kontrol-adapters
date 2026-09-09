using System.Diagnostics;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Inputs;

[assembly: KontrolAdapter("space-engineers-1", "Space Engineers 1", "space-engineers-1", "SpaceEngineers.exe", "Bin64", "244850", false, false,
    supportedMethods: [GameLaunchMethod.BinPluginsFolder], defaultDeploymentMethod: GameLaunchMethod.BinPluginsFolder)]

namespace Kontrol.Adapters.SpaceEngineers1;

public sealed class SpaceEngineers1Installer : IAdapterInstaller
{
    private const string PulsarDirectoryEnvironmentVariable = "KONTROL_PULSAR_DIRECTORY";
    private const string PulsarDirectoryName = "Pulsar";
    private const string LegacyExecutableName = "Legacy.exe";
    private const string PluginPayloadName = "Kontrol.Adapters.SpaceEngineers1.Plugin.dll";
    private const string LocalPluginDirectoryName = "Local";

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

    public DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) => method == GameLaunchMethod.BinPluginsFolder
        ? DeploymentMethodCapabilities.Standard
        : DeploymentMethodCapabilities.Unavailable;
    public DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) => method == GameLaunchMethod.BinPluginsFolder
        ? new("Pulsar Legacy plugin", "Kontrol deploys the packaged .NET Framework payload to Pulsar Legacy's local-plugin folder, then starts Pulsar with Space Engineers 1.", "Only Kontrol.Adapters.SpaceEngineers1.Plugin.dll is copied to Pulsar; no Space Engineers file is changed.")
        : DeploymentMethodInformation.Generic(method);
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
            throw new InvalidOperationException("Deploy the Space Engineers 1 adapter to Pulsar Legacy before launching.");
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
            throw new NotSupportedException($"Space Engineers 1 supports only {GameLaunchMethod.BinPluginsFolder} deployment through Pulsar Legacy.");
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

    private static IEnumerable<string> GetPulsarRoots()
    {
        string? configured = Environment.GetEnvironmentVariable(PulsarDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) yield return configured;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), PulsarDirectoryName);
    }

    private static string ResolveGameExecutable(string gameDirectory)
    {
        string root = gameDirectory.EndsWith("Bin64", StringComparison.OrdinalIgnoreCase)
            ? gameDirectory
            : Path.Combine(gameDirectory, "Bin64");
        string executable = Path.Combine(root, "SpaceEngineers.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("Space Engineers.exe was not found in the selected installation.", executable);
        return executable;
    }

    private static string BuildPulsarNotFoundMessage() =>
        $"Pulsar Legacy was not found. Install Pulsar under %APPDATA%\\Pulsar or set {PulsarDirectoryEnvironmentVariable} to its root directory.";
}
