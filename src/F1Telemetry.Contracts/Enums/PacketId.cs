namespace F1Telemetry.Contracts;

public enum PacketId : byte
{
    Motion = 0,
    Session = 1,
    LapData = 2,
    CarTelemetry = 6,
    CarStatus = 7,
    CarDamage = 10
}
