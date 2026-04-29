namespace F1Telemetry.Contracts;

public sealed record TelemetryFrame(
    SessionId SessionId,
    int LapNumber,
    float SessionTime,
    float SpeedKmh,
    float Throttle,
    float Brake,
    int Gear,
    int EngineRpm,
    bool Drs,
    float TyreTempFl,
    float TyreTempFr,
    float TyreTempRl,
    float TyreTempRr,
    float WorldPositionX,
    float WorldPositionY,
    float WorldPositionZ);
