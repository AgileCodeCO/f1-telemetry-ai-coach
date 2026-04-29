namespace F1Telemetry.Contracts;

public sealed record RawPacket(byte[] Buffer, int Length, DateTimeOffset ReceivedAt);
