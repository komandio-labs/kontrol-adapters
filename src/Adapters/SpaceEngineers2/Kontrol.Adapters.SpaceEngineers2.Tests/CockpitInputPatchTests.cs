using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using NSubstitute;
using Shouldly;
using Kontrol.Sdk.IPC;
using Kontrol.Adapters.SpaceEngineers2.Patches;
using Kontrol.Sdk.Inputs;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.BlockTools;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.Game2.Simulation.WorldObjects.Movement;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class CockpitInputPatchTests
{
    private static int _commitCount;
    private MmfChannel<InputFrame>? _testInputChannel;
    private MmfChannel<TelemetryData>? _testTelemetryChannel;

    [SetUp]
    public void SetUp()
    {
        // Initialize MMF channels to mimic the WPF app side
        _testInputChannel = new MmfChannel<InputFrame>("Local\\Kontrol_Input_space-engineers-2");
        _testInputChannel.CreateOrOpen();

        _testTelemetryChannel = new MmfChannel<TelemetryData>("Local\\Kontrol_Telemetry_space-engineers-2");
        _testTelemetryChannel.CreateOrOpen();

        // Redirect internal _updateControlDataMethod to avoid calling actual game logic
        var field = typeof(CockpitInputPatch).GetField("_updateControlDataMethod", BindingFlags.NonPublic | BindingFlags.Static);
        var dummyMethod = typeof(CockpitInputPatchTests).GetMethod(nameof(DummyUpdateControlData), BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, dummyMethod);
        _commitCount = 0;
    }

    [TearDown]
    public void TearDown()
    {
        _testInputChannel?.Dispose();
        _testTelemetryChannel?.Dispose();

        // Reset the patch fields
        var field = typeof(CockpitInputPatch).GetField("_updateControlDataMethod", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, null);

        var channelsInitializedField = typeof(CockpitInputPatch).GetField("_channelsInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        channelsInitializedField?.SetValue(null, false);
        var actionsField = typeof(CockpitInputPatch).GetField("_previousTriggeredActions", BindingFlags.NonPublic | BindingFlags.Static);
        actionsField?.SetValue(null, 0UL);
    }

    private static void DummyUpdateControlData()
    {
        _commitCount++;
    }

    [TestCase("ToggleDampeners")]
    [TestCase("ToggleLights")]
    [TestCase("ToggleParkingBrakes")]
    [TestCase("TogglePower")]
    [TestCase("InteractionActivated")]
    public void CockpitActions_UseSe2ButtonHandlerSignature(string methodName)
    {
        var method = typeof(CockpitInputHandlerComponent).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        method.ShouldNotBeNull();
        method.GetParameters()[0].ParameterType.ShouldBe(typeof(bool));
        method.GetParameters()[1].ParameterType.FullName.ShouldBe("Keen.VRage.Input.ControlActivation");
    }

    [Test]
    public void ButtonActionInvocation_SendsPressedStartEvent()
    {
        var targetMethod = typeof(CockpitInputHandlerComponent).GetMethod("ToggleDampeners", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var argumentsMethod = typeof(CockpitInputPatch).GetMethod("CreateButtonActionArguments", BindingFlags.NonPublic | BindingFlags.Static)!;

        var arguments = (object[])argumentsMethod.Invoke(null, [targetMethod, true])!;

        arguments[0].ShouldBe(true);
        Convert.ToInt32(arguments[1]).ShouldBe(0);
    }

    [Test]
    public void InputSchema_AppendsCameraModeSwitchWithoutMovingExistingIndices()
    {
        var schema = new SpaceEngineers2Installer().GetInputSchema();

        schema.Version.ShouldBe(5);
        schema.Inputs[10].Id.ShouldBe("systems.exit_grid");
        schema.Inputs[10].DiscreteBehavior.ShouldBe(DiscreteBehavior.Trigger);
        schema.Inputs[11].Id.ShouldBe("weapons.fire_primary");
        schema.Inputs[11].DiscreteBehavior.ShouldBe(DiscreteBehavior.Momentary);
        schema.Inputs[12].Id.ShouldBe("weapons.reload");
        schema.Inputs[12].DiscreteBehavior.ShouldBe(DiscreteBehavior.Momentary);
        schema.Inputs[13].Id.ShouldBe("camera.mode_switch");
        schema.Inputs[13].DiscreteBehavior.ShouldBe(DiscreteBehavior.Trigger);
    }

    [Test]
    public void CameraModeSwitch_UsesNativeSe2CameraMethods()
    {
        var initialize = typeof(CameraSystemComponent).GetMethod("Init", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var toggle = typeof(CameraSystemComponent).GetMethod("ToggleCameraView", BindingFlags.NonPublic | BindingFlags.Instance);

        initialize.ShouldNotBeNull();
        toggle.ShouldNotBeNull();
        toggle.GetParameters().ShouldBeEmpty();
    }

    [Test]
    public void CameraModeSwitch_IsProcessedFromTheCockpitActionEdge()
    {
        var patchType = typeof(CockpitInputPatch).Assembly.GetType(
            "Kontrol.Adapters.SpaceEngineers2.Patches.CameraActionPatch", throwOnError: true)!;
        var process = patchType.GetMethod("ProcessCameraModeSwitch", BindingFlags.NonPublic | BindingFlags.Static)!;

        process.GetParameters().Length.ShouldBe(1);
        process.GetParameters()[0].ParameterType.ShouldBe(typeof(ulong));
    }

    [TestCase("PrimaryAction")]
    [TestCase("SecondaryAction")]
    public void BlockWeaponActions_UseSe2HeldButtonHandlerSignature(string methodName)
    {
        var method = typeof(BlockToolInputHandlerBaseComponent).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        method.ShouldNotBeNull();
        method.GetParameters()[0].ParameterType.ShouldBe(typeof(bool));
        method.GetParameters()[1].ParameterType.FullName.ShouldBe("Keen.VRage.Input.ControlActivation");
    }

    private static unsafe InputFrame CreateInputFrame(bool enabled, float surge = 0f, float sway = 0f, float heave = 0f, float pitch = 0f, float yaw = 0f, float roll = 0f)
    {
        var frame = new InputFrame { SchemaVersion = 1, IsInputEnabled = enabled ? (byte)1 : (byte)0 };
        frame.AnalogValues[0] = pitch;
        frame.AnalogValues[1] = roll;
        frame.AnalogValues[2] = yaw;
        frame.AnalogValues[3] = surge;
        frame.AnalogValues[4] = sway;
        frame.AnalogValues[5] = heave;
        return frame;
    }

    [Test]
    public void ProcessOverride_WhenOverrideIsInactive_ShouldNotModifyAnyInputs()
    {
        // Arrange
        var control = CreateInputFrame(false, 1f, 1f, 1f, 1f, 1f, 1f);
        _testInputChannel!.Write(ref control);

        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        
        float pitchAnalog = 0.1f;
        float yawAnalog = 0.2f;
        float lookUp = 0.3f;
        float lookDown = 0.4f;
        float lookLeft = 0.5f;
        float lookRight = 0.6f;
        
        var movementInputs = new MovementInputs
        {
            Forward = 0.7f,
            Backward = 0.0f,
            Right = 0.8f,
            Left = 0.0f,
            Up = 0.9f,
            Down = 0.0f,
            RollRight = 0.5f,
            RollLeft = 0.0f
        };

        // Act
        CockpitInputPatch.ProcessOverride(
            instance,
            ref pitchAnalog,
            ref yawAnalog,
            ref lookUp,
            ref lookDown,
            ref lookLeft,
            ref lookRight,
            ref movementInputs,
            observedBlock: null!
        );

        // Assert
        pitchAnalog.ShouldBe(0.1f);
        yawAnalog.ShouldBe(0.2f);
        lookUp.ShouldBe(0.3f);
        lookDown.ShouldBe(0.4f);
        lookLeft.ShouldBe(0.5f);
        lookRight.ShouldBe(0.6f);
        movementInputs.Forward.ShouldBe(0.7f);
        movementInputs.Right.ShouldBe(0.8f);
        movementInputs.Up.ShouldBe(0.9f);
        movementInputs.RollRight.ShouldBe(0.5f);
    }

    [Test]
    public void ProcessOverride_WhenOverrideIsActive_ShouldMergeWithNativeMouseAndKeyboardInput()
    {
        // Arrange
        var control = CreateInputFrame(true, surge: .5f, sway: -.3f, heave: .4f, pitch: .3f, yaw: -.2f, roll: .8f);
        _testInputChannel!.Write(ref control);

        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        
        float pitchAnalog = 0.1f;
        float yawAnalog = 0.2f;
        float lookUp = 0.5f;
        float lookDown = 0.0f;
        float lookLeft = 0.0f;
        float lookRight = 0.0f;
        
        var movementInputs = new MovementInputs
        {
            Forward = 0.3f, // Native forward
            Backward = 0.0f,
            Right = 0.0f,
            Left = 0.1f,    // Native left (net native sway is -0.1)
            Up = 0.2f,      // Native up
            Down = 0.0f,
            RollRight = 0.0f,
            RollLeft = 0.1f // Native roll left (net native roll is -0.1)
        };

        // Act
        CockpitInputPatch.ProcessOverride(
            instance,
            ref pitchAnalog,
            ref yawAnalog,
            ref lookUp,
            ref lookDown,
            ref lookLeft,
            ref lookRight,
            ref movementInputs,
            observedBlock: null!
        );

        // Assert
        // Native mouse analog fields are preserved. Kontrol pitch/yaw use the
        // proportional key-equivalent directional fields instead.
        pitchAnalog.ShouldBe(0.1f, 0.001f);
        yawAnalog.ShouldBe(0.2f, 0.001f);
        lookUp.ShouldBe(0.5f);
        lookDown.ShouldBe(0.3f, 0.001f);
        lookLeft.ShouldBe(0.2f, 0.001f);
        lookRight.ShouldBe(0f);

        movementInputs.Forward.ShouldBe(0.5f, 0.001f);
        movementInputs.Backward.ShouldBe(0.0f);

        movementInputs.Left.ShouldBe(0.3f, 0.001f);
        movementInputs.Right.ShouldBe(0.0f);

        movementInputs.Up.ShouldBe(0.4f, 0.001f);
        movementInputs.Down.ShouldBe(0.0f);

        movementInputs.RollRight.ShouldBe(0.8f, 0.001f);
        movementInputs.RollLeft.ShouldBe(0.1f);
        _commitCount.ShouldBe(1);
    }

    [Test]
    public void ProcessOverride_WhenStickIsNeutralAndNativeInputIsNeutral_LeavesEveryDirectionAtZero()
    {
        var control = CreateInputFrame(true);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        float pitch = 0f, yaw = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movement = new MovementInputs();

        CockpitInputPatch.ProcessOverride(instance, ref pitch, ref yaw, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movement, observedBlock: null!);

        pitch.ShouldBe(0f); yaw.ShouldBe(0f);
        movement.Forward.ShouldBe(0f); movement.Backward.ShouldBe(0f);
        movement.Right.ShouldBe(0f); movement.Left.ShouldBe(0f);
        movement.Up.ShouldBe(0f); movement.Down.ShouldBe(0f);
        movement.RollRight.ShouldBe(0f); movement.RollLeft.ShouldBe(0f);
        _commitCount.ShouldBe(1);
    }

    [Test]
    public void RotationHook_RestoresNativeStateAfterKontrolWasConsumedByTheGameUpdate()
    {
        var control = CreateInputFrame(true, surge: .9f, pitch: 1f, yaw: -1f, roll: .8f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        float pitch = .25f, yaw = -.4f, lookUp = .6f, lookDown = 0f, lookLeft = 0f, lookRight = .3f;
        var movement = new MovementInputs { Forward = .2f, RollLeft = .5f };

        CockpitInputPatch.UpdateRotationDataPrefix(instance, ref pitch, ref yaw, ref lookUp, ref lookDown,
            ref lookLeft, ref lookRight, ref movement, null!, out var nativeState);

        movement.Forward.ShouldBe(.9f, .001f);
        movement.RollRight.ShouldBe(.8f, .001f);
        lookDown.ShouldBe(1f);
        lookLeft.ShouldBe(1f);

        CockpitInputPatch.UpdateRotationDataPostfix(ref pitch, ref yaw, ref lookUp, ref lookDown,
            ref lookLeft, ref lookRight, ref movement, nativeState);

        pitch.ShouldBe(.25f);
        yaw.ShouldBe(-.4f);
        lookUp.ShouldBe(.6f);
        lookDown.ShouldBe(0f);
        lookLeft.ShouldBe(0f);
        lookRight.ShouldBe(.3f);
        movement.Forward.ShouldBe(.2f);
        movement.RollLeft.ShouldBe(.5f);
        movement.RollRight.ShouldBe(0f);
    }

    [TestCase(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f)]
    [TestCase(-1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f)]
    [TestCase(0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f)]
    [TestCase(0f, -1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f)]
    [TestCase(0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f)]
    [TestCase(0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 1f)]
    public void ProcessOverride_MapsEachTranslationDirectionAndCommits(
        float surge, float sway, float heave, float roll,
        float forward, float backward, float right, float left, float up, float down)
    {
        var control = CreateInputFrame(true, surge, sway, heave, roll: roll);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        float pitch = 0f, yaw = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movement = new MovementInputs();

        CockpitInputPatch.ProcessOverride(instance, ref pitch, ref yaw, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movement, observedBlock: null!);

        movement.Forward.ShouldBe(forward);
        movement.Backward.ShouldBe(backward);
        movement.Right.ShouldBe(right);
        movement.Left.ShouldBe(left);
        movement.Up.ShouldBe(up);
        movement.Down.ShouldBe(down);
        _commitCount.ShouldBe(1);
    }

    [Test]
    public void ProcessOverride_WhenOverrideActiveAndInputsExceedLimit_ShouldClampToMaxBounds()
    {
        // Arrange
        var control = CreateInputFrame(true, 1f, -1f, 1f, 1f, 1f, 1f);
        _testInputChannel!.Write(ref control);

        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));
        
        float pitchAnalog = 0.5f;
        float yawAnalog = 0.5f;
        float lookUp = 0.0f;
        float lookDown = 0.0f;
        float lookLeft = 0.0f;
        float lookRight = 0.0f;
        
        var movementInputs = new MovementInputs
        {
            Forward = 1.0f,
            Backward = 0.0f,
            Right = 0.0f,
            Left = 1.0f,
            Up = 1.0f,
            Down = 0.0f,
            RollRight = 1.0f,
            RollLeft = 0.0f
        };

        // Act
        CockpitInputPatch.ProcessOverride(
            instance,
            ref pitchAnalog,
            ref yawAnalog,
            ref lookUp,
            ref lookDown,
            ref lookLeft,
            ref lookRight,
            ref movementInputs,
            observedBlock: null!
        );

        // Assert (Must clamp values to [-1, 1])
        pitchAnalog.ShouldBe(0.5f);
        yawAnalog.ShouldBe(0.5f);
        lookDown.ShouldBe(1.0f);
        lookRight.ShouldBe(1.0f);
        movementInputs.Forward.ShouldBe(1.0f);
        movementInputs.Left.ShouldBe(1.0f);
        movementInputs.Up.ShouldBe(1.0f);
        movementInputs.RollRight.ShouldBe(1.0f);
    }
}
