# Testing Strategy

## Goals

- **70% unit test coverage** across all projects (enforced by Coverlet threshold in CI)
- **Integration tests** that simulate real F1 game UDP feeds without requiring a PS5 or a running game
- Tests are fast by default — integration tests that need Docker are tagged and excluded from the default `dotnet test` run
- Every sprint ships its own tests; coverage never decreases between sprints

---

## Coverage Targets by Layer

| Project | Target | Priority areas |
|---|---|---|
| `F1Telemetry.Contracts` | N/A (types only) | — |
| `F1Telemetry.Ingestion` | ≥ 75% | Packet parser (100%), lap detector, channel pipeline |
| `F1Telemetry.Storage` | ≥ 70% | Repository CRUD, query correctness, file archive |
| `F1Telemetry.Agents` | ≥ 75% | Each agent's analysis logic, orchestrator fan-out, LLM client selection |
| `F1Telemetry.App` | ≥ 60% | SignalR hub events, REST endpoint shape, Blazor state service |

Coverage is measured with Coverlet and reported in CI. The build fails if any project drops below its threshold.

```xml
<!-- Add to each test project -->
<ItemGroup>
  <PackageReference Include="coverlet.collector" Version="6.*" />
</ItemGroup>
```

```bash
dotnet test --collect:"XPlat Code Coverage" \
            /p:Threshold=70 \
            /p:ThresholdType=line \
            /p:ThresholdStat=average
```

---

## Test Project Structure

```
tests/
├── F1Telemetry.UnitTests/
│   ├── Ingestion/
│   │   ├── PacketParserTests.cs
│   │   ├── SessionManagerTests.cs
│   │   └── UdpListenerServiceTests.cs
│   ├── Storage/
│   │   ├── SqliteLapRepositoryTests.cs
│   │   └── FileLapArchiveTests.cs
│   ├── Agents/
│   │   ├── DeltaAgentTests.cs
│   │   ├── BrakingAgentTests.cs
│   │   ├── CornerAgentTests.cs
│   │   ├── TyreAgentTests.cs
│   │   ├── RacingLineAgentTests.cs
│   │   └── AgentOrchestratorTests.cs
│   └── App/
│       ├── TelemetryStateTests.cs
│       └── CoachingReportSortTests.cs
│
├── F1Telemetry.IntegrationTests/
│   ├── Harness/
│   │   ├── UdpGameSimulator.cs          ← core of this document
│   │   ├── SessionReplayBuilder.cs
│   │   └── TelemetryFixtures.cs
│   ├── Pipeline/
│   │   ├── UdpIngestionPipelineTests.cs
│   │   └── StoragePipelineTests.cs
│   ├── Agents/
│   │   └── FullAgentPipelineTests.cs
│   └── Api/
│       ├── TelemetryHubTests.cs
│       └── SessionApiTests.cs
```

---

## Unit Testing

### Tools

- **xUnit** — test framework
- **NSubstitute** — mocking (preferred over Moq for concise syntax)
- **FluentAssertions** — assertion DSL
- **AutoFixture** — test data generation for complex domain objects

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  <PackageReference Include="NSubstitute" Version="5.*" />
  <PackageReference Include="FluentAssertions" Version="6.*" />
  <PackageReference Include="AutoFixture" Version="4.*" />
  <PackageReference Include="AutoFixture.AutoNSubstitute" Version="4.*" />
</ItemGroup>
```

### Pattern: Arrange-Act-Assert with one assertion concept per test

```csharp
public class PacketParserTests
{
    [Fact]
    public void Parse_CarTelemetryPacket_ReturnsCorrectSpeed()
    {
        // Arrange
        byte[] rawPacket = TelemetryFixtures.CarTelemetryPacket(speedKmh: 287.5f);
        var parser = new PacketParser();

        // Act
        var result = parser.Parse(rawPacket);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<PacketCarTelemetryData>()
              .Which.SpeedKmh.Should().BeApproximately(287.5f, precision: 0.1f);
    }

