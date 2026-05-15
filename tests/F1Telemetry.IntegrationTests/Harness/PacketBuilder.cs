using System.Buffers.Binary;
using F1Telemetry.Contracts;

namespace F1Telemetry.IntegrationTests.Harness;

// Builds valid F1 25 UDP binary packets.
// Byte offsets match the raw struct layout in F1Telemetry.Ingestion.Parsing.Structs.
internal static class PacketBuilder
{
    private const int NumCars = 22;
    private const int HeaderSize = 29;   // F1 25: SessionLinkIdentifier removed
    private const int CarTelemetrySize = 60; // F1 25: Steer float added
    private const int LapDataSize = 57;  // unchanged from F1 24

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
        BinaryPrimitives.WriteUInt16LittleEndian(car, (ushort)speedKmh); // offset  0: speed
        BinaryPrimitives.WriteSingleLittleEndian(car[2..], throttle);    // offset  2: throttle
        // offset 6: steer — left at zero
        BinaryPrimitives.WriteSingleLittleEndian(car[10..], brake);      // offset 10: brake
        car[15] = (byte)gear;                                            // offset 15: gear
        BinaryPrimitives.WriteUInt16LittleEndian(car[16..], (ushort)engineRpm); // offset 16: engineRPM

        return packet;
    }

    public static byte[] LapData(
        ulong sessionUid,
        byte playerCarIndex,
        byte lapNum,
        uint lastLapTimeMs = 0,
        bool currentLapInvalid = false)
    {
        byte[] packet = new byte[HeaderSize + NumCars * LapDataSize + 2];
        Span<byte> span = packet.AsSpan();

        WriteHeader(span, PacketId.LapData, playerCarIndex, sessionUid);

        Span<byte> car = span[(HeaderSize + playerCarIndex * LapDataSize)..];
        BinaryPrimitives.WriteUInt32LittleEndian(car, lastLapTimeMs);    // offset  0: lastLapTimeInMs
        car[33] = lapNum;                                                 // offset 33: currentLapNum
        car[37] = currentLapInvalid ? (byte)1 : (byte)0;                // offset 37: currentLapInvalid

        return packet;
    }

    // F1 25 header: 29 bytes — no SessionLinkIdentifier
    private static void WriteHeader(Span<byte> span, PacketId packetId, byte playerCarIndex, ulong sessionUid)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, 2025); // offset  0: PacketFormat
        span[6] = (byte)packetId;                             // offset  6: PacketId
        BinaryPrimitives.WriteUInt64LittleEndian(span[7..], sessionUid); // offset  7: SessionUID
        span[27] = playerCarIndex;                            // offset 27: PlayerCarIndex
    }
}
