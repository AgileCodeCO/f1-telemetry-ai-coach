using F1Telemetry.Contracts;

namespace F1Telemetry.App.Dtos;

public sealed record TelemetryFrameDto(
    string SessionId,
    int LapNumber,
    float SessionTime,
    float SpeedKmh,
    float Throttle,
    float Brake,
    int Gear,
    int EngineRpm,
    bool Drs)
{
    public static TelemetryFrameDto FromDomain(TelemetryFrame f) =>
        new(f.SessionId.Value, f.LapNumber, f.SessionTime,
            f.SpeedKmh, f.Throttle, f.Brake, f.Gear, f.EngineRpm, f.Drs);
}
