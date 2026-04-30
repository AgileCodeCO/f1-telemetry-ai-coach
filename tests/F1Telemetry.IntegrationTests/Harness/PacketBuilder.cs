using System.Buffers.Binary;
using F1Telemetry.Contracts;

namespace F1Telemetry.IntegrationTests.Harness;

// Builds valid F1 2024 UDP binary packets.
// Byte offsets match the raw struct layout in F1Telemetry.Ingestion.Parsing.Structs.
internal static class PacketBuilder
{
    private const int NumCars = 22;
    private const int HeaderSize = 30;
    private const int CarTelemetrySize = 56;
    private const int LapDataSize = 57;

    public static byte[] CarTelemetry(
        ulong sessionUid,
        byte playerCarIndex,
        float speedKmh,
        float throttle,
        float brake,
        int gear = 6,
        int engineRpm = 12_000)
    {
        byte[] packet = new byte[HeaderSize + NumCars * CarTelemetrySize + 3];
        Span<byte> span = packet.AsSpan();

        WriteHeader(span, PacketId.CarTelemetry, playerCarIndex, sessionUid);

        Span<byte> car = span[(HeaderSize + playerCarIndex * CarTelemetrySize)..];
        BinaryPrimitives.WriteUInt16LittleEndian(car, (ushort)speedKmh);
        BinaryPrimitives.WriteSingleLittleEndian(car[2..], throttle);
        BinaryPrimitives.WriteSingleLittleEndian(car[6..], brake);
        car[11] = (byte)gear;
        BinaryPrimitives.WriteUInt16LittleEndian(car[12..], (ushort)engineRpm);

        return packet;
    }

    public static byte[] LapData(
        ulong sessionUid,
        byte playerCarIndex,
        byte lapNum,
        uint lastLapTimeMs = 0,
        bool currentLapInvalid = false)
    {
        byte[] packet = new byte[HeaderSize + NumCars * LapDataSize];
        Span<byte> span = packet.AsSpan();

        WriteHeader(span, PacketId.LapData, playerCarIndex, sessionUid);

        Span<byte> car = span[(HeaderSize + playerCarIndex * LapDataSize)..];
        BinaryPrimitives.WriteUInt32LittleEndian(car, lastLapTimeMs);
        car[33] = lapNum;
        car[37] = currentLapInvalid ? (byte)1 : (byte)0;

        return packet;
    }

    private static void WriteHeader(Span<byte> span, PacketId packetId, byte playerCarIndex, ulong sessionUid)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, 2024);
        span[6] = (byte)packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(span[8..], sessionUid);
        span[28] = playerCarIndex;
    }
}
