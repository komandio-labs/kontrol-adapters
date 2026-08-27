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

    public AdapterInputSchema GetInputSchema() => new(8,
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
        new("flight.cruise_control_increase", "Cruise Control +10 m/s", "Increase the active Cruise Control target by 10 m/s.", "Flight controls", 50, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button]),
        new("flight.cruise_control_decrease", "Cruise Control -10 m/s", "Decrease the active Cruise Control target by 10 m/s without going below 0 m/s.", "Flight controls", 60, InputSignalKind.Discrete, DiscreteBehavior.Trigger, AllowedSourceKinds: [InputSourceKind.Button])
    ]);
    public DeploymentMethodInformation GetDeploymentInformation(GameLaunchMethod method) => method switch
    {
        GameLaunchMethod.ProcessInjection => new("Process injection", "Kontrol launches Space Engineers 2 through Steam, validates Steam's actual game process, then loads the Kontrol bootstrap and adapter into that process.", "No Space Engineers 2 files are copied, modified, or backed up."),
        GameLaunchMethod.NativePluginParameter => new("SE2 native plugin loader", "Kontrol copies the adapter and its required dependencies beside the Space Engineers 2 executable, then launches the game through Steam with its -plugins parameter.", "The adapter, its dependencies, and steam_appid.txt are added to Game2. No original Space Engineers 2 assembly is changed."),
        _ => DeploymentMethodInformation.Generic(method)
    };
    private const string HarmonyDllName = "0Harmony.dll";
    private const string SdkDllName = "Kontrol.Sdk.dll";
    private const string SteamAppIdFileName = "steam_appid.txt";
    private const string SteamAppId = "1133870";
    private const string ExeName = "SpaceEngineers2.exe";
    private const string RelativeBinPath = "Game2";
    private const string PluginDllName = "Kontrol.Adapters.SpaceEngineers2.dll";

    public DeploymentMethodCapabilities GetCapabilities(GameLaunchMethod method) => method switch
    {
        GameLaunchMethod.ProcessInjection => DeploymentMethodCapabilities.NoDeploymentRequired,
        GameLaunchMethod.NativePluginParameter => DeploymentMethodCapabilities.Standard,
        _ => DeploymentMethodCapabilities.Unavailable
    };

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
        string gameExeDir = GetGameExeDir(gameDirectory);
        if (string.IsNullOrEmpty(gameExeDir) || !Directory.Exists(gameExeDir)) return false;
        if (method == GameLaunchMethod.ProcessInjection)
            return File.Exists(Path.Combine(gameExeDir, ExeName)) && File.Exists(Path.Combine(gameExeDir, "SpaceEngineers2.runtimeconfig.json"));

        string destPluginPath = Path.Combine(gameExeDir, PluginDllName);
        bool installed = File.Exists(destPluginPath);

        installed = installed && File.Exists(Path.Combine(gameExeDir, HarmonyDllName));
        installed = installed && File.Exists(Path.Combine(gameExeDir, SdkDllName));

        return installed;
    }

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        string gameExeDir = GetGameExeDir(gameDirectory);
        if (!Directory.Exists(gameExeDir))
        {
            throw new DirectoryNotFoundException($"Could not find execution folder at: {gameExeDir}");
        }
        if (method == GameLaunchMethod.ProcessInjection)
        {
            if (!File.Exists(Path.Combine(gameExeDir, ExeName)) || !File.Exists(Path.Combine(gameExeDir, "SpaceEngineers2.runtimeconfig.json")))
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

        // Write steam_appid.txt next to the executable.
        File.WriteAllText(steamAppIdPath, SteamAppId);
    }

    public void Uninstall(string gameDirectory, GameLaunchMethod method)
    {
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

        string steamAppIdPath = Path.Combine(gameExeDir, SteamAppIdFileName);
        if (File.Exists(steamAppIdPath))
        {
            try { ClearReadOnlyAttribute(steamAppIdPath); File.Delete(steamAppIdPath); } catch {}
        }
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

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (method == GameLaunchMethod.NativePluginParameter)
        {
            string pluginPath = Path.GetFullPath(Path.Combine(GetGameExeDir(gameDirectory), PluginDllName));
            string steamExecutable = FindSteamExecutable();
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new PlatformNotSupportedException("Windows Script Host is required to create a Steam launch shortcut.");
            dynamic shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Windows Script Host could not be started.");
            dynamic shortcut = shell.CreateShortcut(Path.Combine(desktopPath, "Space Engineers 2 (VRAGE3) Deployed.lnk"));
            shortcut.TargetPath = steamExecutable;
            string arguments = $"-applaunch {SteamAppId} {BuildNativePluginArgument(pluginPath)}";
            if (!string.IsNullOrWhiteSpace(customLaunchArguments))
            {
                arguments += $" {customLaunchArguments.Trim()}";
            }
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(steamExecutable);
            shortcut.Save();
            return;
        }

        string shortcutPath = Path.Combine(desktopPath, "Space Engineers 2 (VRAGE3) Deployed.url");
        File.WriteAllText(shortcutPath, $"[InternetShortcut]\nURL=steam://run/{SteamAppId}/");
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

    private void ClearReadOnlyAttribute(string path)
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
