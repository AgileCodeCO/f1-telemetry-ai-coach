using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Extensions;
using F1Telemetry.Ingestion.Options;
using F1Telemetry.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace F1Telemetry.IntegrationTests.Pipeline;

[Trait("Category", "Integration")]
public sealed class UdpIngestionPipelineTests : IAsyncLifetime, IAsyncDisposable
{
    private const int _testPort = 20888;

    private IHost _host = null!;
    private UdpGameSimulator _simulator = null!;
    private InMemoryLapCollector _collector = null!;

    public async Task InitializeAsync()
    {
        _collector = new InMemoryLapCollector();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIngestion();
                services.Configure<UdpOptions>(o => o.Port = _testPort);
            })
            .Build();

        // Subscribe before starting the host so no event is missed
        IEventBus eventBus = _host.Services.GetRequiredService<IEventBus>();
        eventBus.Subscribe<LapCompletedEvent>(e => _collector.Add(e.Lap));

        await _host.StartAsync();
        _simulator = new UdpGameSimulator(targetPort: _testPort);
    }

    [Fact]
    public async Task ThreeLapSession_DetectsAllLapBoundaries()
    {
        SessionReplay replay = SessionReplayBuilder.Build(o =>
        {
            o.LapCount = 3;
            o.BaseLapTimeMs = 85_000;
            o.LapTimeVarianceMs = 0;
        });

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        for (int lap = 1; lap <= 3; lap++)
        {
            await _simulator.SendLapAsync(replay, lap, intervalMs: 0, ct: cts.Token);
        }

        await _collector.WaitForCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(5));

        _collector.ReceivedLaps.Should().HaveCount(3);
        _collector.ReceivedLaps.Select(l => l.LapNumber).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task LapTime_IsCarriedFromBoundaryPacket()
    {
        SessionReplay replay = SessionReplayBuilder.Build(o =>
        {
            o.LapCount = 1;
            o.BaseLapTimeMs = 84_000;
            o.LapTimeVarianceMs = 0;
        });

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await _simulator.SendLapAsync(replay, lapNumber: 1, intervalMs: 0, ct: cts.Token);

        await _collector.WaitForCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(5));

        CompletedLap lap = _collector.ReceivedLaps[0];
        lap.LapTime.TotalMilliseconds.Should().BeApproximately(84_000, precision: 10);
    }

    [Fact]
    public async Task GarbagePacket_IsDiscardedWithoutCrash()
    {
        byte[] garbage = new byte[24]; // below RawPacketHeader.Size — parser rejects it
        await _simulator.SendPacketAsync(garbage);
        await Task.Delay(200);

        IHostApplicationLifetime lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task HighFrequencyFlood_DoesNotExhaustMemory()
    {
        long before = GC.GetTotalMemory(forceFullCollection: true);

        await _simulator.FloodAsync(packetCount: 5_000);
        await Task.Delay(500);

        long after = GC.GetTotalMemory(forceFullCollection: true);
        (after - before).Should().BeLessThan(10 * 1024 * 1024); // < 10 MB growth
    }

    public async Task DisposeAsync()
    {
        await _simulator.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();
}
