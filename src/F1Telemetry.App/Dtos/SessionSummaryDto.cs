using System.Globalization;
using F1Telemetry.Contracts;

namespace F1Telemetry.App.Dtos;

public sealed record SessionSummaryDto(
    string SessionId,
    string TrackName,
    string SessionType,
    string StartedAt,
    int LapCount,
    int? BestLapTimeMs)
{
    public static SessionSummaryDto FromDomain(SessionSummary s) =>
        new(s.SessionId.Value,
            s.TrackName,
            s.SessionType,
            s.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            s.LapCount,
            s.BestLapTimeMs);
}
