using System;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using MyShipController = Sandbox.Game.Entities.MyShipController;
using SpaceEngineersControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    public sealed class SpaceEngineersPlugin : VRage.Plugins.IPlugin, IDisposable
    {
        private const uint SchemaVersion = 1;
        private const string InputMapName = @"Local\Kontrol_Input_space-engineers";
        private const string GamePlayScreenTypeName = "Sandbox.Game.Gui.MyGuiScreenGamePlay";
        private static readonly FieldInfo GamePlayStaticField = AccessTools.Field(GamePlayScreenTypeName + ":Static");
        private static readonly MethodInfo SwitchCameraMethod = AccessTools.Method(GamePlayScreenTypeName + ":SwitchCamera");
        private readonly LegacyMmfChannel<InputFrame> _input = new LegacyMmfChannel<InputFrame>(InputMapName);
        private readonly PulsarStatusReporter _status = new PulsarStatusReporter();
        private readonly LegacyAdapterLogReporter _logs = new LegacyAdapterLogReporter();
        private IMyControllableEntity _lastControlled;
        private ulong _previousActions;
        private DateTime _lastInputTraceUtc;

        public void Init(object gameInstance)
        {
            try
            {
                _input.CreateOrOpen();
                _status.ReportLoaded();
                ShipControlCommitHook.Install(this);
                _logs.Write("[LifecycleTrace] Kontrol Joystick / HOTAS / HOSAS ship-control hook initialized.");
                _status.Pulse();
            }
            catch (Exception exception)
            {
                _logs.WriteError("[LifecycleTrace] Could not install the final ship-control hook: " + exception);
                _status.ReportError(
                    "Space Engineers control hook unavailable",
                    "Kontrol could not attach to Space Engineers' final ship-control update for this game build.",
                    "Update the Kontrol Space Engineers plugin, then restart Pulsar Legacy and the game.");
            }
        }

        public void Update()
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                var controlled = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.SchemaVersion != SchemaVersion)
                {
                    _logs.WriteWarning("[InputTrace] Ignoring frame with unsupported schema " + frame.SchemaVersion + ".");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }
                if (frame.IsInputEnabled == 0)
                {
                    TraceInput("[InputTrace] Input is disabled by Kontrol.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }
                if (controlled == null)
                {
                    TraceInput("[InputTrace] No controlled entity is available.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }
                if (!controlled.ControllerInfo.IsLocallyHumanControlled())
                {
                    TraceInput("[InputTrace] Controlled entity is not locally human-controlled.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }

                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Queued pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000}, actions=0x{6:X} for the final ship-control commit.",
                    frame.ReadAnalog(0), frame.ReadAnalog(1), frame.ReadAnalog(2), frame.ReadAnalog(3), frame.ReadAnalog(4), frame.ReadAnalog(5), frame.TriggeredActions));

                var edges = frame.TriggeredActions & ~_previousActions;
                if ((edges & (1UL << SpaceEngineersControlLayout.DampenersAction)) != 0) { controlled.SwitchDamping(); _logs.Write("[ControlTrace] Dampeners action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LightsAction)) != 0) { controlled.SwitchLights(); _logs.Write("[ControlTrace] Lights action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LandingGearsAction)) != 0) { controlled.SwitchLandingGears(); _logs.Write("[ControlTrace] Landing gear action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.HandbrakeAction)) != 0) { controlled.SwitchHandbrake(); _logs.Write("[ControlTrace] Handbrake action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.CameraModeSwitchAction)) != 0) SwitchCameraMode();
                ApplyToolbarActions(controlled, edges);
                if ((edges & (1UL << SpaceEngineersControlLayout.LeaveControlAction)) != 0) { controlled.Use(); _logs.Write("[ControlTrace] Leave vehicle / cockpit action applied."); }
                _previousActions = frame.TriggeredActions;
                _status.Pulse();
            }
            catch (Exception exception)
            {
                StopLastControlled();
                _logs.WriteError("[LifecycleTrace] Space Engineers plugin error: " + exception);
                _status.ReportError("Space Engineers adapter error", exception.Message, "Check Pulsar Legacy's info.log and restart the game.");
            }
        }

        public void Dispose()
        {
            StopLastControlled();
            ShipControlCommitHook.Uninstall(this);
            _input.Dispose();
            _status.Dispose();
            _logs.Dispose();
        }

        private void TraceInput(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastInputTraceUtc < TimeSpan.FromMilliseconds(250))
                return;
            _lastInputTraceUtc = now;
            _logs.WriteDebug(message);
        }

        private void SwitchCameraMode()
        {
            try
            {
                var gamePlay = GamePlayStaticField == null ? null : GamePlayStaticField.GetValue(null);
                if (gamePlay == null || SwitchCameraMethod == null)
                {
                    _logs.WriteWarning("[ControlTrace] Camera Mode Switch received before the SE1 gameplay screen was ready.");
                    return;
                }

                SwitchCameraMethod.Invoke(gamePlay, null);
                _logs.Write("[ControlTrace] Camera Mode Switch action applied.");
            }
            catch (Exception exception)
            {
                _logs.WriteError("[ControlTrace] Camera Mode Switch failed: " + exception);
            }
        }

        private void ApplyToolbarActions(IMyControllableEntity controlled, ulong edges)
        {
            var toolbarOwner = controlled as SpaceEngineersControllableEntity;
            if (toolbarOwner == null || toolbarOwner.Toolbar == null) return;

            for (int action = SpaceEngineersControlLayout.ToolbarFirstAction;
                 action < SpaceEngineersControlLayout.ToolbarFirstAction + SpaceEngineersControlLayout.ToolbarActionCount;
                 action++)
            {
                if ((edges & (1UL << action)) == 0) continue;

                int slot = action - SpaceEngineersControlLayout.ToolbarFirstAction;
                try
                {
                    toolbarOwner.Toolbar.ActivateItemAtSlot(slot);
                    _logs.Write("[ControlTrace] Toolbar slot " + (slot + 1) + " action applied.");
                }
                catch (Exception exception)
                {
                    _logs.WriteError("[ControlTrace] Toolbar slot " + (slot + 1) + " failed: " + exception);
                }
            }
        }

        internal void ApplyAtFinalControlCommit(MyShipController controlled)
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                var current = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.SchemaVersion != SchemaVersion || frame.IsInputEnabled == 0 ||
                    !ReferenceEquals(current, controlled) || !controlled.ControllerInfo.IsLocallyHumanControlled())
                    return;

                var movement = new Vector3(frame.ReadAnalog(4), frame.ReadAnalog(5), frame.ReadAnalog(3));
                var rotation = new Vector2(frame.ReadAnalog(0) * 20f, frame.ReadAnalog(2) * 20f);
                controlled.MoveAndRotate(movement, rotation, frame.ReadAnalog(1));
                _lastControlled = controlled;
                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Committed pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000} at SE1 final ship-control commit.",
                    frame.ReadAnalog(0), frame.ReadAnalog(1), frame.ReadAnalog(2), frame.ReadAnalog(3), frame.ReadAnalog(4), frame.ReadAnalog(5)));
            }
            catch (Exception exception)
            {
                _logs.WriteError("[LifecycleTrace] Final ship-control hook error: " + exception);
                _status.ReportError(
                    "Space Engineers control hook error",
                    exception.Message,
                    "Check Pulsar Legacy's info.log and restart the game.");
            }
        }

        private void StopLastControlled()
        {
            if (_lastControlled != null) _lastControlled.MoveAndRotateStopped();
            _lastControlled = null;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct InputFrame
        {
            public const int MaxAnalogInputs = 32;
            public uint SchemaVersion;
            public byte IsInputEnabled;
            public fixed float AnalogValues[MaxAnalogInputs];
            public ulong DiscreteStates;
            public ulong TriggeredActions;

            public float ReadAnalog(int index)
            {
                if (index < 0 || index >= MaxAnalogInputs) return 0f;
                fixed (float* values = AnalogValues) return values[index];
            }
        }
    }
}
