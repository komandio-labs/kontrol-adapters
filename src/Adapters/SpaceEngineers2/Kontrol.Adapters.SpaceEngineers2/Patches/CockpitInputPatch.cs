using System.Reflection;
using HarmonyLib;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers;
using Keen.Game2.Simulation.GameSystems.Movement;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks;
using Keen.Game2.Simulation.WorldObjects.Movement;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Mathematics;
using Kontrol.Sdk.IPC;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

[HarmonyPatch]
public static class CockpitInputPatch
{
    public readonly record struct NativeInputSnapshot(
        float PitchAnalog, float YawAnalog,
        float LookUp, float LookDown, float LookLeft, float LookRight,
        MovementInputs MovementInputs);

    private static readonly MethodInfo? SwitchGyroModeMethod = AccessTools.DeclaredMethod(
        typeof(CockpitInputHandlerComponent), "SwitchGyroMode", [typeof(bool)]);

    private static readonly FieldInfo? CockpitComponentField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_cockpitComponent");

    private static readonly FieldInfo? VelocityLimitsField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_velocityLimits");

    private static readonly FieldInfo? ObserverChildTransformField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_observerChildTransform");

    private static readonly FieldInfo? ObservedBlockField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_observedBlock");

    private static readonly FieldInfo? MovementInputsField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_movementInputs");

    private static readonly FieldInfo? TargetBasedGyroField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_targetBasedGyro");

    private static bool _wasKontrolActiveInCockpit;
    private static bool _originalDesiredTargetBasedGyro = true;
    private static object? _lastObservedBlock;
    private static Vector3 _currentCockpitAngularVelocity;

    private static readonly Dictionary<int, string> TriggerActions = new()
    {
        [6] = "ToggleDampeners",
        [7] = "ToggleLights",
        [8] = "ToggleParkingBrakes",
        [9] = "TogglePower",
        [10] = "InteractionActivated"
    };
    private static readonly Dictionary<(Type Type, string Name), MethodInfo?> TriggerMethods = new();

    private static readonly MmfChannel<InputFrame> ControlChannel = new("Local\\Kontrol_Input_space-engineers-2");
    private static readonly MmfChannel<TelemetryData> TelemetryChannel = new("Local\\Kontrol_Telemetry_space-engineers-2");
    private static readonly object ChannelInitializationLock = new();
    private static bool _channelsInitialized;
    private static bool _channelFailureReported;
    private static bool _cockpitHookObserved;
    private static DateTime _lastFrameDebugUtc;
    private static string? _lastFrameDebugSummary;
    private static ulong _lastDiscreteDebugState;
    private static DateTime _lastAppliedDebugUtc;
    private static string? _lastAppliedDebugSummary;
    private static bool _missingObservedBlockReported;
    private static ulong _previousTriggeredActions;

