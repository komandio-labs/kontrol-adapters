using Kontrol.Adapters.SpaceEngineers2;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using NUnit.Framework;
using Shouldly;
using System.Reflection;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class InstallerTests
{
    [Test]
    public void NativePluginParameter_Uninstall_RemovesOnlyKontrolDeploymentArtifacts()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"Kontrol_SE2Installer_{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(testRoot, "Game2");
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(sourceDirectory);

        string sourcePlugin = Path.Combine(sourceDirectory, "Kontrol.Adapters.SpaceEngineers2.dll");
        File.WriteAllText(sourcePlugin, "test adapter");
        File.WriteAllText(Path.Combine(sourceDirectory, "0Harmony.dll"), "test harmony");
        File.WriteAllText(Path.Combine(sourceDirectory, "Kontrol.Sdk.dll"), "test sdk");
        string unrelatedGameFile = Path.Combine(gameDirectory, "VRage.Library.dll");
        File.WriteAllText(unrelatedGameFile, "original game file");

        try
        {
            var installer = new SpaceEngineers2Installer();

            installer.Install(testRoot, GameLaunchMethod.NativePluginParameter, sourcePlugin);
            installer.CheckIsInstalled(testRoot, GameLaunchMethod.NativePluginParameter).ShouldBeTrue();
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt")).ShouldBeTrue();

            installer.Uninstall(testRoot, GameLaunchMethod.NativePluginParameter);

            installer.CheckIsInstalled(testRoot, GameLaunchMethod.NativePluginParameter).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "Kontrol.Adapters.SpaceEngineers2.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "0Harmony.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "Kontrol.Sdk.dll")).ShouldBeFalse();
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt")).ShouldBeFalse();
            File.Exists(unrelatedGameFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Test]
    public void AdapterMetadata_PresentsNativePluginAsDefaultAndRetainsProcessInjection()
    {
        var metadata = typeof(SpaceEngineers2Installer).Assembly.GetCustomAttribute<KontrolAdapterAttribute>();

        metadata.ShouldNotBeNull();
        metadata.DefaultDeploymentMethod.ShouldBe(GameLaunchMethod.NativePluginParameter);
        metadata.SupportedMethods.ShouldBe([GameLaunchMethod.NativePluginParameter, GameLaunchMethod.BinPluginsFolder, GameLaunchMethod.ProcessInjection]);
    }

    [Test]
    public void PulsarModern_InstallsAndRemovesOnlyItsOwnedLocalPayload()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"Kontrol_SE2Pulsar_{Guid.NewGuid():N}");
        string pulsarRoot = Path.Combine(testRoot, "Pulsar");
        string sourceDirectory = Path.Combine(testRoot, "source");
        string gameDirectory = Path.Combine(testRoot, "SpaceEngineers2", "Game2");
        Directory.CreateDirectory(pulsarRoot);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(gameDirectory);
        File.WriteAllText(Path.Combine(pulsarRoot, "Modern.exe"), "test pulsar");
        File.WriteAllText(Path.Combine(gameDirectory, "SpaceEngineers2.exe"), "test game");
        foreach (string file in new[]
                 {
                     "Kontrol.Adapters.SpaceEngineers2.dll",
                     "Kontrol.Adapters.SpaceEngineers2.Pulsar.dll",
                     "Kontrol.Adapters.SpaceEngineers2.Pulsar.xml",
                     "Kontrol.Sdk.dll",
                     "0Harmony.dll"
                 })
            File.WriteAllText(Path.Combine(sourceDirectory, file), "test payload");

        string localDirectory = Path.Combine(pulsarRoot, "Modern", "Local", "Kontrol.Adapters.SpaceEngineers2.Pulsar");
        string unrelatedFile = Path.Combine(localDirectory, "user-file.txt");
        string? previousRoot = Environment.GetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY");
        Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", pulsarRoot);
        try
        {
            var installer = new SpaceEngineers2Installer();
            string sourceDllPath = Path.Combine(sourceDirectory, "Kontrol.Adapters.SpaceEngineers2.dll");

            installer.Install(gameDirectory, GameLaunchMethod.BinPluginsFolder, sourceDllPath);
            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeTrue();
            File.WriteAllText(unrelatedFile, "keep me");

            installer.Uninstall(gameDirectory, GameLaunchMethod.BinPluginsFolder);

            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeFalse();
            File.Exists(unrelatedFile).ShouldBeTrue();
            File.Exists(Path.Combine(gameDirectory, "SpaceEngineers2.exe")).ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", previousRoot);
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Test]
    public void PulsarModern_PlanDeclaresNet10PayloadAndNoGameFileWrites()
    {
        var plan = new SpaceEngineers2Installer().GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.BinPluginsFolder,
            Path.Combine("test-game", "SpaceEngineers2"),
            Path.Combine("package", "Kontrol.Adapters.SpaceEngineers2.dll")));

        plan.Capabilities.ShouldBe(new DeploymentMethodCapabilities(CanInstall: true, CanUninstall: true, CanLaunch: true, CanCreateShortcut: false));
        plan.Targets.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentTargetKind.ExternalLoader);
        plan.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([
            "Kontrol.Adapters.SpaceEngineers2.Pulsar.dll",
            "Kontrol.Adapters.SpaceEngineers2.Pulsar.xml",
            "Kontrol.Adapters.SpaceEngineers2.dll",
            "0Harmony.dll",
            "Kontrol.Sdk.dll"]);
        plan.Targets.Single().ConfigurationEffects.ShouldBeEmpty();
        plan.LaunchChain.Steps.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentLaunchStepKind.ExternalLauncher);
        plan.ManualSteps.Single().Instruction.ShouldContain("Kontrol.Adapters.SpaceEngineers2.Pulsar.dll");
    }

    [Test]
    public void NativePluginParameter_Uninstall_RestoresPreExistingSteamAppIdFile()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"Kontrol_SE2SteamAppId_{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(testRoot, "Game2");
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(sourceDirectory);

        string sourcePlugin = Path.Combine(sourceDirectory, "Kontrol.Adapters.SpaceEngineers2.dll");
        File.WriteAllText(sourcePlugin, "test adapter");
        File.WriteAllText(Path.Combine(sourceDirectory, "0Harmony.dll"), "test harmony");
        File.WriteAllText(Path.Combine(sourceDirectory, "Kontrol.Sdk.dll"), "test sdk");
        string steamAppIdPath = Path.Combine(gameDirectory, "steam_appid.txt");
        File.WriteAllText(steamAppIdPath, "pre-existing app id");

        try
        {
            var installer = new SpaceEngineers2Installer();
            installer.Install(testRoot, GameLaunchMethod.NativePluginParameter, sourcePlugin);
            File.ReadAllText(steamAppIdPath).ShouldBe("1133870");
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt.kontrol-backup")).ShouldBeTrue();

            installer.Uninstall(testRoot, GameLaunchMethod.NativePluginParameter);

            File.ReadAllText(steamAppIdPath).ShouldBe("pre-existing app id");
            File.Exists(Path.Combine(gameDirectory, "steam_appid.txt.kontrol-backup")).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Test]
    public void NativePluginParameter_Uninstall_DoesNotDeleteSteamAppIdChangedAfterDeployment()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"Kontrol_SE2SteamAppIdChanged_{Guid.NewGuid():N}");
        string gameDirectory = Path.Combine(testRoot, "Game2");
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(sourceDirectory);

        string sourcePlugin = Path.Combine(sourceDirectory, "Kontrol.Adapters.SpaceEngineers2.dll");
        File.WriteAllText(sourcePlugin, "test adapter");
        File.WriteAllText(Path.Combine(sourceDirectory, "0Harmony.dll"), "test harmony");
        File.WriteAllText(Path.Combine(sourceDirectory, "Kontrol.Sdk.dll"), "test sdk");
        string steamAppIdPath = Path.Combine(gameDirectory, "steam_appid.txt");

        try
        {
            var installer = new SpaceEngineers2Installer();
            installer.Install(testRoot, GameLaunchMethod.NativePluginParameter, sourcePlugin);
            File.WriteAllText(steamAppIdPath, "user-changed app id");

            installer.Uninstall(testRoot, GameLaunchMethod.NativePluginParameter);

            File.ReadAllText(steamAppIdPath).ShouldBe("user-changed app id");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Test]
    public void ProcessInjection_UsesNoDeploymentAndDescribesTheHostOwnedLaunchChain()
    {
        var plan = new SpaceEngineers2Installer().GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.ProcessInjection,
            Path.Combine("test-game", "SpaceEngineers2"),
            Path.Combine("package", "Kontrol.Adapters.SpaceEngineers2.dll")));

        plan.Capabilities.ShouldBe(new DeploymentMethodCapabilities(
            CanInstall: false,
            CanUninstall: false,
            CanLaunch: false,
            CanCreateShortcut: false));
        plan.Capabilities.CanLaunch.ShouldBeFalse();
        plan.Targets.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentTargetKind.KontrolManaged);
        plan.Targets.Single().OwnedFiles.ShouldBeEmpty();
        plan.Targets.Single().ConfigurationEffects.ShouldBeEmpty();
        plan.LaunchChain.Steps.Select(step => step.Kind).ShouldBe([
            DeploymentLaunchStepKind.Steam,
            DeploymentLaunchStepKind.AdapterBootstrap]);
        plan.ManualSteps.ShouldBeEmpty();
        plan.Rollback.Effects.ShouldBeEmpty();
    }

    [Test]
    public void NativePluginParameter_DescribesGame2FilesAndSteamPluginArgument()
    {
        string gameDirectory = Path.Combine("test-game", "SpaceEngineers2");
        string sourceDllPath = Path.Combine("package", "Kontrol.Adapters.SpaceEngineers2.dll");
        var plan = new SpaceEngineers2Installer().GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.NativePluginParameter,
            gameDirectory,
            sourceDllPath));

        plan.Capabilities.ShouldBe(DeploymentMethodCapabilities.Standard);
        plan.Targets.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentTargetKind.GameInstallation);
        plan.Targets.Single().Location.ShouldBe(gameDirectory);
        plan.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([
            "Kontrol.Adapters.SpaceEngineers2.dll",
            "0Harmony.dll",
            "Kontrol.Sdk.dll"]);
        plan.Targets.Single().ConfigurationEffects.ShouldHaveSingleItem().Scope.ShouldBe("Game2\\steam_appid.txt");
        plan.Prerequisites.Single(prerequisite => prerequisite.Id == "steam").IsBlocking.ShouldBeFalse();
        plan.LaunchChain.Steps.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentLaunchStepKind.Steam);
        plan.LaunchChain.Steps.Single().Arguments!.ShouldContain("-plugins:");
        plan.LaunchChain.Steps.Single().Arguments!.ShouldContain("Kontrol.Adapters.SpaceEngineers2.dll");
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
        Should.Throw<NotSupportedException>(() => new SpaceEngineers2Installer().GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.AssemblyHooking,
            "test-game",
            "package\\Kontrol.Adapters.SpaceEngineers2.dll")));
    }

    [Test]
    public void NativePluginParameter_UsesThePluginLoaderArgumentSyntax()
    {
        string pluginPath = Path.Combine("test-game", "Game2", "Kontrol.Adapters.SpaceEngineers2.dll");

        SpaceEngineers2Installer.BuildNativePluginArgument(pluginPath)
            .ShouldBe($"-plugins:{pluginPath}");
    }
}
