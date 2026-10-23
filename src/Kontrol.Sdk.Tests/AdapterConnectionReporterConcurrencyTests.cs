using System.Text.Json;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.IPC;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Sdk.Tests;

[TestFixture]
public sealed class AdapterConnectionReporterConcurrencyTests
{
    [Test]
    public void ConcurrentStatusReports_PreserveEverySequenceAndWriteValidJson()
    {
        string adapterId = $"reporter-{Guid.NewGuid():N}";
        using var receiver = new MmfChannel<TelemetryData>($"Local\\Kontrol_AdapterStatus_{adapterId}");
        receiver.CreateOrOpen();
        var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var previousFrame = new TelemetryData();
        previousFrame.SetJson(JsonSerializer.Serialize(new AdapterRuntimeStatus(
            9000,
            AdapterRuntimeState.Active,
            Environment.ProcessId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            null,
            null,
            null), serializerOptions));
        receiver.Write(ref previousFrame);
        using var reporter = new AdapterConnectionReporter(adapterId);

        reporter.ReportLoaded();
        Parallel.For(0, 200, _ =>
        {
            reporter.ReportError("test", "test error");
            reporter.ClearError();
        });

        receiver.Read(out var frame);
        using JsonDocument status = JsonDocument.Parse(frame.GetJson());
        status.RootElement.GetProperty("sequence").GetInt64().ShouldBe(9401);
    }
}
