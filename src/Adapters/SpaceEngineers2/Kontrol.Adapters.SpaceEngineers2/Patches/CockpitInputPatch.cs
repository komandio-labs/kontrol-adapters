using System.Reflection;
using HarmonyLib;
using Keen.VRage.DCS.Components;
using Kontrol.Sdk.IPC;

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

[HarmonyPatch]
public static class CockpitInputPatch
{
    public readonly record struct NativeInputSnapshot(
        float PitchAnalog, float YawAnalog,
        float LookUp, float LookDown, float LookLeft, float LookRight,
        Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs MovementInputs);

    private static bool _lastOverrideActiveState;
    private static object? _lastObservedBlock;
    private static readonly Dictionary<int, string> TriggerActions = new()
    {
        [6] = "ToggleDampeners",
        [7] = "ToggleLights",
        [8] = "ToggleParkingBrakes",
        [9] = "TogglePower",
        [10] = "InteractionActivated"
    };
    private static readonly Dictionary<(Type Type, string Name), MethodInfo?> TriggerMethods = new();

    private static readonly MmfChannel<InputFrame> ControlChannel = new("Local\\Kontrol_Input_SE2");
    private static readonly MmfChannel<TelemetryData> TelemetryChannel = new("Local\\Kontrol_Telemetry_SE2");
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
    private static MethodInfo? _updateControlDataMethod = AccessTools.DeclaredMethod(
        typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent),
        "UpdateControlData");
    [ThreadStatic]
    private static bool _committingControlData;

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

    [HarmonyPatch(typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent), "UpdateRotationData")]
    [HarmonyPrefix]
    public static void UpdateRotationDataPrefix(
        Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent __instance,
        ref float ____pitchAnalog,
        ref float ____yawAnalog,
        ref float ____lookUp,
        ref float ____lookDown,
        ref float ____lookLeft,
        ref float ____lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs ____movementInputs,
        object ____observedBlock,
        out NativeInputSnapshot __state
    )
    {
        __state = CaptureNativeInput(____pitchAnalog, ____yawAnalog, ____lookUp, ____lookDown,
            ____lookLeft, ____lookRight, ____movementInputs);
        ProcessOverride(__instance, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown, ref ____lookLeft, ref ____lookRight, ref ____movementInputs, ____observedBlock);
    }

    [HarmonyPatch(typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent), "UpdateRotationData")]
    [HarmonyPostfix]
    public static void UpdateRotationDataPostfix(
        ref float ____pitchAnalog, ref float ____yawAnalog,
        ref float ____lookUp, ref float ____lookDown, ref float ____lookLeft, ref float ____lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs ____movementInputs,
        NativeInputSnapshot __state) =>
        RestoreNativeInput(__state, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
            ref ____lookLeft, ref ____lookRight, ref ____movementInputs);

    [HarmonyPatch(typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent), "ComputeReticlePositioning")]
    [HarmonyPrefix]
    public static void ComputeReticlePositioningPrefix(
        Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent __instance,
        ref float ____pitchAnalog,
        ref float ____yawAnalog,
        ref float ____lookUp,
        ref float ____lookDown,
        ref float ____lookLeft,
        ref float ____lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs ____movementInputs,
        object ____observedBlock,
        out NativeInputSnapshot __state
    )
    {
        __state = CaptureNativeInput(____pitchAnalog, ____yawAnalog, ____lookUp, ____lookDown,
            ____lookLeft, ____lookRight, ____movementInputs);
        ProcessOverride(__instance, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown, ref ____lookLeft, ref ____lookRight, ref ____movementInputs, ____observedBlock);
    }

    [HarmonyPatch(typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent), "ComputeReticlePositioning")]
    [HarmonyPostfix]
    public static void ComputeReticlePositioningPostfix(
        ref float ____pitchAnalog, ref float ____yawAnalog,
        ref float ____lookUp, ref float ____lookDown, ref float ____lookLeft, ref float ____lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs ____movementInputs,
        NativeInputSnapshot __state) =>
        RestoreNativeInput(__state, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
            ref ____lookLeft, ref ____lookRight, ref ____movementInputs);

    // UpdateControlData is SE2's final movement commit: it passes
    // _movementInputs to EntityMovementExtensions.UpdateControlData. Translation
    // written earlier during rotation processing can be replaced by the game's
    // own input handling, so apply it immediately before this commit instead.
    // Registered explicitly by the adapter runtime. ProcessOverride has already merged
    // Kontrol with SE2's native fields before invoking UpdateControlData. This
    // prefix handles action edges only; changing/restoring movement here would
    // erase pitch/yaw before SE2's rotation job consumes them.
    public static unsafe void UpdateControlDataPrefix(
        Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent __instance)
    {
        try
        {
            if (!TryReadControlFrame(out var control)) return;
            bool inputEnabled = control.IsInputEnabled != 0;
            ProcessTriggeredActions(__instance, inputEnabled ? control.TriggeredActions : 0);
            ActiveToolActionPatch.ApplyPrimaryFire(inputEnabled && (control.DiscreteStates & (1UL << 11)) != 0);
            ActiveToolActionPatch.ApplyReload(inputEnabled && (control.DiscreteStates & (1UL << 12)) != 0);
            if (!inputEnabled) return;

            float surge = control.AnalogValues[3], sway = control.AnalogValues[4], heave = control.AnalogValues[5], roll = control.AnalogValues[1];
            LogFinalMovementCommit(surge, sway, heave, roll);
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter could not apply Kontrol translation at SE2's final movement commit.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"UpdateControlData override error: {ex}");
        }
    }

    public static unsafe void ProcessOverride(
        Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent instance,
        ref float pitchAnalog,
        ref float yawAnalog,
        ref float lookUp,
        ref float lookDown,
        ref float lookLeft,
        ref float lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs movementInputs,
        object observedBlock
    )
    {
        try
        {
            EnsureChannels();

            if (!_cockpitHookObserved)
            {
                _cockpitHookObserved = true;
                SpaceEngineers2AdapterDiagnostics.Write("Kontrol input is available while piloting a cockpit.");
            }

            if (observedBlock != _lastObservedBlock)
            {
                _lastObservedBlock = observedBlock;
                SpaceEngineers2AdapterDiagnostics.WriteDebug(observedBlock is null
                    ? "SE2 cleared the observed cockpit block."
                    : $"Player entered cockpit block ({observedBlock.GetType().Name}).");
            }

            // Read the control overrides from the MMF
            if (!TryReadControlFrame(out var control)) return;

            bool isInputEnabled = control.IsInputEnabled != 0;
            if (isInputEnabled != _lastOverrideActiveState)
            {
                _lastOverrideActiveState = isInputEnabled;
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Input override state changed to: {isInputEnabled}.");
            }

            if (isInputEnabled)
            {
                float pitch = NormalizeAxis(control.AnalogValues[0]);
                float roll = NormalizeAxis(control.AnalogValues[1]);
                float yaw = NormalizeAxis(control.AnalogValues[2]);
                float surge = NormalizeAxis(control.AnalogValues[3]);
                float sway = NormalizeAxis(control.AnalogValues[4]);
                float heave = NormalizeAxis(control.AnalogValues[5]);
                // This is the host-to-adapter boundary. The rate-limited trace is
                // intentionally emitted only in adapter debug mode.
                LogReceivedFrame(control.SchemaVersion, pitch, roll, yaw, surge, sway, heave,
                    control.DiscreteStates, control.TriggeredActions);
                // Preserve SE2's native mouse/keyboard state and add Kontrol as
                // another controller for this update. Directional pairs use the
                // stronger magnitude, matching how simultaneous input devices
                // normally feed the same SE2 action.
                MergeTranslation(ref movementInputs, surge, sway, heave, roll);
                MergeRotationDirections(ref lookUp, ref lookDown, ref lookLeft, ref lookRight, pitch, yaw);

                LogAppliedGameState(pitchAnalog, yawAnalog, movementInputs.Forward, movementInputs.Backward,
                    movementInputs.Right, movementInputs.Left, movementInputs.Up, movementInputs.Down,
                    movementInputs.RollRight, movementInputs.RollLeft);

                // SE2 normally calls UpdateControlData only when one of its own
                // InputContext handlers changes. Kontrol writes the private
                // fields from a DCS update job, so explicitly submit this full
                // state (including an all-zero neutral state) to the ship.
                CommitControlData(instance);
            }

            // SE2 may call this input handler while transitioning into/out of a
            // cockpit, before its observed block has been assigned. The Kontrol
            // frame may still be valid, but cockpit-specific telemetry cannot be
            // read yet.
            if (observedBlock is null)
            {
                if (!_missingObservedBlockReported)
                {
                    _missingObservedBlockReported = true;
                    SpaceEngineers2AdapterDiagnostics.WriteDebug("Cockpit input handler ran before SE2 supplied an observed block; applied input but skipped cockpit telemetry.");
                }
                return;
            }
            _missingObservedBlockReported = false;

            // Extract and write telemetry (game -> WPF app)
            var gridProp = observedBlock.GetType().GetProperty("Grid", BindingFlags.Public | BindingFlags.Instance);
            var grid = gridProp?.GetValue(observedBlock);
            var entityProp = grid?.GetType().GetProperty("Entity", BindingFlags.Public | BindingFlags.Instance);

            if (entityProp?.GetValue(grid) is Entity entity)
            {
                var telemetryDict = new Dictionary<string, string>
                {
                    { "Status", "In Cockpit" },
                    { "Tick Count", Environment.TickCount.ToString() }
                };

                // Ship Mass
                if (entity.Data.Has<Keen.VRage.Physics.Data.RigidBodyMassProperties>())
                {
                    var massProps = entity.Data.Get<Keen.VRage.Physics.Data.RigidBodyMassProperties>();
                    telemetryDict["Ship Mass"] = $"{massProps.Mass:N0} kg";
                }

                // Linear and Angular Speeds
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

                // Dampeners status
                bool damp = entity.Data.Has<Keen.Game2.Simulation.WorldObjects.Movement.DampeningData>();
                telemetryDict["Dampeners"] = damp ? "Enabled" : "Disabled";

                var telemetry = new TelemetryData();
                string json = System.Text.Json.JsonSerializer.Serialize(telemetryDict);
                telemetry.SetJson(json);

                // Write to Telemetry MMF channel
                TelemetryChannel.Write(ref telemetry);
            }
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter encountered an input-processing error.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"ProcessOverride error: {ex}");
        }
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

    private static void MergeTranslation(ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs movementInputs, float surge, float sway, float heave, float roll)
    {
        movementInputs.Forward = Math.Max(movementInputs.Forward, Math.Max(surge, 0f));
        movementInputs.Backward = Math.Max(movementInputs.Backward, Math.Max(-surge, 0f));
        movementInputs.Right = Math.Max(movementInputs.Right, Math.Max(sway, 0f));
        movementInputs.Left = Math.Max(movementInputs.Left, Math.Max(-sway, 0f));
        movementInputs.Up = Math.Max(movementInputs.Up, Math.Max(heave, 0f));
        movementInputs.Down = Math.Max(movementInputs.Down, Math.Max(-heave, 0f));
        movementInputs.RollRight = Math.Max(movementInputs.RollRight, Math.Max(roll, 0f));
        movementInputs.RollLeft = Math.Max(movementInputs.RollLeft, Math.Max(-roll, 0f));
    }

    private static void MergeRotationDirections(
        ref float lookUp, ref float lookDown, ref float lookLeft, ref float lookRight,
        float pitch, float yaw)
    {
        pitch = NormalizeAxis(pitch);
        yaw = NormalizeAxis(yaw);
        // UpdateControlData calculates Pitch = analog - (Up - Down) * digital
        // and Yaw = analog + (Right - Left) * digital. These mappings preserve
        // the old Kontrol signs while using SE2's key-equivalent path.
        lookDown = Math.Max(lookDown, Math.Max(pitch, 0f));
        lookUp = Math.Max(lookUp, Math.Max(-pitch, 0f));
        lookRight = Math.Max(lookRight, Math.Max(yaw, 0f));
        lookLeft = Math.Max(lookLeft, Math.Max(-yaw, 0f));
    }

    private static NativeInputSnapshot CaptureNativeInput(
        float pitchAnalog, float yawAnalog, float lookUp, float lookDown, float lookLeft, float lookRight,
        Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs movementInputs) =>
        new(pitchAnalog, yawAnalog, lookUp, lookDown, lookLeft, lookRight, movementInputs);

    private static void RestoreNativeInput(
        NativeInputSnapshot snapshot,
        ref float pitchAnalog, ref float yawAnalog, ref float lookUp, ref float lookDown, ref float lookLeft, ref float lookRight,
        ref Keen.Game2.Simulation.WorldObjects.Movement.MovementInputs movementInputs)
    {
        pitchAnalog = snapshot.PitchAnalog;
        yawAnalog = snapshot.YawAnalog;
        lookUp = snapshot.LookUp;
        lookDown = snapshot.LookDown;
        lookLeft = snapshot.LookLeft;
        lookRight = snapshot.LookRight;
        movementInputs = snapshot.MovementInputs;
    }


    private static float NormalizeAxis(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

    private static void CommitControlData(
        Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent instance)
    {
        if (_committingControlData) return;
        var method = _updateControlDataMethod;
        if (method is null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol cannot submit cockpit input because SE2's UpdateControlData method was not found.");
            return;
        }

        try
        {
            _committingControlData = true;
            method.Invoke(instance, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol could not submit the cockpit input state to SE2.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 movement commit error: {ex.InnerException}");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol could not submit the cockpit input state to SE2.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 movement commit error: {ex}");
        }
        finally
        {
            _committingControlData = false;
        }
    }

    private static DateTime _lastFinalCommitDebugUtc;
    private static string? _lastFinalCommitDebugSummary;
    private static void LogFinalMovementCommit(float surge, float sway, float heave, float roll)
    {
        string summary = $"forward={surge:F2}; strafe={sway:F2}; lift={heave:F2}; roll={roll:F2}";
        if (summary == _lastFinalCommitDebugSummary || DateTime.UtcNow - _lastFinalCommitDebugUtc < TimeSpan.FromMilliseconds(250)) return;
        _lastFinalCommitDebugSummary = summary;
        _lastFinalCommitDebugUtc = DateTime.UtcNow;
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Committed Kontrol movement to SE2: {summary}.");
    }

    private static void LogReceivedFrame(uint schemaVersion, float pitch, float roll, float yaw, float surge, float sway, float heave, ulong discrete, ulong actions)
    {
        string summary = $"schema={schemaVersion}; pitch={pitch:F2}; roll={roll:F2}; yaw={yaw:F2}; forward={surge:F2}; strafe={sway:F2}; lift={heave:F2}; discrete=0x{discrete:X}; actions=0x{actions:X}";
        // A trigger can be deliberately short-lived. Never throttle away a
        // frame that contains an action or a held-state transition, otherwise
        // diagnostics cannot prove whether the button reached the adapter.
        bool discreteChanged = discrete != _lastDiscreteDebugState;
        if (actions == 0 && !discreteChanged &&
            (summary == _lastFrameDebugSummary || DateTime.UtcNow - _lastFrameDebugUtc < TimeSpan.FromMilliseconds(250))) return;
        _lastFrameDebugSummary = summary;
        _lastDiscreteDebugState = discrete;
        _lastFrameDebugUtc = DateTime.UtcNow;
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Received Kontrol input frame: {summary}.");
    }

    private static void LogAppliedGameState(float pitch, float yaw, float forward, float backward, float right, float left, float up, float down, float rollRight, float rollLeft)
    {
        string summary = $"pitchAnalog={pitch:F2}; yawAnalog={yaw:F2}; movement F/B={forward:F2}/{backward:F2}, R/L={right:F2}/{left:F2}, U/D={up:F2}/{down:F2}, roll R/L={rollRight:F2}/{rollLeft:F2}";
        if (summary == _lastAppliedDebugSummary || DateTime.UtcNow - _lastAppliedDebugUtc < TimeSpan.FromMilliseconds(250)) return;
        _lastAppliedDebugSummary = summary;
        _lastAppliedDebugUtc = DateTime.UtcNow;
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Applied to SE2: {summary}.");
    }
}
