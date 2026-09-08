#if DEBUG
using System.Diagnostics;
using System.Threading;

namespace Kontrol.Adapters.SpaceEngineers2;

/// <summary>
/// Debug-only, allocation-free-on-the-hot-path timing for game-thread adapter work.
/// It emits slow-operation traces immediately and compact summaries every five seconds.
/// </summary>
internal static class DebugAdapterPerformanceTrace
{
    private const double SlowOperationMilliseconds = 2d;
    private const int SummaryIntervalSeconds = 5;
    private static readonly string[] OperationNames =
    [
        "settings IPC read/apply",
        "control-frame IPC read",
        "UpdateControlData prefix",
        "UpdateRotationData prefix",
        "ComputeReticlePositioning prefix",
        "triggered-action dispatch"
    ];
    private static readonly long[] Counts = new long[OperationNames.Length];
    private static readonly long[] TotalTicks = new long[OperationNames.Length];
    private static readonly long[] MaxTicks = new long[OperationNames.Length];
    private static readonly long[] SlowCounts = new long[OperationNames.Length];
    private static long _nextSummaryTimestamp = Stopwatch.GetTimestamp() + Stopwatch.Frequency * SummaryIntervalSeconds;

    internal static long Start() => SpaceEngineers2DebugTraces.IsEnabled(SpaceEngineers2DebugTraceKeys.Performance)
        ? Stopwatch.GetTimestamp()
        : 0;

    internal static void Record(int operation, long startedAt)
    {
        if (startedAt == 0) return;

        long elapsed = Stopwatch.GetTimestamp() - startedAt;
        Interlocked.Increment(ref Counts[operation]);
        Interlocked.Add(ref TotalTicks[operation], elapsed);
        UpdateMaximum(operation, elapsed);

        double elapsedMilliseconds = ToMilliseconds(elapsed);
        if (elapsedMilliseconds >= SlowOperationMilliseconds)
        {
            Interlocked.Increment(ref SlowCounts[operation]);
            SpaceEngineers2AdapterDiagnostics.WriteDebug(
                $"[AdapterPerf] Slow {OperationNames[operation]}; durationMs={elapsedMilliseconds:F2}.");
        }

        TryWriteSummary();
    }

    private static void UpdateMaximum(int operation, long elapsed)
    {
        long current;
        while (elapsed > (current = Volatile.Read(ref MaxTicks[operation])) &&
               Interlocked.CompareExchange(ref MaxTicks[operation], elapsed, current) != current) { }
    }

    private static void TryWriteSummary()
    {
        long now = Stopwatch.GetTimestamp();
        long due = Volatile.Read(ref _nextSummaryTimestamp);
        if (now < due || Interlocked.CompareExchange(ref _nextSummaryTimestamp,
                now + Stopwatch.Frequency * SummaryIntervalSeconds, due) != due)
        {
            return;
        }

        for (var operation = 0; operation < OperationNames.Length; operation++)
        {
            long count = Interlocked.Exchange(ref Counts[operation], 0);
            long total = Interlocked.Exchange(ref TotalTicks[operation], 0);
            long maximum = Interlocked.Exchange(ref MaxTicks[operation], 0);
            long slow = Interlocked.Exchange(ref SlowCounts[operation], 0);
            if (count == 0) continue;
            SpaceEngineers2AdapterDiagnostics.WriteDebug(
                $"[AdapterPerf] {OperationNames[operation]} summary; samples={count}; averageMs={ToMilliseconds(total) / count:F3}; maxMs={ToMilliseconds(maximum):F3}; slowSamples={slow}.");
        }
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
}
#endif
