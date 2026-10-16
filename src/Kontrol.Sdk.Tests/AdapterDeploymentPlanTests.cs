using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Sdk.Tests;

[TestFixture]
public sealed class AdapterDeploymentPlanTests
{
    [Test]
    public void DefaultMember_IsOptionalForInstallerImplementations()
    {
        var method = typeof(IAdapterInstaller).GetMethod(
            nameof(IAdapterInstaller.GetDeploymentPlan),
            [typeof(GameLaunchMethod)]);

        method.ShouldNotBeNull();
        method!.IsAbstract.ShouldBeFalse();
        method.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).ShouldHaveSingleItem();

        var contextualMethod = typeof(IAdapterInstaller).GetMethod(
            nameof(IAdapterInstaller.GetDeploymentPlan),
            [typeof(AdapterDeploymentContext)]);
        contextualMethod.ShouldNotBeNull();
        contextualMethod!.IsAbstract.ShouldBeFalse();
        contextualMethod.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).ShouldBeEmpty();

        typeof(IAdapterInstaller).Assembly
            .GetType("Kontrol.Sdk.Interfaces.DeploymentMethodInformation")!
            .GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)
            .ShouldHaveSingleItem();
    }

    [Test]
    public void AdapterCanDeclareAnExternalLoaderPlan()
    {
        IAdapterInstaller installer = new ExternalLoaderInstaller();
        var plan = installer.GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.BinPluginsFolder,
            "C:\\Games\\Example",
            "C:\\Kontrol\\Example.Adapter.dll"));

        plan.Targets.Single().Location.ShouldBe("C:\\ExampleLoader\\Legacy\\Local");
        plan.Targets.Single().Kind.ShouldBe(DeploymentTargetKind.ExternalLoader);
        plan.Targets.Single().OwnedFiles.Single().Path.ShouldBe("Example.Adapter.Plugin.dll");
        plan.LaunchChain.Steps.Single().Kind.ShouldBe(DeploymentLaunchStepKind.ExternalLauncher);
        plan.Prerequisites.Single().State.ShouldBe(DeploymentPrerequisiteState.Satisfied);
        plan.ManualSteps.Single().Required.ShouldBeTrue();
        plan.Verification.RuntimeState.ShouldBe(DeploymentRuntimeState.Ready);
        plan.Rollback.Effects.Single().Description.ShouldContain("Example.Adapter.Plugin.dll");
    }

    private sealed class ExternalLoaderInstaller : IAdapterInstaller
    {
        public AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context) => new(
            context.Method,
            new DeploymentMethodCapabilities(true, true, true, false),
            "Example Legacy loader",
            "Copies the adapter payload to the external loader.",
            "Only the adapter payload is copied to the loader directory.",
            "Confirm the external loader will be started.",
            [new DeploymentPrerequisite(
                "legacy-loader",
                "Legacy loader",
                "The loader must be installed.",
                DeploymentPrerequisiteState.Satisfied,
                "C:\\ExampleLoader",
                "Install the loader and retry.")],
            [new DeploymentTarget(
                "loader-local",
                "Loader local plugins",
                DeploymentTargetKind.ExternalLoader,
                "C:\\ExampleLoader\\Legacy\\Local",
                [new DeploymentOwnedFile("Example.Adapter.Plugin.dll", "The adapter payload copied to the loader.")],
                [new DeploymentConfigurationEffect("loader-profile", "The adapter plugin is enabled in the selected loader profile.")])],
            new DeploymentLaunchChain([
                new DeploymentLaunchStep(
                    "External loader",
                    DeploymentLaunchStepKind.ExternalLauncher,
                    "Starts the loader with the selected game executable.")]),
            [new DeploymentManualStep("Enable the adapter in the loader profile.")],
            new DeploymentVerification(
                DeploymentState.Deployed,
                "Payload deployed.",
                DeploymentRuntimeState.Ready,
                "Adapter heartbeat is ready."),
            new DeploymentRollbackPlan(
                "Remove the adapter payload.",
                [new DeploymentRollbackEffect("loader-local", "Delete Example.Adapter.Plugin.dll from the loader-local target.")]));

        public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
        public void Uninstall(string gameDirectory, GameLaunchMethod method) { }
        public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method) => false;
        public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
        public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) { }
    }
}