    [Fact]
    public void Parse_UnknownPacketId_ReturnsFail()
    {
        byte[] rawPacket = TelemetryFixtures.PacketWithId(packetId: 99);
        var parser = new PacketParser();

        var result = parser.Parse(rawPacket);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unknown packet ID");
    }
}
```

### Pattern: Theory for input variation

```csharp
[Theory]
[InlineData(0, 0)]     // start of session — no lap boundary
[InlineData(1, 1)]     // first lap completed
[InlineData(1, 2)]     // second lap boundary fires event
[InlineData(5, 6)]     // mid-session boundary
public void LapBoundaryDetected_WhenLapNumberIncreases(int previous, int current)
{
    var eventBus = Substitute.For<IEventBus>();
    var manager = new SessionManager(eventBus);

    manager.ProcessLapData(new PacketLapData { CurrentLapNum = (byte)previous });
    manager.ProcessLapData(new PacketLapData { CurrentLapNum = (byte)current });

    bool shouldFire = current > previous && previous > 0;
    if (shouldFire)
        eventBus.Received(1).Publish(Arg.Any<LapCompletedEvent>());
    else
        eventBus.DidNotReceive().Publish(Arg.Any<LapCompletedEvent>());
}
```

### Pattern: Agent testing with mock LLM

All agents are tested with a mocked `ILlmClient`. This isolates the agent's prompt construction and response parsing logic from any real LLM call.

```csharp
public class DeltaAgentTests
{
    private readonly ILapRepository _lapRepo = Substitute.For<ILapRepository>();
    private readonly ITelemetryRepository _telemetryRepo = Substitute.For<ITelemetryRepository>();
    private readonly ILlmClient _llmClient = Substitute.For<ILlmClient>();
    private readonly DeltaAgent _agent;

