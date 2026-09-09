using Kontrol.Adapters.DummyAdapter;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk;
using Kontrol.Sdk.Interfaces;
using Kontrol.Sdk.Inputs;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.DummyAdapter.Tests;

[TestFixture]
public class DummyAdapterInstallerTests
{
    private readonly DummyAdapterInstaller _installer = new();

    [Test]
    public void InputSchema_ExposesStableMovementRotationAndActionInputs()
    {
        var schema = _installer.GetInputSchema();

        schema.Version.ShouldBe(1);
        schema.Inputs.Count.ShouldBe(9);
        schema.Inputs.Count(input => input.SignalKind == InputSignalKind.Analog).ShouldBe(6);
        schema.Inputs.Count(input => input.SignalKind == InputSignalKind.Discrete).ShouldBe(3);
        schema.Inputs.Select(input => input.Id).ShouldBe(new[]
        {
            "movement.forward", "movement.strafe", "movement.lift",
            "look.pitch", "look.yaw", "look.roll",
            "action.primary", "action.secondary", "action.utility"
        });
    }

    [Test]
    public void ProcessInjection_IsTheOnlySupportedDeploymentMethod()
    {
        var processInjection = _installer.GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.ProcessInjection,
            "C:\\KontrolSandbox",
            "C:\\Kontrol\\Kontrol.Adapters.DummyAdapter.dll"));

        processInjection.Capabilities.CanLaunch.ShouldBeTrue();
        processInjection.Capabilities.CanInstall.ShouldBeFalse();
        processInjection.LaunchChain.Steps.ShouldContain(step => step.Kind == DeploymentLaunchStepKind.AdapterBootstrap);
        Should.Throw<NotSupportedException>(() => _installer.GetDeploymentPlan(new AdapterDeploymentContext(
            GameLaunchMethod.AssemblyHooking,
            "C:\\KontrolSandbox",
            "C:\\Kontrol\\Kontrol.Adapters.DummyAdapter.dll")));
    }

    [Test]
    public void SdkAndAdapterAssemblies_AreStampedFromTheInitialVersionSources()
    {
        KontrolSdkContract.Version.ShouldBe("1.4.0");
        KontrolSdkContract.Major.ShouldBe(1);
        typeof(KontrolSdkContract).Assembly.GetName().Version!.ToString().ShouldBe("1.4.0.0");
        typeof(DummyAdapterInstaller).Assembly.GetName().Version!.ToString().ShouldBe("1.0.0.0");
    }
}
