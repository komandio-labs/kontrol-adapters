using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
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
        private readonly LegacyMmfChannel<InputFrame> _input = new LegacyMmfChannel<InputFrame>(InputMapName);
        private readonly PulsarStatusReporter _status = new PulsarStatusReporter();
        private readonly LegacyAdapterLogReporter _logs = new LegacyAdapterLogReporter();
        private IMyControllableEntity _lastControlled;
        private ulong _previousActions;
        private DateTime _lastInputTraceUtc;

        public void Init(object gameInstance)
        {
            _input.CreateOrOpen();
            _status.ReportLoaded();
            _logs.Write("[LifecycleTrace] Space Engineers controls plugin initialized.");
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

                var movement = new Vector3(frame.ReadAnalog(4), frame.ReadAnalog(5), frame.ReadAnalog(3));
                var rotation = new Vector2(frame.ReadAnalog(0), frame.ReadAnalog(2));
                controlled.MoveAndRotate(movement, rotation, frame.ReadAnalog(1));
                _lastControlled = controlled;
                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Applied pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000}, actions=0x{6:X}.",
                    rotation.Y, rotation.X, frame.ReadAnalog(1), movement.Z, movement.X, movement.Y, frame.TriggeredActions));

                var edges = frame.TriggeredActions & ~_previousActions;
                if ((edges & (1UL << SpaceEngineersControlLayout.DampenersAction)) != 0) { controlled.SwitchDamping(); _logs.Write("[ControlTrace] Dampeners action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LightsAction)) != 0) { controlled.SwitchLights(); _logs.Write("[ControlTrace] Lights action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LandingGearsAction)) != 0) { controlled.SwitchLandingGears(); _logs.Write("[ControlTrace] Landing gear action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.HandbrakeAction)) != 0) { controlled.SwitchHandbrake(); _logs.Write("[ControlTrace] Handbrake action applied."); }
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
