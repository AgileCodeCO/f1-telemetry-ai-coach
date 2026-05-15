using F1Telemetry.Contracts;

namespace F1Telemetry.App.Dtos;

public sealed record CoachingReportDto(
    int LapNumber,
    int LapTimeMs,
    IReadOnlyList<FindingDto> Findings)
{
    public static CoachingReportDto FromDomain(LapCoachingReport r) =>
        new(r.Lap.LapNumber,
            (int)r.Lap.LapTime.TotalMilliseconds,
            r.Findings.Select(FindingDto.FromDomain).ToList());
}
