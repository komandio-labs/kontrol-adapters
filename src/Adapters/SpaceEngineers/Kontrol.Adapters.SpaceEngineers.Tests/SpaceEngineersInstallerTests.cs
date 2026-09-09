using Kontrol.Adapters.SpaceEngineers;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using NUnit.Framework;
using Shouldly;
using System.Text.Json;

namespace Kontrol.Adapters.SpaceEngineers.Tests;

[TestFixture]
public class SpaceEngineersInstallerTests
{
    [Test]
    public void Schema_ExposesApiBackedFlightAndSystemControls()
    {
        var schema = new SpaceEngineersInstaller().GetInputSchema();
        schema.Version.ShouldBe(1);
        schema.Inputs.Select(input => input.Id).ShouldBe(new[]
        {
            "flight.pitch", "flight.roll", "flight.yaw", "movement.forward", "movement.strafe", "movement.lift",
            "systems.dampeners", "systems.lights", "systems.landing_gears", "systems.handbrake"
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
        plan.Title.ShouldBe("Pulsar Legacy plugin");
        plan.Targets.ShouldHaveSingleItem().Kind.ShouldBe(DeploymentTargetKind.ExternalLoader);
        plan.Targets.Single().Location.ShouldEndWith(Path.Combine("Legacy", "Local"));
        plan.Targets.Single().OwnedFiles.ShouldHaveSingleItem().Path.ShouldBe("Kontrol.Adapters.SpaceEngineers.Plugin.dll");
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
            .ShouldBe(new[] { hostEntry, "Kontrol.Sdk.dll", pulsarPayload, "Kontrol.Adapters.SpaceEngineers.Plugin.xml", "adapter.manifest.json", "LICENSE", "THIRD_PARTY_NOTICES.md" });
        deployment.Summary.ShouldContain("separate .NET Framework payload");
        deployment.Targets.Single().OwnedFiles.Select(file => file.Path).ShouldBe([pulsarPayload]);
        deployment.Targets.Single().Location.ShouldEndWith(Path.Combine("Legacy", "Local"));

        string payloadProject = File.ReadAllText(Path.Combine(
            adapterRoot, "Kontrol.Adapters.SpaceEngineers.Plugin", "Kontrol.Adapters.SpaceEngineers.Plugin.csproj"));
        payloadProject.ShouldContain("<TargetFramework>net48</TargetFramework>");
        payloadProject.ShouldContain($"<AssemblyName>{Path.GetFileNameWithoutExtension(pulsarPayload)}</AssemblyName>");
        payloadProject.ShouldNotContain($"<AssemblyName>{Path.GetFileNameWithoutExtension(hostEntry)}</AssemblyName>");
    }

    [Test]
    public void SystemActions_UseTheirStableSchemaPositions()
    {
        var schema = new SpaceEngineersInstaller().GetInputSchema();
        schema.Inputs[SpaceEngineersControlLayout.DampenersAction].Id.ShouldBe("systems.dampeners");
        schema.Inputs[SpaceEngineersControlLayout.LightsAction].Id.ShouldBe("systems.lights");
        schema.Inputs[SpaceEngineersControlLayout.LandingGearsAction].Id.ShouldBe("systems.landing_gears");
        schema.Inputs[SpaceEngineersControlLayout.HandbrakeAction].Id.ShouldBe("systems.handbrake");
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
            Directory.CreateDirectory(pulsarRoot);
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(Path.Combine(gameDirectory, "Bin64"));
            File.WriteAllBytes(Path.Combine(pulsarRoot, "Legacy.exe"), []);
            File.WriteAllBytes(Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.Plugin.dll"), []);
            File.WriteAllBytes(Path.Combine(gameDirectory, "Bin64", "SpaceEngineers.exe"), []);
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", pulsarRoot);

            var plan = new SpaceEngineersInstaller().GetDeploymentPlan(new AdapterDeploymentContext(
                GameLaunchMethod.BinPluginsFolder,
                gameDirectory,
                Path.Combine(packageDirectory, "Kontrol.Adapters.SpaceEngineers.dll")));

            plan.Prerequisites.Single(prerequisite => prerequisite.Id == "pulsar-local-plugin-write-access")
                .State.ShouldBe(DeploymentPrerequisiteState.Satisfied);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KONTROL_PULSAR_DIRECTORY", previousPulsarDirectory);
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

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
