using F1Telemetry.Contracts;

namespace F1Telemetry.App.Dtos;

public sealed record LapSummaryDto(
    string SessionId,
    int LapNumber,
    int LapTimeMs,
    int Sector1Ms,
    int Sector2Ms,
    int Sector3Ms,
    bool IsPersonalBest,
    bool IsValid,
    float? MaxSpeedKmh,
    float? AvgThrottle)
{
    public static LapSummaryDto FromDomain(CompletedLap lap) =>
        new(lap.SessionId.Value,
            lap.LapNumber,
            (int)lap.LapTime.TotalMilliseconds,
            (int)lap.Sector1.TotalMilliseconds,
            (int)lap.Sector2.TotalMilliseconds,
            (int)lap.Sector3.TotalMilliseconds,
            IsPersonalBest: false,
            lap.IsValid,
            MaxSpeedKmh: lap.Frames.Count > 0 ? lap.Frames.Max(f => f.SpeedKmh) : null,
            AvgThrottle: lap.Frames.Count > 0 ? lap.Frames.Average(f => f.Throttle) : null);
}
