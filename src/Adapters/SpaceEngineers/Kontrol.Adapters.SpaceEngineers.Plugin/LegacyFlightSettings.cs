using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Kontrol.Sdk.IPC;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    internal struct FlightSensitivitySettings
    {
        internal const float DefaultPitch = 20f;
        internal const float DefaultYaw = 20f;
        internal const float DefaultRoll = 1f;

        internal float Pitch;
        internal float Yaw;
        internal float Roll;

        internal static FlightSensitivitySettings Default => new FlightSensitivitySettings
        {
            Pitch = DefaultPitch,
            Yaw = DefaultYaw,
            Roll = DefaultRoll
        };
    }

    /// <summary>
    /// Reads the host's generic adapter-settings JSON through the shared Kontrol SDK IPC contract.
    /// </summary>
    internal sealed class LegacyFlightSettingsReader : IDisposable
    {
        private const string SettingsMapName = @"Local\Kontrol_Settings_space-engineers";
        private static readonly Regex NumberProperty = new Regex(
            "\\\"(?<key>flight\\.(?:pitchSensitivity|yawSensitivity|rollSensitivity))\\\"\\s*:\\s*(?<value>-?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][+-]?\\d+)?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly MmfChannel<TelemetryData> _channel = new MmfChannel<TelemetryData>(SettingsMapName);
        private string _lastJson = string.Empty;
        private bool _initialized;

        internal FlightSensitivitySettings Current { get; private set; } = FlightSensitivitySettings.Default;

        internal bool Refresh()
        {
            if (!_initialized)
            {
                PulsarStartupTrace.Write("Settings IPC open begin.");
                _channel.CreateOrOpen();
                _initialized = true;
                PulsarStartupTrace.Write("Settings IPC open succeeded.");
            }

            TelemetryData packet;
            _channel.Read(out packet);
            string json = packet.GetJson();
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json, _lastJson, StringComparison.Ordinal)) return false;

            var next = FlightSensitivitySettings.Default;
            foreach (Match match in NumberProperty.Matches(json))
            {
                float value;
                if (!float.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;

                switch (match.Groups["key"].Value)
                {
                    case "flight.pitchSensitivity": next.Pitch = Clamp(value); break;
                    case "flight.yawSensitivity": next.Yaw = Clamp(value); break;
                    case "flight.rollSensitivity": next.Roll = Clamp(value); break;
                }
            }

            Current = next;
            _lastJson = json;
            PulsarStartupTrace.Write(string.Format(
                CultureInfo.InvariantCulture,
                "Settings IPC applied: pitch={0:0.0}, yaw={1:0.0}, roll={2:0.0}.",
                Current.Pitch, Current.Yaw, Current.Roll));
            return true;
        }

        public void Dispose() => _channel.Dispose();

        private static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return FlightSensitivitySettings.DefaultPitch;
            return Math.Max(0.1f, Math.Min(40f, value));
        }
    }
}
