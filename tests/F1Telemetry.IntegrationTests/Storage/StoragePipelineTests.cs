using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Extensions;
using F1Telemetry.Ingestion.Options;
using F1Telemetry.IntegrationTests.Harness;
using F1Telemetry.Storage.Data;
using F1Telemetry.Storage.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace F1Telemetry.IntegrationTests.Storage;

[Trait("Category", "Integration")]
public sealed class StoragePipelineTests : IAsyncLifetime, IAsyncDisposable
{
    private const int _testPort = 20889;

    private IHost _host = null!;
    private UdpGameSimulator _simulator = null!;
    private InMemoryLapCollector _collector = null!;
    private string _archiveDir = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _archiveDir = Path.Combine(Path.GetTempPath(), $"f1arch_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(Path.GetTempPath(), $"f1test_{Guid.NewGuid():N}.db");
        _collector = new InMemoryLapCollector();

        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}",
            ["Storage:ArchivePath"] = _archiveDir,
            ["InfluxDb:Url"] = "http://localhost:8086",
            ["InfluxDb:Token"] = "my-local-token",
            ["InfluxDb:Org"] = "f1telemetry",
            ["InfluxDb:Bucket"] = "telemetry",
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config))
            .ConfigureServices((ctx, services) =>
            {
                services.AddIngestion();
                services.Configure<UdpOptions>(o => o.Port = _testPort);
                services.AddStorage(ctx.Configuration);
            })
            .Build();

        IEventBus eventBus = _host.Services.GetRequiredService<IEventBus>();
        eventBus.Subscribe<LapCompletedEvent>(e => _collector.Add(e.Lap));

        await _host.StartAsync();
        _simulator = new UdpGameSimulator(targetPort: _testPort);
    }

    [Fact]
    public async Task ThreeLapSession_AllLapsSavedToSqlite()
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
        await Task.Delay(500);

        using IServiceScope scope = _host.Services.CreateScope();
        F1TelemetryDbContext db = scope.ServiceProvider.GetRequiredService<F1TelemetryDbContext>();
        List<LapEntity> laps = await db.Laps.ToListAsync();

        laps.Should().HaveCount(3);
    }

    [Fact]
    public async Task SingleLap_WritesJsonArchiveFile()
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
        await Task.Delay(500);

        string sessionDir = Path.Combine(_archiveDir, SessionId.From(replay.SessionUid).Value);
        string[] files = Directory.Exists(sessionDir)
            ? Directory.GetFiles(sessionDir, "*.json")
            : [];

        files.Should().HaveCount(1);
    }

    public async Task DisposeAsync()
    {
        _collector.Dispose();
        await _simulator.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();

        if (Directory.Exists(_archiveDir))
        {
            Directory.Delete(_archiveDir, recursive: true);
        }

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();
}
