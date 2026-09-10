using System;
using System.Diagnostics;
using System.IO;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    /// <summary>
    /// Startup-only diagnostic sink independent of Kontrol's shared-memory channels.
    /// It makes a Pulsar initialization stall observable when opening a channel itself fails.
    /// </summary>
    internal static class PulsarStartupTrace
    {
        private static readonly string TracePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kontrol", "adapters", "space-engineers", "logs", "pulsar-startup-trace.log");

        internal static void Write(string message)
        {
            try
            {
                var directory = Path.GetDirectoryName(TracePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    TracePath,
                    DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) +
                    " [" + Process.GetCurrentProcess().Id + "] " + message + Environment.NewLine);
            }
            catch
            {
                // Startup diagnostics must never affect the game or Pulsar loader.
            }
        }
    }
}
