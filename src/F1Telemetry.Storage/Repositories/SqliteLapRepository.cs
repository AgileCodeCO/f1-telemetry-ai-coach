using System.Globalization;
using F1Telemetry.Contracts;
using F1Telemetry.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace F1Telemetry.Storage.Repositories;

internal sealed class SqliteLapRepository(F1TelemetryDbContext db) : ILapRepository
{
    public async Task SaveLapAsync(CompletedLap lap, CancellationToken ct = default)
    {
        await EnsureSessionExistsAsync(lap.SessionId, ct);

        CompletedLap? currentBest = await GetPersonalBestAsync(ct);
        bool isPersonalBest = currentBest is null || lap.LapTime < currentBest.LapTime;

        if (isPersonalBest && currentBest is not null)
        {
            LapEntity? previousBest = await db.Laps
                .FirstOrDefaultAsync(l => l.IsPersonalBest, ct);
            if (previousBest is not null)
            {
                previousBest.IsPersonalBest = false;
            }
        }

        float? maxSpeed = lap.Frames.Count > 0
            ? lap.Frames.Max(f => f.SpeedKmh)
            : null;

        float? avgThrottle = lap.Frames.Count > 0
            ? lap.Frames.Average(f => f.Throttle)
            : null;

        db.Laps.Add(new LapEntity
        {
            SessionId = lap.SessionId.Value,
            LapNumber = lap.LapNumber,
            LapTimeMs = (int)lap.LapTime.TotalMilliseconds,
            Sector1Ms = (int)lap.Sector1.TotalMilliseconds,
            Sector2Ms = (int)lap.Sector2.TotalMilliseconds,
            Sector3Ms = (int)lap.Sector3.TotalMilliseconds,
            IsPersonalBest = isPersonalBest,
            IsValid = lap.IsValid,
            MaxSpeedKmh = maxSpeed,
            AvgThrottle = avgThrottle,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CompletedLap>> GetLapsBySessionAsync(SessionId sessionId, CancellationToken ct = default)
    {
        List<LapEntity> entities = await db.Laps
            .Where(l => l.SessionId == sessionId.Value)
            .OrderBy(l => l.LapNumber)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<CompletedLap?> GetPersonalBestAsync(CancellationToken ct = default)
    {
        LapEntity? entity = await db.Laps
            .Where(l => l.IsValid && l.LapTimeMs != null)
            .OrderBy(l => l.LapTimeMs)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetAllSessionsAsync(CancellationToken ct = default)
    {
        List<SessionEntity> sessions = await db.Sessions
            .Include(s => s.Laps)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(ct);

        return sessions.Select(s =>
        {
            int? best = s.Laps
                .Where(l => l.IsValid && l.LapTimeMs.HasValue)
                .Select(l => l.LapTimeMs)
                .Min();

            bool parsed = DateTimeOffset.TryParse(
                s.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset startedAt);

            return new SessionSummary(
                SessionId: new SessionId(s.Id),
                TrackName: s.TrackName,
                SessionType: s.SessionType,
                StartedAt: parsed ? startedAt : DateTimeOffset.MinValue,
                LapCount: s.Laps.Count,
                BestLapTimeMs: best);
        }).ToList();
    }

    public async Task SaveFeedbackAsync(SessionId sessionId, int lapNumber, AgentFinding finding, CancellationToken ct = default)
    {
        LapEntity? lap = await db.Laps
            .FirstOrDefaultAsync(l => l.SessionId == sessionId.Value && l.LapNumber == lapNumber, ct);

        if (lap is null)
        {
            return;
        }

        db.LapFeedback.Add(new LapFeedbackEntity
        {
            LapId = lap.Id,
            AgentName = finding.AgentName,
            Category = finding.Category.ToString(),
            Finding = finding.Finding,
            EstimatedGainMs = finding.EstimatedGainMs,
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentFinding>> GetFeedbackAsync(SessionId sessionId, int lapNumber, CancellationToken ct = default)
    {
        LapEntity? lap = await db.Laps
            .Include(l => l.Feedback)
            .FirstOrDefaultAsync(l => l.SessionId == sessionId.Value && l.LapNumber == lapNumber, ct);

        if (lap is null)
        {
            return [];
        }

        return lap.Feedback
            .Select(f => new AgentFinding(
                AgentName: f.AgentName,
                Category: Enum.TryParse(f.Category, out AnalysisCategory cat) ? cat : AnalysisCategory.Delta,
                Finding: f.Finding,
                EstimatedGainMs: f.EstimatedGainMs ?? 0))
            .ToList();
    }

    private async Task EnsureSessionExistsAsync(SessionId sessionId, CancellationToken ct)
    {
        bool exists = await db.Sessions.AnyAsync(s => s.Id == sessionId.Value, ct);
        if (!exists)
        {
            db.Sessions.Add(new SessionEntity
            {
                Id = sessionId.Value,
                TrackName = "Unknown",
                SessionType = "Unknown",
                StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static CompletedLap MapToDomain(LapEntity e) =>
        new(
            SessionId: new SessionId(e.SessionId),
            LapNumber: e.LapNumber,
            LapTime: TimeSpan.FromMilliseconds(e.LapTimeMs ?? 0),
            Sector1: TimeSpan.FromMilliseconds(e.Sector1Ms ?? 0),
            Sector2: TimeSpan.FromMilliseconds(e.Sector2Ms ?? 0),
            Sector3: TimeSpan.FromMilliseconds(e.Sector3Ms ?? 0),
            IsValid: e.IsValid,
            Frames: []);
}