    public DeltaAgentTests()
    {
        _agent = new DeltaAgent(_lapRepo, _telemetryRepo, _llmClient,
                                NullLogger<DeltaAgent>.Instance);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPersonalBestExists_IncludesDeltaInPrompt()
    {
        // Arrange
        var currentLap = LapFixtures.CreateLap(lapTimeMs: 85_000);
        var bestLap = LapFixtures.CreateLap(lapTimeMs: 84_200);
        _lapRepo.GetPersonalBestAsync(currentLap.SessionId, Arg.Any<CancellationToken>())
               .Returns(bestLap);
        _llmClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(LlmFixtures.DeltaAgentResponse());

        // Act
        var finding = await _agent.AnalyzeAsync(new LapAnalysisContext(currentLap), CancellationToken.None);

        // Assert
        await _llmClient.Received(1).CompleteAsync(
            Arg.Any<string>(),
            Arg.Is<string>(p => p.Contains("800") && p.Contains("ms behind")),
            Arg.Any<CancellationToken>());
        finding.Category.Should().Be(AnalysisCategory.Delta);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenNoBestLap_ReturnsBaselineMessage()
    {
        _lapRepo.GetPersonalBestAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>())
               .Returns((CompletedLap?)null);

        var finding = await _agent.AnalyzeAsync(new LapAnalysisContext(LapFixtures.CreateLap()),
                                                CancellationToken.None);

        finding.Explanation.Should().Contain("first timed lap");
        await _llmClient.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
```

### Pattern: SQLite in-memory for repository tests

EF Core supports `UseInMemoryDatabase` but SQLite in-memory mode is preferred — it respects foreign key constraints and produces behavior closer to production.

```csharp
public class SqliteLapRepositoryTests : IAsyncLifetime
{
    private F1TelemetryDbContext _context = null!;
    private SqliteLapRepository _repository = null!;

    public async Task InitializeAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<F1TelemetryDbContext>()
            .UseSqlite(connection)
            .Options;

        _context = new F1TelemetryDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new SqliteLapRepository(_context);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task SaveLapAsync_PersistsAllFields()
    {
        var lap = LapFixtures.CreateLap(lapTimeMs: 85_123, sector1Ms: 28_400);

        await _repository.SaveLapAsync(lap, CancellationToken.None);

        var saved = await _repository.GetLapAsync(lap.Id, CancellationToken.None);
        saved.Should().NotBeNull();
        saved!.LapTimeMs.Should().Be(85_123);
        saved.Sector1Ms.Should().Be(28_400);
    }

    [Fact]
    public async Task GetPersonalBestAsync_ReturnsShortestValidLap()
    {
        await _repository.SaveLapAsync(LapFixtures.CreateLap(lapTimeMs: 86_000, isValid: true), CancellationToken.None);
        await _repository.SaveLapAsync(LapFixtures.CreateLap(lapTimeMs: 84_500, isValid: true), CancellationToken.None);
        await _repository.SaveLapAsync(LapFixtures.CreateLap(lapTimeMs: 82_000, isValid: false), CancellationToken.None);

        var best = await _repository.GetPersonalBestAsync(new SessionId("TEST"), CancellationToken.None);

        best!.LapTimeMs.Should().Be(84_500); // invalid lap is excluded
    }
}
```

---

## Integration Testing

### The UDP Game Simulator

The `UdpGameSimulator` is the central component of the integration test suite. It replaces the PS5 and the F1 game: it constructs valid UDP datagrams that match the F1 wire protocol and broadcasts them to a configurable port. Tests can use it to simulate any scenario — a clean flying lap, a lap with tyre degradation, a session abort, or a connection drop.

```csharp
/// <summary>
/// Simulates the PS5 F1 game's UDP telemetry broadcast for integration testing.
/// Sends correctly structured packets to a local UDP socket at a configurable rate.
/// </summary>
public sealed class UdpGameSimulator : IAsyncDisposable
{
    private readonly UdpClient _client;
    private readonly IPEndPoint _target;
    private readonly ILogger<UdpGameSimulator> _logger;

    public UdpGameSimulator(int targetPort = 20777,
                            ILogger<UdpGameSimulator>? logger = null)
    {
        _client = new UdpClient();
        _target = new IPEndPoint(IPAddress.Loopback, targetPort);
        _logger = logger ?? NullLogger<UdpGameSimulator>.Instance;
    }

    /// <summary>
    /// Broadcasts a full lap worth of telemetry frames at the given rate.
    /// </summary>
    public async Task SendLapAsync(
        SessionReplay replay,
        int lapNumber,
        float intervalMs = 16.67f,   // ~60Hz
        CancellationToken ct = default)
    {
        var frames = replay.GetLapFrames(lapNumber);
        _logger.LogInformation("Simulator: sending {Count} frames for lap {Lap}", frames.Count, lapNumber);

        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            await SendPacketAsync(frame.ToCarTelemetryPacket());
            await SendPacketAsync(frame.ToLapDataPacket());
            await Task.Delay(TimeSpan.FromMilliseconds(intervalMs), ct);
        }

        // Send the lap-completed frame (lap number increments)
        await SendPacketAsync(LapDataPacketBuilder.LapCompleted(lapNumber));
    }

    /// <summary>
    /// Sends a single packet immediately. Use for targeted scenario tests.
    /// </summary>
    public async Task SendPacketAsync<T>(T packet) where T : struct
    {
        byte[] bytes = PacketSerializer.Serialize(packet);
        await _client.SendAsync(bytes, _target);
    }

    /// <summary>
    /// Sends packets at maximum rate with no delay — use for throughput/drop tests.
    /// </summary>
    public async Task FloodAsync(int packetCount, CancellationToken ct = default)
    {
        var packet = TelemetryFixtures.CarTelemetryPacket(speedKmh: 200f);
        for (int i = 0; i < packetCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _client.SendAsync(packet, _target);
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

### SessionReplayBuilder

Constructs a `SessionReplay` from either a fixture definition or an archived JSON file from a real session. This lets tests replay exact real-world scenarios.

```csharp
public static class SessionReplayBuilder
{
    /// <summary>
    /// Creates a replay from a lap archive JSON file (produced by FileLapArchive).
    /// Use this to test specific real laps captured from the PS5.
    /// </summary>
    public static SessionReplay FromArchive(string archivePath)
    {
        var json = File.ReadAllText(archivePath);
        return JsonSerializer.Deserialize<SessionReplay>(json)!;
    }

    /// <summary>
    /// Builds a synthetic session for parametric testing.
    /// </summary>
    public static SessionReplay Build(Action<SessionReplayOptions> configure)
    {
        var opts = new SessionReplayOptions();
        configure(opts);
        return new SyntheticSessionReplay(opts);
    }
}

public class SessionReplayOptions
{
    public string TrackName { get; set; } = "Monza";
    public int LapCount { get; set; } = 3;
    public float BaseLapTimeMs { get; set; } = 85_000f;
    public float LapTimeVarianceMs { get; set; } = 500f;
    public float MaxSpeedKmh { get; set; } = 340f;
    public bool IncludeTyreDegradation { get; set; } = true;
    public bool IncludePitLap { get; set; } = false;
    public bool IncludeInvalidLap { get; set; } = false;
}
```

### Integration Test: UDP Ingestion Pipeline

```csharp
[Trait("Category", "Integration")]
public class UdpIngestionPipelineTests : IAsyncLifetime
{
    private IHost _host = null!;
    private UdpGameSimulator _simulator = null!;
    private const int TestPort = 20888; // different from production to avoid conflicts

    public async Task InitializeAsync()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIngestion();
                services.Configure<UdpOptions>(o => o.Port = TestPort);
                // Replace real storage with an in-memory collector
                services.AddSingleton<ILapCompletedCollector, InMemoryLapCollector>();
                services.AddSingleton<IEventBus>(sp =>
                    new CollectingEventBus(sp.GetRequiredService<ILapCompletedCollector>()));
            })
            .Build();

        await _host.StartAsync();
        _simulator = new UdpGameSimulator(targetPort: TestPort);
    }

    [Fact]
    public async Task SimulatedThreeLapSession_DetectsAllLapBoundaries()
    {
        var replay = SessionReplayBuilder.Build(o =>
        {
            o.LapCount = 3;
            o.BaseLapTimeMs = 85_000f;
        });

        var collector = _host.Services.GetRequiredService<ILapCompletedCollector>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        for (int lap = 1; lap <= 3; lap++)
            await _simulator.SendLapAsync(replay, lap, intervalMs: 1f, ct: cts.Token); // fast — no real-time delay

        await collector.WaitForCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(5));
        collector.ReceivedLaps.Should().HaveCount(3);
        collector.ReceivedLaps.Select(l => l.LapNumber).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task InvalidLapPackets_AreDiscardedGracefully()
    {
        byte[] garbage = new byte[24]; // valid header length but zeroed body
        await _simulator.SendPacketAsync(garbage);
        await Task.Delay(200);

        // Host should still be running — no crash
        _host.Services.GetRequiredService<IHostApplicationLifetime>()
             .ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task HighFrequencyFlood_DoesNotExhaustMemory()
    {
        var before = GC.GetTotalMemory(forceFullCollection: true);
        await _simulator.FloodAsync(packetCount: 10_000);
        await Task.Delay(500);
        var after = GC.GetTotalMemory(forceFullCollection: true);

        long growthBytes = after - before;
        growthBytes.Should().BeLessThan(10 * 1024 * 1024); // < 10 MB growth for 10k packets
    }

    public async Task DisposeAsync()
    {
        await _simulator.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }
}
```

### Integration Test: Storage Pipeline

```csharp
[Trait("Category", "Integration")]
public class StoragePipelineTests : IAsyncLifetime
{
    private IHost _host = null!;
    private UdpGameSimulator _simulator = null!;
    private SqliteConnection _connection = null!;
    private const int TestPort = 20889;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIngestion();
                services.AddStorage();
                services.Configure<UdpOptions>(o => o.Port = TestPort);
                // Inject the shared in-memory connection
                services.AddDbContext<F1TelemetryDbContext>(o => o.UseSqlite(_connection));
                // Use a temp directory for the file archive
                services.Configure<StorageOptions>(o =>
                    o.ArchivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            })
            .Build();

        var db = _host.Services.GetRequiredService<F1TelemetryDbContext>();
        await db.Database.EnsureCreatedAsync();
        await _host.StartAsync();

        _simulator = new UdpGameSimulator(targetPort: TestPort);
    }

    [Fact]
    public async Task CompletedLap_IsPersisted_InBothStores()
    {
        var replay = SessionReplayBuilder.Build(o => { o.LapCount = 1; o.BaseLapTimeMs = 84_000f; });
        await _simulator.SendLapAsync(replay, lapNumber: 1, intervalMs: 1f);
        await Task.Delay(1000); // allow async writes to complete

        // Assert SQLite
        var lapRepo = _host.Services.GetRequiredService<ILapRepository>();
        var laps = await lapRepo.GetLapsBySessionAsync(replay.SessionId, CancellationToken.None);
        laps.Should().HaveCount(1);
        laps[0].LapTimeMs.Should().BeGreaterThan(0);

        // Assert file archive
        var archive = _host.Services.GetRequiredService<ILapArchive>();
        var archivedLap = await archive.GetArchivedLapAsync(replay.SessionId, lapNumber: 1, CancellationToken.None);
        archivedLap.Should().NotBeNull();
    }

    public async Task DisposeAsync()
    {
        await _simulator.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
        await _connection.DisposeAsync();
    }
}
```

### Integration Test: SignalR Hub

```csharp
[Trait("Category", "Integration")]
public class TelemetryHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TelemetryHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILlmClient, StubLlmClient>(); // no real LLM needed
            });
        });
    }

    [Fact]
    public async Task CoachingReportReady_IsPushed_AfterLapCompleted()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(_factory.Server.BaseAddress + "hubs/telemetry",
                     o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        CoachingReportDto? received = null;
        hubConnection.On<CoachingReportDto>("CoachingReportReady", dto => received = dto);
        await hubConnection.StartAsync();

        // Simulate a lap completed event via the test API endpoint
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/test/simulate-lap", new { LapNumber = 1 });

        // Wait for hub push
        await Task.Delay(5000); // allow agent processing time

        received.Should().NotBeNull();
        received!.Findings.Should().NotBeEmpty();
        received.Findings.Should().BeInDescendingOrder(f => f.EstimatedGainMs);

        await hubConnection.StopAsync();
    }
}
```

---

## Test Fixtures and Helpers

### TelemetryFixtures

```csharp
public static class TelemetryFixtures
{
    public static byte[] CarTelemetryPacket(float speedKmh = 200f, float throttle = 1f, float brake = 0f)
    {
        var header = new PacketHeader
        {
            PacketFormat = 2024,
            PacketId = (byte)PacketId.CarTelemetry,
            SessionUID = 0xDEADBEEFCAFEBABE
        };

        var data = new PacketCarTelemetryData
        {
            SpeedKmh = speedKmh,
            Throttle = throttle,
            Brake = brake,
            Gear = 7,
            EngineRPM = 12500
        };

        return PacketSerializer.Combine(header, data);
    }

    public static byte[] PacketWithId(byte packetId)
    {
        var header = new PacketHeader { PacketId = packetId };
        return PacketSerializer.Serialize(header);
    }
}
```

### LapFixtures

```csharp
public static class LapFixtures
{
    public static CompletedLap CreateLap(
        int lapTimeMs = 85_000,
        int sector1Ms = 28_000,
        int sector2Ms = 30_000,
        int sector3Ms = 27_000,
        bool isValid = true,
        int lapNumber = 1)
    {
        return new CompletedLap(
            SessionId: SessionId.From(0xDEADBEEF00000001),
            LapNumber: lapNumber,
            LapTime: TimeSpan.FromMilliseconds(lapTimeMs),
            Sector1: TimeSpan.FromMilliseconds(sector1Ms),
            Sector2: TimeSpan.FromMilliseconds(sector2Ms),
            Sector3: TimeSpan.FromMilliseconds(sector3Ms),
            IsValid: isValid,
            Frames: []
        );
    }
}
```

### StubLlmClient

A deterministic LLM stub for integration tests that avoids any real API calls:

```csharp
public sealed class StubLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var response = new AgentFindingDto
        {
            Category = "Delta",
            Explanation = "You lost 0.31s in sector 2 compared to your best lap. Focus on the T4 braking zone.",
            EstimatedGainMs = 310
        };
        return Task.FromResult(JsonSerializer.Serialize(response));
    }
}
```

---

## CI Pipeline Configuration

```yaml
# .github/workflows/ci.yml
name: CI

on: [push, pull_request]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
        run: |
          dotnet test --no-build --configuration Release \
            --filter "Category!=Integration" \
            --collect:"XPlat Code Coverage" \
            /p:Threshold=70 /p:ThresholdType=line

      - name: Upload coverage
        uses: codecov/codecov-action@v4

  integration-tests:
    runs-on: ubuntu-latest
    services:
      influxdb:
        image: influxdb:2.7
        ports: ["8086:8086"]
        env:
          DOCKER_INFLUXDB_INIT_MODE: setup
          DOCKER_INFLUXDB_INIT_USERNAME: f1admin
          DOCKER_INFLUXDB_INIT_PASSWORD: f1password
          DOCKER_INFLUXDB_INIT_ORG: f1telemetry
          DOCKER_INFLUXDB_INIT_BUCKET: telemetry
          DOCKER_INFLUXDB_INIT_ADMIN_TOKEN: ci-test-token
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build --no-restore --configuration Release
      - name: Integration tests
        run: |
          dotnet test --no-build --configuration Release \
            --filter "Category=Integration"
        env:
          InfluxDb__Token: ci-test-token
```

---

## Running Tests Locally

```bash
# All unit tests (fast, no Docker needed)
dotnet test --filter "Category!=Integration"

# All integration tests (requires Docker with InfluxDB running)
docker compose up -d influxdb
dotnet test --filter "Category=Integration"

# All tests with coverage report
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
start coverage-report/index.html

# Run the game simulator standalone for manual inspection
dotnet run --project tests/F1Telemetry.IntegrationTests -- simulate --laps 5 --port 20777
```