    private static void EnsureChannels()
    {
        if (_channelsInitialized) return;
        lock (ChannelInitializationLock)
        {
            if (_channelsInitialized) return;
            try
            {
                ControlChannel.CreateOrOpen();
                TelemetryChannel.CreateOrOpen();
                _channelsInitialized = true;
                _channelFailureReported = false;
            }
            catch (Exception ex)
            {
                if (_channelFailureReported) return;
                _channelFailureReported = true;
                SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter could not open its input channel.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"IPC channel initialization error: {ex}");
            }
        }
    }

    internal static bool TryReadControlFrame(out InputFrame control)
    {
        EnsureChannels();
        if (!_channelsInitialized)
        {
            control = default;
            return false;
        }

        try
        {
            ControlChannel.Read(out control);
            return true;
        }
        catch (Exception ex)
        {
            control = default;
            if (_channelFailureReported) return false;
            _channelFailureReported = true;
            SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter could not read its input channel.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"IPC channel read error: {ex}");
            return false;
        }
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "ComputeReticlePositioning")]
    [HarmonyPrefix]
    public static bool ComputeReticlePositioningPrefix(CockpitInputHandlerComponent __instance)
    {
        try
        {
            if (ApplyCurrentKontrolFrame(__instance))
            {
                return false; // Skip original reticle integration job when Kontrol is active
            }
            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"ComputeReticlePositioningPrefix error: {ex}");
            return true;
        }
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "UpdateRotationData")]
    [HarmonyPrefix]
    public static bool UpdateRotationDataPrefix(CockpitInputHandlerComponent __instance)
    {
        try
        {
            if (ApplyCurrentKontrolFrame(__instance))
            {
                return false; // Skip original smoothing/decay job when Kontrol is active
            }
            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"UpdateRotationDataPrefix error: {ex}");
            return true;
        }
    }

    public static bool UpdateControlDataPrefix(CockpitInputHandlerComponent __instance)
    {
        try
        {
            if (ApplyCurrentKontrolFrame(__instance))
            {
                return false; // Skip native UpdateControlData body to prevent resets
            }
            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"UpdateControlDataPrefix error: {ex}");
            return true;
        }
    }

    public static unsafe bool ApplyCurrentKontrolFrame(CockpitInputHandlerComponent instance)
    {
        try
        {
            EnsureChannels();

            if (!TryReadControlFrame(out var control) || control.IsInputEnabled == 0)
            {
                if (_wasKontrolActiveInCockpit)
                {
                    NeutralizeCockpitInput(instance);
                }
                return false;
            }

            if (!_cockpitHookObserved)
            {
                _cockpitHookObserved = true;
                SpaceEngineers2AdapterDiagnostics.Write("Kontrol input is available while piloting a cockpit.");
            }

            var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(instance);
            if (observedBlock != _lastObservedBlock)
            {
                _lastObservedBlock = observedBlock;
                SpaceEngineers2AdapterDiagnostics.WriteDebug(observedBlock is null
                    ? "SE2 cleared the observed cockpit block."
                    : $"Player entered cockpit block ({observedBlock.GetType().Name}).");
            }

            // 1. Mode Management: Switch SE2 cockpit to native angular mode cleanly
            var cockpitComponent = (CockpitComponent?)CockpitComponentField?.GetValue(instance);
            bool currentTargetBased = TargetBasedGyroField?.GetValue(instance) is true;

            if (!_wasKontrolActiveInCockpit || currentTargetBased)
            {
                _originalDesiredTargetBasedGyro = cockpitComponent?.TargetBasedGyro ?? true;
                SwitchGyroModeMethod?.Invoke(instance, [false]);
                _wasKontrolActiveInCockpit = true;
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Switched cockpit gyro mode to angular (saved desired target-based={_originalDesiredTargetBasedGyro}).");
            }

            // 2. Read and normalize axes directly from joystick frame
            float pitch = NormalizeAxis(control.AnalogValues[0]);
            float roll = NormalizeAxis(control.AnalogValues[1]);
            float yaw = NormalizeAxis(control.AnalogValues[2]);
            float surge = NormalizeAxis(control.AnalogValues[3]);
            float sway = NormalizeAxis(control.AnalogValues[4]);
            float heave = NormalizeAxis(control.AnalogValues[5]);

            LogReceivedFrame(control.SchemaVersion, pitch, roll, yaw, surge, sway, heave,
                control.DiscreteStates, control.TriggeredActions);

            // 3. Handle discrete actions
            ProcessTriggeredActions(instance, control.TriggeredActions);
            ActiveToolActionPatch.ApplyPrimaryFire((control.DiscreteStates & (1UL << 11)) != 0);
            ActiveToolActionPatch.ApplyReload((control.DiscreteStates & (1UL << 12)) != 0);

            // 4. Submit translation & physics ControlData via Fly-by-Wire Speed Governor (Mode 2)
            var observerChild = (ChildTransformComponent?)ObserverChildTransformField?.GetValue(instance);
            var gridEntity = observedBlock?.Grid?.Entity;

            if (gridEntity != null && observerChild != null)
            {
                var observerOrientation = observerChild.Data.Get<RelativeTransform>().Orientation;

                float maxLinearSpeed = 100.0f;
                var velocityLimits = (IVelocityLimitProvider?)VelocityLimitsField?.GetValue(instance);
                if (velocityLimits != null && float.IsFinite(velocityLimits.LinearVelocityLimit) && velocityLimits.LinearVelocityLimit > 0f)
                {
                    maxLinearSpeed = velocityLimits.LinearVelocityLimit;
                }

                float commandedSurge = surge;
                float commandedSway = sway;
                float commandedHeave = heave;

                // Closed-loop velocity governor: throttle position = target velocity (like Star Citizen / DCS / Flight Sim)
                if (gridEntity.Data.Has<Keen.VRage.Physics.Data.RigidBodyData>())
                {
                    var rigidBody = gridEntity.Data.Get<Keen.VRage.Physics.Data.RigidBodyData>();
                    WorldTransform worldTransform = gridEntity.Data.GetWorldTransform();
                    Vector3 shipVelLocal = WorldTransform.TransformDirectionInv(rigidBody.LinearVelocity, worldTransform);
                    Vector3 cockpitVel = Quaternion.Inverse(observerOrientation) * shipVelLocal;

                    float currentForward = -cockpitVel.Z;
                    float currentStrafe = cockpitVel.X;
                    float currentLift = cockpitVel.Y;

                    float targetForward = surge * maxLinearSpeed;
                    float targetStrafe = sway * maxLinearSpeed;
                    float targetLift = heave * maxLinearSpeed;

                    float kp = 0.25f; // 4 m/s proportional band to smoothly throttle back into cruise speed
                    commandedSurge = Math.Clamp((targetForward - currentForward) * kp, -1f, 1f);
                    commandedSway = Math.Clamp((targetStrafe - currentStrafe) * kp, -1f, 1f);
                    commandedHeave = Math.Clamp((targetLift - currentLift) * kp, -1f, 1f);
                }

                var movementInputs = new MovementInputs
                {
                    Forward = Math.Max(commandedSurge, 0f),
                    Backward = Math.Max(-commandedSurge, 0f),
                    Right = Math.Max(commandedSway, 0f),
                    Left = Math.Max(-commandedSway, 0f),
                    Up = Math.Max(commandedHeave, 0f),
                    Down = Math.Max(-commandedHeave, 0f),
                    RollRight = Math.Max(roll, 0f),
                    RollLeft = Math.Max(-roll, 0f),
                    Pitch = pitch,
                    Yaw = yaw
                };

                gridEntity.Data.UpdateControlData(in movementInputs, new Quaternion?(observerOrientation), isAngular: false);

                // 5. Direct Angular Target with exponential response curve, realistic angular rate scaling,
                // and smooth inertial slewing filter so the ship accelerates and decelerates with true physical inertia.
                float shapedPitch = ShapeResponse(pitch, exponent: 2.0f);
                float shapedRoll = ShapeResponse(roll, exponent: 2.0f);
                float shapedYaw = ShapeResponse(yaw, exponent: 2.0f);

                float targetRateScale = 0.85f; // ~48 deg/sec at 100% stick deflection
                Vector3 targetCockpitAngular = new Vector3(-shapedPitch, -shapedYaw, -shapedRoll) * targetRateScale;

                float dt = 1f / 60f;
                // Physical rotational ramp-up & braking deceleration rates (rad/s^2)
                float accelRate = 1.3f; // ~0.65s to reach 100% speed
                float decelRate = 1.0f; // ~0.85s natural glide down when centered

                _currentCockpitAngularVelocity.X = SlewAxis(_currentCockpitAngularVelocity.X, targetCockpitAngular.X, dt, accelRate, decelRate);
                _currentCockpitAngularVelocity.Y = SlewAxis(_currentCockpitAngularVelocity.Y, targetCockpitAngular.Y, dt, accelRate, decelRate);
                _currentCockpitAngularVelocity.Z = SlewAxis(_currentCockpitAngularVelocity.Z, targetCockpitAngular.Z, dt, accelRate, decelRate);

                var cockpitOrientation = observedBlock.Data.GetRelativeTransform().Orientation;
                Vector3 gridAngular = cockpitOrientation * _currentCockpitAngularVelocity;

                gridEntity.Data.Set(new AngularControlData
                {
                    TargetAngularVelocity = gridAngular
                });

                LogAppliedGameState(movementInputs.Forward, movementInputs.Backward,
                    movementInputs.Right, movementInputs.Left, movementInputs.Up, movementInputs.Down,
                    movementInputs.RollRight, movementInputs.RollLeft, pitch, yaw);
            }

            // 7. Write telemetry to WPF app
            WriteTelemetry(observedBlock);

            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Error in ApplyCurrentKontrolFrame.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"ApplyCurrentKontrolFrame exception: {ex}");
            return false;
        }
    }

    public static void NeutralizeCockpitInput(CockpitInputHandlerComponent instance)
    {
        try
        {
            if (!_wasKontrolActiveInCockpit) return;
            _wasKontrolActiveInCockpit = false;

            var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(instance);
            var gridEntity = observedBlock?.Grid?.Entity;
            if (gridEntity != null)
            {
                var zeroInputs = default(MovementInputs);
                gridEntity.Data.UpdateControlData(in zeroInputs, null, isAngular: false);
                gridEntity.Data.Set(new AngularControlData { TargetAngularVelocity = Vector3.Zero });
            }

            _currentCockpitAngularVelocity = Vector3.Zero;
            ActiveToolActionPatch.ApplyPrimaryFire(false);
            ActiveToolActionPatch.ApplyReload(false);

            var cockpitComponent = (CockpitComponent?)CockpitComponentField?.GetValue(instance);
            bool desiredMode = cockpitComponent?.TargetBasedGyro ?? _originalDesiredTargetBasedGyro;
            SwitchGyroModeMethod?.Invoke(instance, [desiredMode]);
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Restored cockpit gyro mode to: {desiredMode}.");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"NeutralizeCockpitInput error: {ex}");
        }
    }

    public static unsafe void ProcessOverride(
        CockpitInputHandlerComponent instance,
        ref float pitchAnalog,
        ref float yawAnalog,
        ref float lookUp,
        ref float lookDown,
        ref float lookLeft,
        ref float lookRight,
        ref MovementInputs movementInputs,
        object observedBlock
    )
    {
        if (TryReadControlFrame(out var control) && control.IsInputEnabled != 0)
        {
            float pitch = NormalizeAxis(control.AnalogValues[0]);
            float roll = NormalizeAxis(control.AnalogValues[1]);
            float yaw = NormalizeAxis(control.AnalogValues[2]);
            float surge = NormalizeAxis(control.AnalogValues[3]);
            float sway = NormalizeAxis(control.AnalogValues[4]);
            float heave = NormalizeAxis(control.AnalogValues[5]);

            movementInputs.Forward = Math.Max(surge, 0f);
            movementInputs.Backward = Math.Max(-surge, 0f);
            movementInputs.Right = Math.Max(sway, 0f);
            movementInputs.Left = Math.Max(-sway, 0f);
            movementInputs.Up = Math.Max(heave, 0f);
            movementInputs.Down = Math.Max(-heave, 0f);
            movementInputs.RollRight = Math.Max(roll, 0f);
            movementInputs.RollLeft = Math.Max(-roll, 0f);
            movementInputs.Pitch = pitch;
            movementInputs.Yaw = yaw;
        }
        ApplyCurrentKontrolFrame(instance);
    }

    private static float ShapeResponse(float value, float exponent = 1.8f)
    {
        if (!float.IsFinite(value) || value == 0f) return 0f;
        float clamped = Math.Clamp(value, -1f, 1f);
        return MathF.Sign(clamped) * MathF.Pow(MathF.Abs(clamped), exponent);
    }

    private static float SlewAxis(float current, float target, float dt, float accelRate, float decelRate)
    {
        float rate = MathF.Abs(target) >= MathF.Abs(current) && MathF.Sign(target) == MathF.Sign(current)
            ? accelRate
            : decelRate;

        float maxDelta = rate * dt;
        float delta = target - current;
        if (MathF.Abs(delta) <= maxDelta)
        {
            return target;
        }
        return current + MathF.Sign(delta) * maxDelta;
    }

    private static float NormalizeAxis(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

    private static void WriteTelemetry(CubeBlockComponent? observedBlock)
    {
        if (observedBlock is null)
        {
            if (!_missingObservedBlockReported)
            {
                _missingObservedBlockReported = true;
                SpaceEngineers2AdapterDiagnostics.WriteDebug("Cockpit input handler ran before SE2 supplied an observed block; skipped cockpit telemetry.");
            }
            return;
        }
        _missingObservedBlockReported = false;

        var entity = observedBlock.Grid?.Entity;
        if (entity is null) return;

        var telemetryDict = new Dictionary<string, string>
        {
            { "Status", "In Cockpit" },
            { "Tick Count", Environment.TickCount.ToString() }
        };

        if (entity.Data.Has<Keen.VRage.Physics.Data.RigidBodyMassProperties>())
        {
            var massProps = entity.Data.Get<Keen.VRage.Physics.Data.RigidBodyMassProperties>();
            telemetryDict["Ship Mass"] = $"{massProps.Mass:N0} kg";
        }

        if (entity.Data.Has<Keen.VRage.Physics.Data.RigidBodyData>())
        {
            var rigidBody = entity.Data.Get<Keen.VRage.Physics.Data.RigidBodyData>();
            var linVel = rigidBody.LinearVelocity;
            var angVel = rigidBody.AngularVelocity;

            float linSpeed = (float)Math.Sqrt(linVel.X * linVel.X + linVel.Y * linVel.Y + linVel.Z * linVel.Z);
            float angSpeed = (float)Math.Sqrt(angVel.X * angVel.X + angVel.Y * angVel.Y + angVel.Z * angVel.Z);

            telemetryDict["Linear Speed"] = $"{linSpeed:F1} m/s";
            telemetryDict["Angular Speed"] = $"{angSpeed:F2} rad/s";
        }

        bool damp = entity.Data.Has<DampeningData>();
        telemetryDict["Dampeners"] = damp ? "Enabled" : "Disabled";

        var telemetry = new TelemetryData();
        string json = System.Text.Json.JsonSerializer.Serialize(telemetryDict);
        telemetry.SetJson(json);
        TelemetryChannel.Write(ref telemetry);
    }

    private static void ProcessTriggeredActions(object instance, ulong triggeredActions)
    {
        ulong newActions = triggeredActions & ~_previousTriggeredActions;
        _previousTriggeredActions = triggeredActions;
        if (newActions == 0) return;

        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Received Kontrol vehicle-system action bits: 0x{newActions:X}.");
        CameraActionPatch.ProcessCameraModeSwitch(newActions);
        foreach (var (bit, methodName) in TriggerActions)
        {
            if ((newActions & (1UL << bit)) == 0) continue;
            var methodKey = (instance.GetType(), methodName);
            if (!TriggerMethods.TryGetValue(methodKey, out var method))
            {
                method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                TriggerMethods[methodKey] = method;
            }
            if (method is null)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"Kontrol received action '{methodName}', but SE2 does not expose the expected control method.");
                continue;
            }

            try
            {
                InvokeButtonAction(instance, method, true);
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Kontrol invoked SE2 cockpit action '{methodName}'.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"SE2 rejected Kontrol cockpit action '{methodName}'.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 cockpit action '{methodName}' error: {ex.InnerException}");
            }
            catch (Exception ex)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"Kontrol could not invoke SE2 cockpit action '{methodName}'.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 cockpit action '{methodName}' error: {ex}");
            }
        }
    }

    private static void InvokeButtonAction(object instance, MethodInfo method, bool value)
    {
        method.Invoke(instance, CreateButtonActionArguments(method, value));
    }

    private static object[] CreateButtonActionArguments(MethodInfo method, bool value)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(bool) ||
            parameters[1].ParameterType.FullName != "Keen.VRage.Input.ControlActivation" ||
            !parameters[1].ParameterType.IsEnum)
        {
            throw new MissingMethodException(
                $"SE2 action '{method.Name}' has an unsupported signature ({string.Join(", ", parameters.Select(parameter => parameter.ParameterType.Name))}).");
        }

        return
        [
            value,
            Enum.ToObject(parameters[1].ParameterType, value ? 0 : 2)
        ];
    }

    private static void LogReceivedFrame(uint schemaVersion, float pitch, float roll, float yaw, float surge, float sway, float heave, ulong discrete, ulong actions)
    {
        string summary = $"schema={schemaVersion}; pitch={pitch:F2}; roll={roll:F2}; yaw={yaw:F2}; forward={surge:F2}; strafe={sway:F2}; lift={heave:F2}; discrete=0x{discrete:X}; actions=0x{actions:X}";
        bool discreteChanged = discrete != _lastDiscreteDebugState;
        if (actions == 0 && !discreteChanged &&
            (summary == _lastFrameDebugSummary || DateTime.UtcNow - _lastFrameDebugUtc < TimeSpan.FromMilliseconds(250))) return;
        _lastFrameDebugSummary = summary;
        _lastDiscreteDebugState = discrete;
        _lastFrameDebugUtc = DateTime.UtcNow;
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Received Kontrol input frame: {summary}.");
    }

    private static void LogAppliedGameState(float forward, float backward, float right, float left, float up, float down, float rollRight, float rollLeft, float pitch, float yaw)
    {
        string summary = $"movement F/B={forward:F2}/{backward:F2}, R/L={right:F2}/{left:F2}, U/D={up:F2}/{down:F2}, roll R/L={rollRight:F2}/{rollLeft:F2}, pitch/yaw={pitch:F2}/{yaw:F2}";
        if (summary == _lastAppliedDebugSummary || DateTime.UtcNow - _lastAppliedDebugUtc < TimeSpan.FromMilliseconds(250)) return;
        _lastAppliedDebugSummary = summary;
        _lastAppliedDebugUtc = DateTime.UtcNow;
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Applied to SE2: {summary}.");
    }
}
