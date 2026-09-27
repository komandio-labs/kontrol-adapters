using System.Diagnostics;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Inputs;
using Kontrol.Sdk.Interfaces;

[assembly: KontrolAdapter(
    "vtol-vr",
    "VTOL VR",
    "vtol-vr",
    "VTOLVR.exe",
    "",
    "667970",
    requiresHarmony: false,
    requiresCore: false,
    supportedMethods: [GameLaunchMethod.BinPluginsFolder],
    defaultDeploymentMethod: GameLaunchMethod.BinPluginsFolder)]

namespace Kontrol.Adapters.VtolVR;

public sealed class VtolAdapterInstaller : IAdapterInstaller
{
    private const string AdapterFolder = "Kontrol.VtolAdapter";
    private const string PayloadName = "Kontrol.Adapters.VtolVR.Mod.dll";
    private const string SdkPayloadName = "Kontrol.Sdk.Vtol.dll";
    private const string ItemManifestName = "item.json";
    private const string ModLoaderSteamAppId = "3018410";
    private const string ModLoaderInstallDirectoryName = "VTOL VR Mod Loader";

    public AdapterInputSchema GetInputSchema() => new(1,
    [
        new("flight.pitch", "Pitch", "Nose up / nose down", "Flight controls", 10, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .08f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Nose up", "Nose down")),
        new("flight.roll", "Roll", "Bank left / bank right", "Flight controls", 20, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .08f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Bank left", "Bank right")),
        new("flight.yaw", "Yaw", "Turn left / turn right", "Flight controls", 30, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .08f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Turn left", "Turn right")),
        new("flight.throttle", "Throttle", "Decrease / increase throttle", "Flight controls", 40, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .02f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Idle", "Full")),
        new("flight.tilt", "Tilt", "Decrease / increase tilt on tilt aircraft", "Flight controls", 50, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .05f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Decrease tilt", "Increase tilt")),
        new("flight.brakes", "Brakes", "Release / apply virtual brakes", "Flight controls", 60, InputSignalKind.Analog,
            AllowInvert: true, DefaultDeadzone: .03f, AllowedSourceKinds: [InputSourceKind.Axis, InputSourceKind.ButtonPair],
            DirectionLabels: new("Release", "Apply"))
    ]);

    public AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureMethod(context.Method);
        string modDirectory = GetModDirectory(context.GameDirectory);
        string? loaderManagedDirectory = FindModLoaderManagedDirectory(context.GameDirectory);
        bool loaderAvailable = loaderManagedDirectory is not null;
        bool payloadAvailable = File.Exists(GetPayloadPath(context.SourceDllPath)) &&
            File.Exists(GetSdkPayloadPath(context.SourceDllPath)) &&
            File.Exists(Path.Combine(Path.GetDirectoryName(context.SourceDllPath) ?? string.Empty, ItemManifestName));
        bool deployed = File.Exists(Path.Combine(modDirectory, PayloadName)) &&
            File.Exists(Path.Combine(modDirectory, "Kontrol.Sdk.dll")) &&
            File.Exists(Path.Combine(modDirectory, ItemManifestName));
        var state = !loaderAvailable ? DeploymentState.NotConfigured : !payloadAvailable
            ? DeploymentState.Failed : deployed ? DeploymentState.Deployed : DeploymentState.NotDeployed;
        string message = !loaderAvailable ? "VTOL VR Mod Loader was not found in the selected game folder."
            : !payloadAvailable ? "The VTOL Mod Loader payload is missing beside the adapter entry assembly."
            : deployed ? "The Kontrol VTOL mod files are present in the Mod Loader Mods folder."
            : "The VTOL Mod Loader payload is ready to install.";

        return new AdapterDeploymentPlan(
            context.Method,
            new DeploymentMethodCapabilities(CanInstall: true, CanUninstall: true, CanLaunch: true, CanCreateShortcut: false),
            "VTOL VR Mod Loader",
            "Kontrol installs the VTOL VR mod payload into the selected game's Mod Loader Mods directory and opens VTOL VR Mod Loader through Steam.",
            "Only the Kontrol mod DLL, its SDK dependency, and item.json are copied or removed. The Mod Loader starts VTOL VR and loads the Kontrol mod on start.",
            "Confirm writing the Kontrol mod files under the selected VTOL VR Mod Loader Mods folder.",
            [new DeploymentPrerequisite("vtol-mod-loader", "VTOL VR Mod Loader",
                "The Mod Loader managed API must be installed with VTOL VR.",
                loaderAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                loaderAvailable ? Directory.GetParent(loaderManagedDirectory!)?.FullName : null,
                "Install VTOL VR Mod Loader from Steam in the same Steam library as VTOL VR, then retry deployment."),
             new DeploymentPrerequisite("vtol-mod-payload", "Kontrol VTOL mod payload",
                "The separately compiled net472 VtolMod assembly, portable SDK, and item.json must be packaged.",
                payloadAvailable ? DeploymentPrerequisiteState.Satisfied : DeploymentPrerequisiteState.Missing,
                payloadAvailable ? GetPayloadPath(context.SourceDllPath) : null,
                "Build the VTOL mod project with local loader/game references and package its output beside the entry assembly.")],
            [new DeploymentTarget("vtol-mods", "VTOL VR Mod Loader Mods", DeploymentTargetKind.ExternalLoader,
                modDirectory,
                [new DeploymentOwnedFile(PayloadName, "Kontrol's VTOL Mod Loader plugin."),
                 new DeploymentOwnedFile("Kontrol.Sdk.dll", "The .NET Standard SDK runtime used by the game-side mod."),
                 new DeploymentOwnedFile(ItemManifestName, "Mod Loader metadata for the Kontrol adapter.")],
                [new DeploymentConfigurationEffect("mod-loader-autoload", "The item.json configures Kontrol to load on start in Mod Loader.")])],
            new DeploymentLaunchChain([new DeploymentLaunchStep("VTOL VR Mod Loader", DeploymentLaunchStepKind.ExternalLauncher,
                "Kontrol opens VTOL VR Mod Loader through Steam. Select Play in Mod Loader to start VTOL VR with Kontrol loaded.")]),
            [new DeploymentManualStep("In VTOL VR Mod Loader, select Play to start VTOL VR; the Kontrol mod is configured to load on start.")],
            new DeploymentVerification(state, message, DeploymentRuntimeState.Unknown,
                "Runtime readiness is confirmed when the mod reports its loaded status to Kontrol."),
            new DeploymentRollbackPlan("Remove the three Kontrol-owned files from the Mod Loader Mods folder.",
                [new DeploymentRollbackEffect("vtol-mods", $"Delete {PayloadName}, Kontrol.Sdk.dll, and {ItemManifestName}.")]));
    }

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        EnsureMethod(method);
        if (FindModLoaderManagedDirectory(gameDirectory) is null)
            throw new DirectoryNotFoundException("VTOL VR Mod Loader was not found beside the selected VTOL VR Steam installation.");
        string payload = GetPayloadPath(sourceDllPath);
        string sdkPayload = GetSdkPayloadPath(sourceDllPath);
        string item = Path.Combine(Path.GetDirectoryName(sourceDllPath) ?? string.Empty, ItemManifestName);
        if (!File.Exists(payload)) throw new FileNotFoundException("VTOL Mod Loader payload is missing.", payload);
        if (!File.Exists(sdkPayload)) throw new FileNotFoundException("VTOL game-side SDK dependency is missing.", sdkPayload);
        if (!File.Exists(item)) throw new FileNotFoundException("VTOL Mod Loader item manifest is missing.", item);
        string modDirectory = GetModDirectory(gameDirectory);
        Directory.CreateDirectory(modDirectory);
        File.Copy(payload, Path.Combine(modDirectory, PayloadName), overwrite: true);
        File.Copy(sdkPayload, Path.Combine(modDirectory, "Kontrol.Sdk.dll"), overwrite: true);
        File.Copy(item, Path.Combine(modDirectory, ItemManifestName), overwrite: true);
    }

    public void Uninstall(string gameDirectory, GameLaunchMethod method)
    {
        EnsureMethod(method);
        string modDirectory = GetModDirectory(gameDirectory);
        DeleteOwnedFile(Path.Combine(modDirectory, PayloadName));
        DeleteOwnedFile(Path.Combine(modDirectory, "Kontrol.Sdk.dll"));
        DeleteOwnedFile(Path.Combine(modDirectory, ItemManifestName));
    }

    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method)
    {
        EnsureMethod(method);
        string directory = GetModDirectory(gameDirectory);
        return File.Exists(Path.Combine(directory, PayloadName)) &&
            File.Exists(Path.Combine(directory, "Kontrol.Sdk.dll")) &&
            File.Exists(Path.Combine(directory, ItemManifestName));
    }

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath)
    {
        EnsureMethod(method);
        if (!CheckIsInstalled(gameDirectory, method))
            throw new InvalidOperationException("Deploy the Kontrol adapter to the VTOL VR Mod Loader before launching.");
        if (FindModLoaderManagedDirectory(gameDirectory) is null)
            throw new DirectoryNotFoundException("VTOL VR Mod Loader was not found beside the selected VTOL VR Steam installation.");

        var startInfo = new ProcessStartInfo($"steam://run/{ModLoaderSteamAppId}/")
        {
            UseShellExecute = true
        };
        if (Process.Start(startInfo) is null)
            throw new InvalidOperationException("Windows did not accept the VTOL VR Mod Loader Steam launch request.");
    }

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotSupportedException("Start VTOL VR from its Mod Loader application.");

    private static string GetModDirectory(string gameDirectory) =>
        Path.Combine(gameDirectory, "@Mod Loader", "Mods", AdapterFolder);

    private static string? FindModLoaderManagedDirectory(string gameDirectory)
    {
        string integratedManagedDirectory = Path.Combine(gameDirectory, "@Mod Loader", "Managed");
        if (File.Exists(Path.Combine(integratedManagedDirectory, "ModLoader.Framework.dll")))
            return integratedManagedDirectory;

        string? steamCommonDirectory = Directory.GetParent(Path.GetFullPath(gameDirectory))?.FullName;
        if (steamCommonDirectory is null) return null;

        string steamAppManagedDirectory = Path.Combine(steamCommonDirectory, ModLoaderInstallDirectoryName, "Managed");
        return File.Exists(Path.Combine(steamAppManagedDirectory, "ModLoader.Framework.dll"))
            ? steamAppManagedDirectory
            : null;
    }

    private static string GetPayloadPath(string sourceDllPath) =>
        Path.Combine(Path.GetDirectoryName(sourceDllPath) ?? string.Empty, PayloadName);

    private static string GetSdkPayloadPath(string sourceDllPath) =>
        Path.Combine(Path.GetDirectoryName(sourceDllPath) ?? string.Empty, SdkPayloadName);

    private static void EnsureMethod(GameLaunchMethod method)
    {
        if (method != GameLaunchMethod.BinPluginsFolder)
            throw new NotSupportedException($"VTOL VR supports only {GameLaunchMethod.BinPluginsFolder} deployment through Mod Loader.");
    }

    private static void DeleteOwnedFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
