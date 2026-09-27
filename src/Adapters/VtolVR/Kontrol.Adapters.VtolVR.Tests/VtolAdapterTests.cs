using System.Text.Json;
using Kontrol.Adapters.VtolVR;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.VtolVR.Tests;

[TestFixture]
public sealed class VtolAdapterTests
{
    [Test]
    public void Schema_ExposesSixStableFlightAxes()
    {
        var schema = new VtolAdapterInstaller().GetInputSchema();
        schema.Version.ShouldBe(1);
        schema.Inputs.Select(input => input.Id).ShouldBe(new[]
        {
            "flight.pitch", "flight.roll", "flight.yaw", "flight.throttle", "flight.tilt", "flight.brakes"
        });
        schema.Inputs.ShouldAllBe(input => input.SignalKind == Kontrol.Sdk.Inputs.InputSignalKind.Analog);
    }

    [Test]
    public void Deployment_UsesModLoaderFolderAndLaunchesExternalLoader()
    {
        var installer = new VtolAdapterInstaller();
        var plan = installer.GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.BinPluginsFolder, Path.Combine("test-game", "VTOL VR"), Path.Combine("package", "Kontrol.Adapters.VtolVR.dll")));
        plan.Targets.Single().Kind.ShouldBe(DeploymentTargetKind.ExternalLoader);
        plan.Targets.Single().Location.ShouldEndWith(Path.Combine("@Mod Loader", "Mods", "Kontrol.VtolAdapter"));
        plan.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe(new[]
        {
            "Kontrol.Adapters.VtolVR.Mod.dll", "Kontrol.Sdk.dll", "item.json"
        });
        plan.Capabilities.CanLaunch.ShouldBeTrue();
        plan.LaunchChain.Steps.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentLaunchStepKind.ExternalLauncher);
        plan.ManualSteps.Single().Instruction.ShouldContain("select Play");
        plan.Prerequisites.Single(item => item.Id == "vtol-mod-loader").State.ShouldBe(DeploymentPrerequisiteState.Missing);
    }

    [Test]
    public void Deployment_RecognizesSteamInstalledLoaderAndDeploysToGameModsFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), $"Kontrol_VtolDeployment_{Guid.NewGuid():N}");
        string commonDirectory = Path.Combine(root, "steamapps", "common");
        string gameDirectory = Path.Combine(commonDirectory, "VTOL VR");
        string loaderManagedDirectory = Path.Combine(commonDirectory, "VTOL VR Mod Loader", "Managed");
        string packageDirectory = Path.Combine(root, "package");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(loaderManagedDirectory);
        Directory.CreateDirectory(packageDirectory);

        string sourceDllPath = Path.Combine(packageDirectory, "Kontrol.Adapters.VtolVR.dll");
        File.WriteAllText(sourceDllPath, "adapter entry");
        File.WriteAllText(Path.Combine(packageDirectory, "Kontrol.Adapters.VtolVR.Mod.dll"), "game mod");
        File.WriteAllText(Path.Combine(packageDirectory, "Kontrol.Sdk.Vtol.dll"), "game sdk");
        File.WriteAllText(Path.Combine(packageDirectory, "item.json"), "{}");
        File.WriteAllText(Path.Combine(loaderManagedDirectory, "ModLoader.Framework.dll"), "loader framework");

        try
        {
            var installer = new VtolAdapterInstaller();
            var context = new AdapterDeploymentContext(GameLaunchMethod.BinPluginsFolder, gameDirectory, sourceDllPath);
            var plan = installer.GetDeploymentPlan(context);

            plan.Prerequisites.Single(item => item.Id == "vtol-mod-loader").State.ShouldBe(DeploymentPrerequisiteState.Satisfied);
            plan.Prerequisites.Single(item => item.Id == "vtol-mod-loader").ResolvedValue.ShouldBe(
                Path.Combine(commonDirectory, "VTOL VR Mod Loader"));
            plan.Verification.DeploymentState.ShouldBe(DeploymentState.NotDeployed);

            installer.Install(gameDirectory, GameLaunchMethod.BinPluginsFolder, sourceDllPath);

            string modDirectory = Path.Combine(gameDirectory, "@Mod Loader", "Mods", "Kontrol.VtolAdapter");
            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeTrue();
            Directory.GetFiles(modDirectory).Length.ShouldBe(3);
            File.Exists(Path.Combine(modDirectory, "Kontrol.Adapters.VtolVR.Mod.dll")).ShouldBeTrue();
            File.Exists(Path.Combine(modDirectory, "Kontrol.Sdk.dll")).ShouldBeTrue();
            File.Exists(Path.Combine(modDirectory, "item.json")).ShouldBeTrue();
            installer.GetDeploymentPlan(context).Verification.DeploymentState.ShouldBe(DeploymentState.Deployed);

            installer.Uninstall(gameDirectory, GameLaunchMethod.BinPluginsFolder);

            installer.CheckIsInstalled(gameDirectory, GameLaunchMethod.BinPluginsFolder).ShouldBeFalse();
            File.Exists(Path.Combine(loaderManagedDirectory, "ModLoader.Framework.dll")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Launch_RequiresKontrolPayloadToBeDeployed()
    {
        var installer = new VtolAdapterInstaller();
        Should.Throw<InvalidOperationException>(() => installer.Launch(
            Path.Combine("missing-game", "VTOL VR"),
            GameLaunchMethod.BinPluginsFolder,
            Path.Combine("package", "Kontrol.Adapters.VtolVR.dll")));
    }

    [Test]
    public void PackageManifest_RecordsOnlyVerifiedGameBuildIdentity()
    {
        string manifestPath = Path.Combine(FindAdapterRoot(), "package.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        manifest.RootElement.GetProperty("adapterId").GetString().ShouldBe("vtol-vr");
        var identity = manifest.RootElement.GetProperty("gameBuildIdentity");
        identity.GetProperty("platformAppId").GetString().ShouldBe("667970");
        identity.GetProperty("platformBuildId").GetString().ShouldBe("25514272");
        identity.GetProperty("productVersion").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    private static string FindAdapterRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kontrol.Adapters.slnx")))
            directory = directory.Parent;
        directory.ShouldNotBeNull();
        return Path.Combine(directory!.FullName, "src", "Adapters", "VtolVR");
    }
}
