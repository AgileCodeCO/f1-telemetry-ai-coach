using F1Telemetry.Contracts;
using F1Telemetry.Storage.Data;
using F1Telemetry.Storage.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace F1Telemetry.UnitTests.Storage;

public sealed class SqliteLapRepositoryTests : IAsyncLifetime, IAsyncDisposable
{
    private F1TelemetryDbContext _db = null!;
    private SqliteLapRepository _repository = null!;

    public async Task InitializeAsync()
    {
        DbContextOptions<F1TelemetryDbContext> opts = new DbContextOptionsBuilder<F1TelemetryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new F1TelemetryDbContext(opts);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.EnsureCreatedAsync();
        _repository = new SqliteLapRepository(_db);
    }

    [Fact]
    public async Task SaveLapAsync_PersistsLapToDatabase()
    {
        CompletedLap lap = BuildLap(lapNumber: 1, lapTimeMs: 90_000);

        await _repository.SaveLapAsync(lap);

        LapEntity? entity = await _db.Laps.FirstOrDefaultAsync();
        entity.Should().NotBeNull();
        entity!.LapNumber.Should().Be(1);
        entity.LapTimeMs.Should().Be(90_000);
        entity.SessionId.Should().Be(lap.SessionId.Value);
    }

    [Fact]
    public async Task SaveLapAsync_FirstLap_IsPersonalBest()
    {
        CompletedLap lap = BuildLap(lapNumber: 1, lapTimeMs: 90_000);

        await _repository.SaveLapAsync(lap);

        LapEntity? entity = await _db.Laps.FirstAsync();
        entity.IsPersonalBest.Should().BeTrue();
    }

    [Fact]
    public async Task SaveLapAsync_FasterSecondLap_UpdatesPersonalBest()
    {
        CompletedLap slow = BuildLap(lapNumber: 1, lapTimeMs: 90_000);
        CompletedLap fast = BuildLap(lapNumber: 2, lapTimeMs: 85_000);

        await _repository.SaveLapAsync(slow);
        await _repository.SaveLapAsync(fast);

        List<LapEntity> laps = await _db.Laps.OrderBy(l => l.LapNumber).ToListAsync();
        laps[0].IsPersonalBest.Should().BeFalse();
        laps[1].IsPersonalBest.Should().BeTrue();
    }

    [Fact]
    public async Task SaveLapAsync_SlowerSecondLap_KeepsOriginalBest()
    {
        CompletedLap fast = BuildLap(lapNumber: 1, lapTimeMs: 85_000);
        CompletedLap slow = BuildLap(lapNumber: 2, lapTimeMs: 90_000);

        await _repository.SaveLapAsync(fast);
        await _repository.SaveLapAsync(slow);

        List<LapEntity> laps = await _db.Laps.OrderBy(l => l.LapNumber).ToListAsync();
        laps[0].IsPersonalBest.Should().BeTrue();
        laps[1].IsPersonalBest.Should().BeFalse();
    }

    [Fact]
    public async Task GetLapsBySessionAsync_ReturnsOnlyLapsForSession()
    {
        SessionId sessionA = SessionId.From(0xAAAAAAAAAAAAAAAAUL);
        SessionId sessionB = SessionId.From(0xBBBBBBBBBBBBBBBBUL);

        await _repository.SaveLapAsync(BuildLap(lapNumber: 1, sessionId: sessionA));
        await _repository.SaveLapAsync(BuildLap(lapNumber: 2, sessionId: sessionA));
        await _repository.SaveLapAsync(BuildLap(lapNumber: 1, sessionId: sessionB));

        IReadOnlyList<CompletedLap> laps = await _repository.GetLapsBySessionAsync(sessionA);

        laps.Should().HaveCount(2);
        laps.All(l => l.SessionId == sessionA).Should().BeTrue();
    }

    [Fact]
    public async Task GetPersonalBestAsync_ReturnsNull_WhenNoLapsSaved()
    {
        CompletedLap? best = await _repository.GetPersonalBestAsync();
        best.Should().BeNull();
    }

    [Fact]
    public async Task GetPersonalBestAsync_ReturnsLapWithShortestTime()
    {
        await _repository.SaveLapAsync(BuildLap(lapNumber: 1, lapTimeMs: 90_000));
        await _repository.SaveLapAsync(BuildLap(lapNumber: 2, lapTimeMs: 85_000));
        await _repository.SaveLapAsync(BuildLap(lapNumber: 3, lapTimeMs: 88_000));

        CompletedLap? best = await _repository.GetPersonalBestAsync();

        best.Should().NotBeNull();
        best!.LapTime.TotalMilliseconds.Should().BeApproximately(85_000, precision: 1);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();

    private static CompletedLap BuildLap(
        int lapNumber,
        uint lapTimeMs = 90_000,
        SessionId sessionId = default)
    {
        if (sessionId == default)
        {
            sessionId = SessionId.From(0xDEADBEEF00000001UL);
        }

        return new CompletedLap(
            SessionId: sessionId,
            LapNumber: lapNumber,
            LapTime: TimeSpan.FromMilliseconds(lapTimeMs),
            Sector1: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            Sector2: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            Sector3: TimeSpan.FromMilliseconds(lapTimeMs / 3),
            IsValid: true,
            Frames: []);
    }
}
