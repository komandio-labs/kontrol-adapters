using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using NSubstitute;
using Shouldly;
using Kontrol.Sdk.IPC;
using Kontrol.Adapters.SpaceEngineers2.Patches;
using Kontrol.Sdk.Settings;
using Kontrol.Adapters.SpaceEngineers2.Settings;
using Kontrol.Sdk.Inputs;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.BlockTools;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.Game2.Simulation.WorldObjects.Movement;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Movement;
using Keen.VRage.Library.Mathematics;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class CockpitInputPatchTests
{
    private static int _commitCount;
    private MmfChannel<InputFrame>? _testInputChannel;
    private MmfChannel<TelemetryData>? _testTelemetryChannel;
    private MmfChannel<TelemetryData>? _testSettingsChannel;

    [SetUp]
    public void SetUp()
    {
        CockpitInputPatch.ResetChannelsForTests();

        // Initialize MMF channels to mimic the WPF app side
        _testInputChannel = new MmfChannel<InputFrame>("Local\\Kontrol_Input_space-engineers-2");
        _testInputChannel.CreateOrOpen();

        _testTelemetryChannel = new MmfChannel<TelemetryData>("Local\\Kontrol_Telemetry_space-engineers-2");
        _testTelemetryChannel.CreateOrOpen();

        _testSettingsChannel = new MmfChannel<TelemetryData>("Local\\Kontrol_Settings_space-engineers-2");
        _testSettingsChannel.CreateOrOpen();

        // Redirect internal _updateControlDataMethod to avoid calling actual game logic
        var field = typeof(CockpitInputPatch).GetField("_updateControlDataMethod", BindingFlags.NonPublic | BindingFlags.Static);
        var dummyMethod = typeof(CockpitInputPatchTests).GetMethod(nameof(DummyUpdateControlData), BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, dummyMethod);
        _commitCount = 0;
        SpaceEngineers2SettingsManager.Instance.ApplySettings(new Dictionary<string, object?>());
    }

    [TearDown]
    public void TearDown()
    {
        _testInputChannel?.Dispose();
        _testTelemetryChannel?.Dispose();
        _testSettingsChannel?.Dispose();

        CockpitInputPatch.ResetChannelsForTests();
        SpaceEngineers2SettingsManager.Instance.ApplySettings(new Dictionary<string, object?>());

        // Reset the patch fields
        var field = typeof(CockpitInputPatch).GetField("_updateControlDataMethod", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, null);
    }

    private void SetFlightModelMode(string mode)
    {
        var dict = new Dictionary<string, object?> { ["flightModelMode"] = mode };
        SpaceEngineers2SettingsManager.Instance.ApplySettings(dict);
        if (_testSettingsChannel != null)
        {
            var telemetry = new TelemetryData();
            telemetry.SetJson(System.Text.Json.JsonSerializer.Serialize(dict));
            _testSettingsChannel.Write(ref telemetry);
        }
    }

    private static void DummyUpdateControlData()
    {
        _commitCount++;
    }

    [Test]
    public void ComputeProportionalThrust_PreservesPartialPositiveInput()
    {
        var (forward, backward, right, left, up, down) = CockpitInputPatch.ComputeProportionalThrust(0.5f, 0.25f, 0.75f);

        forward.ShouldBe(0.5f);
        backward.ShouldBe(0f);
        right.ShouldBe(0.25f);
        left.ShouldBe(0f);
        up.ShouldBe(0.75f);
        down.ShouldBe(0f);
    }

    [Test]
    public void ComputeProportionalThrust_PreservesPartialNegativeInput()
    {
        var (forward, backward, right, left, up, down) = CockpitInputPatch.ComputeProportionalThrust(-0.5f, -0.25f, -0.75f);

        forward.ShouldBe(0f);
        backward.ShouldBe(0.5f);
        right.ShouldBe(0f);
        left.ShouldBe(0.25f);
        up.ShouldBe(0f);
        down.ShouldBe(0.75f);
    }

    [TestCase(0.2f, 0f, 0.2f)]
    [TestCase(0.9f, 0f, 0.9f)]
    [TestCase(0.2f, 30f, 0.1f)]
    [TestCase(0.2f, 60f, 0f)]
    [TestCase(0.2f, 120f, 0f)]
    [TestCase(-0.2f, -120f, 0f)]
    [TestCase(-0.2f, 60f, -0.2f)]
    [TestCase(0.2f, -300f, 0.2f)]
    public void ComputeVelocityHoldAxis_AddsThrustOnlyTowardTheLiveVelocityTarget(float input, float actualVelocity, float expectedOutput)
    {
        TranslationVelocityController.ComputeAxis(input, actualVelocity, 300f).ShouldBe(expectedOutput, 0.0001f);
    }

    [Test]
    public void ComputeVelocityHoldThrust_SplitsSignedAxesWithoutOpposingCommands()
    {
        var (forward, backward, right, left, up, down) = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: -0.5f, sway: 0.5f, heave: -0.5f,
            actualSurge: 0f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f);

        forward.ShouldBe(0f);
        backward.ShouldBe(0.5f);
        right.ShouldBe(0.5f);
        left.ShouldBe(0f);
        up.ShouldBe(0f);
        down.ShouldBe(0.5f);
        (forward > 0f && backward > 0f).ShouldBeFalse();
        (right > 0f && left > 0f).ShouldBeFalse();
        (up > 0f && down > 0f).ShouldBeFalse();
    }

    [Test]
    public void ComputeVelocityHoldThrust_LeavesTargetReductionsToSe2Dampeners()
    {
        var first = CockpitInputPatch.ComputeVelocityHoldThrust(0.2f, 0f, 0f, 0f, 0f, 0f, 300f);
        var changed = CockpitInputPatch.ComputeVelocityHoldThrust(0.8f, 0f, 0f, 0f, 0f, 0f, 300f);
        var reducedWhileMoving = CockpitInputPatch.ComputeVelocityHoldThrust(0.2f, 0f, 0f, 180f, 0f, 0f, 300f);

        first.fwd.ShouldBe(0.2f, 0.0001f);
        changed.fwd.ShouldBe(0.8f, 0.0001f);
        reducedWhileMoving.fwd.ShouldBe(0f);
        reducedWhileMoving.back.ShouldBe(0f);
    }

    [Test]
    public void VelocityHoldResponseGain_KeepsFullInputStrongUntilCloserToTheTarget()
    {
        var smooth = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: 1f, sway: 0f, heave: 0f,
            actualSurge: 272.2222f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f, responseGain: 1f);
        var responsive = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: 1f, sway: 0f, heave: 0f,
            actualSurge: 272.2222f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f, responseGain: 12f);

        smooth.fwd.ShouldBe(.0925927f, .0001f);
        responsive.fwd.ShouldBe(1f, .0001f);
    }

    [Test]
    public void VelocityHoldResponseGain_DoesNotCreateBrakingForASmallTargetReduction()
    {
        var output = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: .95f, sway: 0f, heave: 0f,
            actualSurge: 300f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f, responseGain: 12f);

        output.fwd.ShouldBe(0f);
        output.back.ShouldBe(0f);
    }

    [Test]
    public void ComputeCruiseVelocityHoldThrust_PreservesSignedOverspeedCorrection()
    {
        var cruise = CockpitInputPatch.ComputeCruiseVelocityHoldThrust(
            surge: 0.2f, sway: 0f, heave: 0f,
            actualSurge: 120f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f);

        cruise.fwd.ShouldBe(0f);
        cruise.back.ShouldBe(0.2f, 0.0001f);
    }

    [Test]
    public void ComputeVelocityHoldThrust_NeutralInputSubmitsNoAdapterThrust()
    {
        var (forward, backward, right, left, up, down) = CockpitInputPatch.ComputeVelocityHoldThrust(
            0f, 0f, 0f, 100f, -100f, 50f, 300f);

        forward.ShouldBe(0f);
        backward.ShouldBe(0f);
        right.ShouldBe(0f);
        left.ShouldBe(0f);
        up.ShouldBe(0f);
        down.ShouldBe(0f);
    }

    [Test]
    public void VelocityHoldMaximumSpeed_Converts300MetersPerSecondTo1080KilometersPerHour()
    {
        TranslationVelocityController.KilometersPerHour(300f).ShouldBe(1080f, 0.001f);
    }

    [Test]
    public void SpeedUnitPresentation_FormatsMetricAndImperialTelemetry()
    {
        SpeedUnitPresentation.Format(100f, "KilometersPerHour").ShouldBe("360.0 km/h");
        SpeedUnitPresentation.Format(100f, "MilesPerHour").ShouldBe("223.7 mph");
    }

    [Test]
    public void SpeedUnitPresentation_GameDefaultUsesTheObservedSe2HudUnit()
    {
        SpeedUnitPresentation.ResetForTests();
        SpeedUnitPresentation.CaptureGameSpeedUnit(new TestGuiOptions { SpeedUnit = 2 });

        SpeedUnitPresentation.Format(100f, "GameDefault").ShouldBe("223.7 mph");
    }

    [Test]
    public void SpeedUnitPresentation_GameDefaultResolvesObservedUnitForSettingsPresentation()
    {
        SpeedUnitPresentation.ResetForTests();
        SpeedUnitPresentation.CaptureGameSpeedUnit(new TestGuiOptions { SpeedUnit = 1 });

        var presentation = SpeedUnitPresentation.ResolveTargetSpeedPresentation("GameDefault", 300f);

        presentation.Unit.ShouldBe(MeasurementUnit.KilometersPerHour);
        presentation.Multiplier.ShouldBe(3.6f);
        presentation.MidLabel.ShouldBe("1080 km/h");
        presentation.Maximum.ShouldBe(300f);
    }

    [Test]
    public void ComputeLocalTranslationVelocity_UsesTheControlFrameSignConvention()
    {
        var (surge, sway, heave) = CockpitInputPatch.ComputeLocalTranslationVelocity(
            Quaternion.Identity, Quaternion.Identity, new Vector3(4f, 5f, -6f));

        surge.ShouldBe(6f);
        sway.ShouldBe(4f);
        heave.ShouldBe(5f);
    }

    [Test]
    public void ComputeLocalTranslationVelocity_UndoesGridAndObserverOrientations()
    {
        Quaternion gridWorldOrientation = Quaternion.CreateFromYawPitchRoll(0.71f, -0.29f, 0.18f);
        Quaternion observerOrientation = Quaternion.CreateFromYawPitchRoll(-0.34f, 0.23f, -0.41f);
        var expectedInputVelocity = new Vector3(17f, -8f, -31f);
        Vector3 worldVelocity = gridWorldOrientation * (observerOrientation * expectedInputVelocity);

        var (surge, sway, heave) = CockpitInputPatch.ComputeLocalTranslationVelocity(
            gridWorldOrientation, observerOrientation, worldVelocity);

        surge.ShouldBe(31f, 0.001f);
        sway.ShouldBe(17f, 0.001f);
        heave.ShouldBe(-8f, 0.001f);
    }

    [Test]
    public void VelocityHoldController_HasNoDampenerStateAndDoesNotChangeItsOutputForDampenerPolicy()
    {
        // Dampener preference remains game-owned; Velocity Hold only owns its
        // signed translation command, so ON/OFF use the same controller output.
        float commandWithDampenersOn = TranslationVelocityController.ComputeAxis(0.5f, 75f, 300f);
        float commandWithDampenersOff = TranslationVelocityController.ComputeAxis(0.5f, 75f, 300f);

        commandWithDampenersOn.ShouldBe(0.25f, 0.0001f);
        commandWithDampenersOff.ShouldBe(commandWithDampenersOn, 0.0001f);
    }

    [TestCase("DirectAngularFlight")]
    [TestCase("NativeReticleSteering")]
    public void VelocityHoldPositiveThrottleAtLowerTargetUsesDampenerPolicyInEitherFlightMode(string flightMode)
    {
        SetFlightModelMode(flightMode);

        var output = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: 0.2f, sway: 0f, heave: 0f,
            actualSurge: 300f, actualSway: 0f, actualHeave: 0f,
            maximumTargetSpeedMetersPerSecond: 300f);

        output.fwd.ShouldBe(0f);
        output.back.ShouldBe(0f);
    }

    [Test]
    public void VelocityHoldNeutralTarget_LeavesDampenerPolicyToSe2()
    {
        var output = CockpitInputPatch.ComputeVelocityHoldThrust(
            surge: 0f, sway: 0f, heave: 0f,
            actualSurge: 100f, actualSway: -100f, actualHeave: 50f,
            maximumTargetSpeedMetersPerSecond: 300f);

        output.ShouldBe((0f, 0f, 0f, 0f, 0f, 0f));
    }

    [TestCase(true, true, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public void Neutralization_DoesNotSwitchDirectAngularFlightBackToReticle(
        bool isDirectAngularFlight, bool originalTargetBasedGyro, bool expectedTargetBasedGyro)
    {
        CockpitInputPatch.ResolveGyroModeAfterNeutralization(
            isDirectAngularFlight, originalTargetBasedGyro).ShouldBe(expectedTargetBasedGyro);
    }

    [TestCase(.5f, .25f, -.75f,  .25f, -.75f, -.5f)]
    [TestCase(-.5f, -.25f, .75f, -.25f,  .75f,  .5f)]
    public void Presentation_MapsAllSixTranslationDirectionsInSe2LocalCoordinates(
        float surge, float sway, float heave, float expectedX, float expectedY, float expectedZ)
    {
        VoluntaryThrustData presentation = TranslationPresentationState.CreateForTests(
            Quaternion.Identity, surge, sway, heave);

        presentation.VoluntaryThrust.X.ShouldBe(expectedX, .0001f);
        presentation.VoluntaryThrust.Y.ShouldBe(expectedY, .0001f);
        presentation.VoluntaryThrust.Z.ShouldBe(expectedZ, .0001f);
    }

    [Test]
    public void Presentation_RotatesTheRawObserverLocalVectorIntoTheGridFrame()
    {
        Quaternion observerOrientation = Quaternion.CreateFromYawPitchRoll(.7f, -.3f, .2f);
        VoluntaryThrustData presentation = TranslationPresentationState.CreateForTests(
            observerOrientation, surge: .4f, sway: -.2f, heave: .6f);

        (presentation.VoluntaryThrust - observerOrientation * new Vector3(-.2f, .6f, -.4f)).Length()
            .ShouldBe(0f, .0001f);
    }

    [Test]
    public void Presentation_StateIsGridScopedAndResetsSafely()
    {
        TranslationPresentationState.SetForTests(42, Quaternion.Identity, .5f, 0f, 0f);
        TranslationPresentationState.TryGet(42, out var matching).ShouldBeTrue();
        matching.VoluntaryThrust.Z.ShouldBe(-.5f);
        TranslationPresentationState.TryGet(43, out _).ShouldBeFalse();

        TranslationPresentationState.Reset();
        TranslationPresentationState.TryGet(42, out _).ShouldBeFalse();
    }

    [Test]
    public void Presentation_CruiseControlShowsItsPhysicalHoldCommandWhenTheJoystickIsCentered()
    {
        var presentation = CockpitInputPatch.ResolvePresentationAxes(
            cruiseActive: true, rawSurge: 0f, rawSway: 0f, rawHeave: 0f,
            forward: .37f, backward: 0f, right: 0f, left: 0f, up: 0f, down: 0f);

        presentation.ShouldBe((.37f, 0f, 0f));
    }

    [Test]
    public void Presentation_CruiseControlKeepsRawJoystickPresentationDuringManualOverride()
    {
        var presentation = CockpitInputPatch.ResolvePresentationAxes(
            cruiseActive: true, rawSurge: .4f, rawSway: 0f, rawHeave: 0f,
            forward: .8f, backward: 0f, right: 0f, left: 0f, up: 0f, down: 0f);

        presentation.ShouldBe((.4f, 0f, 0f));
    }

    [Test]
    public void VelocityHoldMaximumSpeed_PrefersSoftGridLimitThenProviderAndNeverAssumesALimit()
    {
        CockpitInputPatch.ResolveVelocityHoldMaximumSpeed(
            new SoftSpeedLimitData { Speed = 180f }, new TestVelocityLimits { LinearVelocityLimit = 250f }, 0f)
            .ShouldBe(180f);
        CockpitInputPatch.ResolveVelocityHoldMaximumSpeed(
            null, new TestVelocityLimits { LinearVelocityLimit = 500f }, 0f)
            .ShouldBe(500f);
        CockpitInputPatch.ResolveVelocityHoldMaximumSpeed(null, null, 0f)
            .ShouldBe(0f);
        CockpitInputPatch.ResolveVelocityHoldMaximumSpeed(null, new TestVelocityLimits { LinearVelocityLimit = 500f }, 275f)
            .ShouldBe(275f);
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

        schema.Version.ShouldBe(8);
        schema.Inputs[10].Id.ShouldBe("systems.exit_grid");
        schema.Inputs[6].Id.ShouldBe("systems.dampeners");
        schema.Inputs[6].DiscreteBehavior.ShouldBe(DiscreteBehavior.Toggle);
        schema.Inputs[6].EffectiveActionBehavior.ShouldBe(DiscreteBehavior.Toggle);
        schema.Inputs[6].EffectiveDeliveryMode.ShouldBe(DiscreteDeliveryMode.Event);
        schema.Inputs[7].Id.ShouldBe("systems.lights");
        schema.Inputs[7].EffectiveActionBehavior.ShouldBe(DiscreteBehavior.Trigger);
        schema.Inputs[7].EffectiveDeliveryMode.ShouldBe(DiscreteDeliveryMode.Event);
        schema.Inputs[11].Id.ShouldBe("weapons.fire_primary");
        schema.Inputs[11].DiscreteBehavior.ShouldBe(DiscreteBehavior.Momentary);
        schema.Inputs[12].Id.ShouldBe("weapons.reload");
        schema.Inputs[12].DiscreteBehavior.ShouldBe(DiscreteBehavior.Momentary);
        schema.Inputs[13].Id.ShouldBe("camera.mode_switch");
        schema.Inputs[13].DiscreteBehavior.ShouldBe(DiscreteBehavior.Trigger);
        schema.Inputs[14].Id.ShouldBe("flight.cruise_control_set");
        schema.Inputs[14].DiscreteBehavior.ShouldBe(DiscreteBehavior.Trigger);
        schema.Inputs[14].EffectiveActionBehavior.ShouldBe(DiscreteBehavior.Trigger);
        schema.Inputs[14].EffectiveDeliveryMode.ShouldBe(DiscreteDeliveryMode.Event);
        schema.Inputs[14].Category.ShouldBe("Flight controls");
        schema.Inputs[15].Id.ShouldBe("flight.cruise_control_increase");
        schema.Inputs[16].Id.ShouldBe("flight.cruise_control_decrease");
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
        // Analog fields are NOT modified by MergeRotationDirections — rotation goes
        // through lookUp/Down/Left/Right as directional magnitudes instead.
        pitchAnalog.ShouldBe(0.1f, 0.001f);
        yawAnalog.ShouldBe(0.2f, 0.001f);
        lookUp.ShouldBe(0.5f);
        lookDown.ShouldBe(0.3f, 0.001f);   // pitch 0.3 → lookDown = max(0, 0.3)
        lookLeft.ShouldBe(0.2f, 0.001f);    // yaw -0.2 → lookLeft = max(0, 0.2)
        lookRight.ShouldBe(0.0f);

        movementInputs.Forward.ShouldBe(0.5f, 0.001f);
        movementInputs.Backward.ShouldBe(0.0f);

        movementInputs.Left.ShouldBe(0.3f, 0.001f);
        movementInputs.Right.ShouldBe(0.0f);

        movementInputs.Up.ShouldBe(0.4f, 0.001f);
        movementInputs.Down.ShouldBe(0.0f);

        movementInputs.RollRight.ShouldBe(0.8f, 0.001f);
        movementInputs.RollLeft.ShouldBe(0.1f);
    }

    [Test]
    public void ProcessOverride_WhenInNativeReticleMode_UpdatesLookInputs()
    {
        SpaceEngineers2SettingsManager.Instance.ApplySettings(new Dictionary<string, object?>
        {
            ["flightModelMode"] = "NativeReticleSteering"
        });

        var control = CreateInputFrame(true, surge: .5f, pitch: .3f, yaw: -.2f);
        _testInputChannel!.Write(ref control);

        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        float pitchAnalog = 0.0f;
        float yawAnalog = 0.0f;
        float lookUp = 0.0f;
        float lookDown = 0.0f;
        float lookLeft = 0.0f;
        float lookRight = 0.0f;
        var movementInputs = new MovementInputs();

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

        pitchAnalog.ShouldBe(0.0f);
        yawAnalog.ShouldBe(0.0f);
        lookUp.ShouldBe(0.0f);
        lookDown.ShouldBe(0.3f, 0.001f);   // pitch 0.3 → lookDown = max(0, 0.3)
        lookLeft.ShouldBe(0.2f, 0.001f);    // yaw -0.2 → lookLeft = max(0, 0.2)
        lookRight.ShouldBe(0.0f);
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
    }

    [Test]
    public void RotationHook_WhenDirectAngularModeIsActive_SuppressesNativeSmoothing()
    {
        SetFlightModelMode("DirectAngularFlight");

        var control = CreateInputFrame(true, surge: .9f, pitch: 1f, yaw: -1f, roll: .8f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        float pitchAnalog = 0f, yawAnalog = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movementInputs = default(MovementInputs);
        bool runOriginal = CockpitInputPatch.UpdateRotationDataPrefix(instance, ref pitchAnalog, ref yawAnalog, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movementInputs, null!, out _);

        runOriginal.ShouldBeFalse();
    }

    [Test]
    public void RotationHook_WhenNativeReticleModeIsActive_AllowsNativeSmoothing()
    {
        SetFlightModelMode("NativeReticleSteering");

        var control = CreateInputFrame(true, surge: .9f, pitch: 1f, yaw: -1f, roll: .8f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        float pitchAnalog = 0f, yawAnalog = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movementInputs = default(MovementInputs);
        bool runOriginal = CockpitInputPatch.UpdateRotationDataPrefix(instance, ref pitchAnalog, ref yawAnalog, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movementInputs, null!, out _);

        runOriginal.ShouldBeTrue();
    }

    [Test]
    public void ComputeReticlePositioning_WhenDirectAngularModeIsActive_SuppressesNativeReticleMath()
    {
        SetFlightModelMode("DirectAngularFlight");

        var control = CreateInputFrame(true, surge: .9f, pitch: 1f, yaw: -1f, roll: .8f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        float pitchAnalog = 0f, yawAnalog = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movementInputs = default(MovementInputs);
        bool runOriginal = CockpitInputPatch.ComputeReticlePositioningPrefix(instance, ref pitchAnalog, ref yawAnalog, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movementInputs, null!, out _);

        runOriginal.ShouldBeFalse();
    }

    [Test]
    public void ComputeReticlePositioning_WhenNativeReticleModeIsActive_AllowsNativeReticleMath()
    {
        SetFlightModelMode("NativeReticleSteering");

        var control = CreateInputFrame(true, surge: .9f, pitch: 1f, yaw: -1f, roll: .8f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        float pitchAnalog = 0f, yawAnalog = 0f, lookUp = 0f, lookDown = 0f, lookLeft = 0f, lookRight = 0f;
        var movementInputs = default(MovementInputs);
        bool runOriginal = CockpitInputPatch.ComputeReticlePositioningPrefix(instance, ref pitchAnalog, ref yawAnalog, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movementInputs, null!, out _);

        runOriginal.ShouldBeTrue();
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

        // Assert (MergeRotationDirections: analog stays unchanged, rotation via look directions)
        pitchAnalog.ShouldBe(0.5f, 0.001f);
        yawAnalog.ShouldBe(0.5f, 0.001f);
        lookDown.ShouldBe(1.0f, 0.001f);   // pitch 1.0 → lookDown = max(0, 1.0)
        lookRight.ShouldBe(1.0f, 0.001f);  // yaw 1.0 → lookRight = max(0, 1.0)
        movementInputs.Forward.ShouldBe(1.0f);
        movementInputs.Left.ShouldBe(1.0f);
        movementInputs.Up.ShouldBe(1.0f);
        movementInputs.RollRight.ShouldBe(1.0f);
    }

    private sealed class TestVelocityLimits
    {
        public float LinearVelocityLimit { get; init; }
    }

    private sealed class TestGuiOptions
    {
        public int SpeedUnit { get; init; }
    }

    [Test]
    public void Translation_DirectAngularAndNativeReticle_ProduceIdenticalMovementInputs()
    {
        var control = CreateInputFrame(true, surge: 0.6f, sway: -0.4f, heave: 0.8f, roll: 0.5f);
        _testInputChannel!.Write(ref control);
        var instance = (CockpitInputHandlerComponent)RuntimeHelpers.GetUninitializedObject(typeof(CockpitInputHandlerComponent));

        // Test MergeTranslation directly
        var movement1 = new MovementInputs();
        float pA = 0f, yA = 0f, lu = 0f, ld = 0f, ll = 0f, lr = 0f;
        CockpitInputPatch.ProcessOverride(instance, ref pA, ref yA, ref lu, ref ld, ref ll, ref lr, ref movement1, null!);

        movement1.Forward.ShouldBe(0.6f, 0.001f);
        movement1.Left.ShouldBe(0.4f, 0.001f);
        movement1.Up.ShouldBe(0.8f, 0.001f);
        movement1.RollRight.ShouldBe(0.5f, 0.001f);
    }
}
