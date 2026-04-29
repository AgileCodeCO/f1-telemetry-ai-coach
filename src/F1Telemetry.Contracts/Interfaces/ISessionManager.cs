namespace F1Telemetry.Contracts;

public interface ISessionManager
{
    void ProcessCarTelemetry(PacketCarTelemetryData data);
    void ProcessLapData(PacketLapData data);
    void ProcessMotionData(PacketMotionData data);
    void ProcessSessionData(PacketSessionData data);
    void ProcessCarStatus(PacketCarStatusData data);
    void ProcessCarDamage(PacketCarDamageData data);
}
