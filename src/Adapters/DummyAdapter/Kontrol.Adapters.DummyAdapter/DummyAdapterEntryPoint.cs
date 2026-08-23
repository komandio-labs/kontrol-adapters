using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.IPC;
using Kontrol.Sdk.Inputs;
using Kontrol.Sdk.Diagnostics;
using System.Diagnostics;

[assembly: KontrolAdapter(
    "dummy-adapter",
    "Kontrol Sandbox",
    "dummy-adapter",
    "Kontrol.Sandbox.Game.exe",
    "",
    "0",
    requiresHarmony: false,
    requiresCore: false,
    supportedMethods: [GameLaunchMethod.ProcessInjection],
    defaultDeploymentMethod: GameLaunchMethod.ProcessInjection
)]

namespace Kontrol.Adapters.DummyAdapter;

/// <summary>Managed entry point used by the generic CoreCLR bootstrapper in the sandbox.</summary>
public static class DummyAdapterEntryPoint
{
    private static CancellationTokenSource? _cancellation;
    private static readonly AdapterLogReporter LogReporter = new("DummyAdapter");
    private static readonly AdapterConnectionReporter ConnectionReporter = new("DummyAdapter");
    private static Timer? _heartbeatTimer;

    public static void Initialize()
    {
        if (_cancellation is not null) return;
        LogReporter.Write("Kontrol Sandbox adapter initialized.");
        ConnectionReporter.ReportLoaded();
        _heartbeatTimer = new Timer(_ => ConnectionReporter.Pulse(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _cancellation = new CancellationTokenSource();
        _ = Task.Run(() => PumpControls(_cancellation.Token));
    }

    public static void Shutdown()
    {
        _cancellation?.Cancel();
        _heartbeatTimer?.Dispose();
        ConnectionReporter.Dispose();
        LogReporter.Dispose();
    }

    private static void PumpControls(CancellationToken cancellationToken)
    {
        using var controls = new MmfChannel<InputFrame>("Local\\Kontrol_Input_DummyAdapter");
        using var telemetry = new MmfChannel<TelemetryData>("Local\\Kontrol_Telemetry_DummyAdapter");
        controls.CreateOrOpen();
        telemetry.CreateOrOpen();

        while (!cancellationToken.IsCancellationRequested)
        {
            controls.Read(out var control);
            var stateType = Type.GetType("Kontrol.Sandbox.Game.SandboxControlState, Kontrol.Sandbox.Game");
            stateType?.GetMethod("SetInputFrame")?.Invoke(null, [control]);

            var data = new TelemetryData();
            data.SetJson(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Status"] = control.IsInputEnabled != 0 ? "Kontrol active" : "Kontrol paused",
                ["Adapter"] = "Dummy adapter loaded in-process"
            }));
            telemetry.Write(ref data);
            Thread.Sleep(16);
        }
    }
}

/// <summary>Development-only deployment behavior for the external Silk.NET sandbox target.</summary>
public sealed class DummyAdapterInstaller : IAdapterInstaller
{
    public AdapterInputSchema GetInputSchema() => new(1,
    [
        new("movement.forward", "Forward", "Move forward / reverse", "Movement", 10, InputSignalKind.Analog, AllowInvert: true), new("movement.strafe", "Strafe", "Move left / right", "Movement", 20, InputSignalKind.Analog, AllowInvert: true), new("movement.lift", "Lift", "Move up / down", "Movement", 30, InputSignalKind.Analog, AllowInvert: true),
        new("look.pitch", "Pitch", "Look up / down", "Rotation", 10, InputSignalKind.Analog, AllowInvert: true), new("look.yaw", "Yaw", "Turn left / right", "Rotation", 20, InputSignalKind.Analog, AllowInvert: true), new("look.roll", "Roll", "Bank left / right", "Rotation", 30, InputSignalKind.Analog, AllowInvert: true),
        new("action.primary", "Action 1", "Generic momentary action", "Actions", 10, InputSignalKind.Discrete, DiscreteBehavior.Momentary), new("action.secondary", "Action 2", "Generic toggle action", "Actions", 20, InputSignalKind.Discrete, DiscreteBehavior.Toggle), new("action.utility", "Action 3", "Generic one-shot action", "Actions", 30, InputSignalKind.Discrete, DiscreteBehavior.Trigger)
    ]);
    public DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) => method switch
    {
        GameLaunchMethod.ProcessInjection => DeploymentMethodCapabilities.NoDeploymentRequired,
        _ => DeploymentMethodCapabilities.Unavailable
    };
    public DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) => method switch
    {
        GameLaunchMethod.ProcessInjection => new("CoreCLR process injection", "Kontrol starts the target with a CoreCLR startup hook. The hook runs inside the target process and loads this adapter before the game starts.", "Changes: no target files are copied, changed, or backed up. The bootstrap and adapter are loaded from Kontrol's own output directory."),
        _ => DeploymentMethodInformation.Generic(method)
    };
    public string? GetSuggestedGameDirectory() => FindSandboxDirectory();

    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method) =>
        method == GameLaunchMethod.ProcessInjection && File.Exists(Path.Combine(FindSandboxDirectory() ?? gameDirectory, "Kontrol.Sandbox.Game.exe"));

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        if (!CheckIsInstalled(gameDirectory, method))
            throw new FileNotFoundException("Build Kontrol.Sandbox.Game before launching the sandbox.");
    }

    public void Uninstall(string gameDirectory, GameLaunchMethod method) { }

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        var sandboxDirectory = FindSandboxDirectory() ?? gameDirectory;
        var executable = Path.Combine(sandboxDirectory, "Kontrol.Sandbox.Game.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("Sandbox executable was not found.", executable);
        var bootstrapPath = Path.Combine(Path.GetDirectoryName(sourceDllPath)!, "Kontrol.Bootstrap.CoreClr.x64.dll");
        if (!File.Exists(bootstrapPath)) throw new FileNotFoundException("CoreCLR bootstrapper was not found.", bootstrapPath);
        var startInfo = new ProcessStartInfo(executable) { WorkingDirectory = sandboxDirectory, UseShellExecute = false };
        startInfo.Environment["DOTNET_STARTUP_HOOKS"] = bootstrapPath;
        startInfo.Environment["KONTROL_ADAPTER_PATH"] = sourceDllPath;
        startInfo.Environment["KONTROL_ADAPTER_ENTRY_TYPE"] = "Kontrol.Adapters.DummyAdapter.DummyAdapterEntryPoint";
        startInfo.Environment["KONTROL_ADAPTER_ENTRY_METHOD"] = "Initialize";
        startInfo.Environment["KONTROL_ADAPTER_DEBUG"] = Environment.GetEnvironmentVariable("KONTROL_ADAPTER_DEBUG") ?? "0";
        startInfo.Environment["KONTROL_ADAPTER_LOG_FOLDER"] = Environment.GetEnvironmentVariable("KONTROL_ADAPTER_LOG_FOLDER") ?? "KontrolSandbox";
        Process.Start(startInfo);
    }

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotSupportedException("Shortcuts are not provided for the development sandbox.");

    private static string? FindSandboxDirectory()
    {
        var directory = Path.GetDirectoryName(typeof(DummyAdapterInstaller).Assembly.Location);
        while (!string.IsNullOrEmpty(directory))
        {
            var root = Path.Combine(directory, "kontrol-adapters");
            if (Directory.Exists(root))
            {
                var sandbox = Path.Combine(root, "src", "Kontrol.Sandbox.Game", "bin", "Debug", "net9.0");
                return Directory.Exists(sandbox) ? sandbox : null;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }
}
