using System.Runtime.InteropServices;
using Kontrol.Sdk.Inputs;
using Kontrol.Sdk.IPC;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Sdk.Tests;

[TestFixture]
public sealed class InputSchemaControlSetTests
{
    [Test]
    public void InputsWithoutApplicableSetsAreGlobalAndCategoriesRemainFunctionalGroups()
    {
        var input = new InputDescriptor("gear", "Landing gear", "", "Flight controls", 1, InputSignalKind.Discrete);
        var schema = new AdapterInputSchema(3, [input]);

        schema.ControlSets.ShouldBeEmpty();
        input.ApplicableControlSetIds.ShouldBeEmpty();
        input.Category.ShouldBe("Flight controls");
    }

    [Test]
    public void InputCanApplyToOneControlSet()
    {
        var schema = new AdapterInputSchema(3, [
            new InputDescriptor("rotor.tilt", "Tilt", "", "Flight controls", 1, InputSignalKind.Analog)
            {
                ApplicableControlSetIds = ["helicopter"]
            }
        ])
        {
            ControlSets = [new ControlSetDescriptor("helicopter", "Helicopter", 1)]
        };

        schema.ControlSets.ShouldBe([new ControlSetDescriptor("helicopter", "Helicopter", 1)]);
        schema.Inputs[0].ApplicableControlSetIds.ShouldBe(["helicopter"]);
    }

    [Test]
    public void InputCanApplyToMultipleControlSets()
    {
        var schema = new AdapterInputSchema(3, [
            new InputDescriptor("rotor.collective", "Collective", "", "Flight controls", 1, InputSignalKind.Analog)
            {
                ApplicableControlSetIds = ["helicopter", "tiltrotor"]
            }
        ])
        {
            ControlSets = [
                new ControlSetDescriptor("helicopter", "Helicopter", 1),
                new ControlSetDescriptor("tiltrotor", "Tiltrotor", 2)
            ]
        };

        schema.Inputs[0].ApplicableControlSetIds.ShouldBe(["helicopter", "tiltrotor"]);
        schema.Inputs[0].Category.ShouldBe("Flight controls");
    }

    [Test]
    public void ControlSetMetadataPreservesExistingConstructorsAndInputFrameLayout()
    {
        typeof(InputDescriptor).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(InputSignalKind),
            typeof(DiscreteBehavior?), typeof(bool), typeof(float), typeof(float)
        ]).ShouldNotBeNull();
        typeof(InputDescriptor).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(InputSignalKind),
            typeof(DiscreteBehavior?), typeof(bool), typeof(float), typeof(float), typeof(IReadOnlyList<InputSourceKind>),
            typeof(DiscreteBehavior?), typeof(DiscreteDeliveryMode?), typeof(DirectionLabels), typeof(AxisThresholdDefaults)
        ]).ShouldNotBeNull();
        typeof(AdapterInputSchema).GetConstructor([typeof(int), typeof(IReadOnlyList<InputDescriptor>)])
            .ShouldNotBeNull();

        Marshal.SizeOf<InputFrame>().ShouldBe(149);
        typeof(InputFrame).GetField(nameof(InputFrame.SchemaVersion)).ShouldNotBeNull();
        typeof(InputFrame).GetField(nameof(InputFrame.SchemaVersion))!.FieldType.ShouldBe(typeof(uint));
    }
}
