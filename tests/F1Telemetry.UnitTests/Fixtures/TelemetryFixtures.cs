using System.Buffers.Binary;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Parsing.Structs;

namespace F1Telemetry.UnitTests.Fixtures;

// Creates valid F1 2024 UDP binary packets for use in unit tests.
// Packet layout matches the raw structs in F1Telemetry.Ingestion.Parsing.Structs.
internal static class TelemetryFixtures
{
    private const int NumCars = 22;

    public static byte[] CarTelemetryPacket(
        float speedKmh = 200f,
        float throttle = 1f,
        float brake = 0f,
        int gear = 7,
        int engineRpm = 12500,
        byte playerCarIndex = 0)
    {
        int totalSize = RawPacketHeader.Size + NumCars * RawCarTelemetryData.Size + 3;
        byte[] packet = new byte[totalSize];
        var span = packet.AsSpan();

        WriteHeader(span, PacketId.CarTelemetry, playerCarIndex);

        // Write player car's telemetry at the correct offset
        int carOffset = RawPacketHeader.Size + playerCarIndex * RawCarTelemetryData.Size;
        var car = span[carOffset..];
        BinaryPrimitives.WriteUInt16LittleEndian(car, (ushort)speedKmh);  // offset 0: speed
        BinaryPrimitives.WriteSingleLittleEndian(car[2..], throttle);      // offset 2: throttle
        BinaryPrimitives.WriteSingleLittleEndian(car[6..], brake);         // offset 6: brake
        car[11] = (byte)gear;                                              // offset 11: gear (sbyte)
        BinaryPrimitives.WriteUInt16LittleEndian(car[12..], (ushort)engineRpm); // offset 12

        return packet;
    }

    public static byte[] LapDataPacket(
        byte currentLapNum = 1,
        uint lastLapTimeMs = 85_000,
        bool currentLapInvalid = false,
        byte playerCarIndex = 0)
    {
        int totalSize = RawPacketHeader.Size + NumCars * RawLapData.Size;
        byte[] packet = new byte[totalSize];
        var span = packet.AsSpan();

        WriteHeader(span, PacketId.LapData, playerCarIndex);

        int carOffset = RawPacketHeader.Size + playerCarIndex * RawLapData.Size;
        var car = span[carOffset..];
        BinaryPrimitives.WriteUInt32LittleEndian(car, lastLapTimeMs);      // offset 0: lastLapTimeInMs
        car[33] = currentLapNum;                                           // offset 33: currentLapNum
        car[37] = currentLapInvalid ? (byte)1 : (byte)0;                  // offset 37: currentLapInvalid

        return packet;
    }

    public static byte[] PacketWithId(byte packetId, byte playerCarIndex = 0)
    {
        // Minimal 30-byte header with the given packet ID
        byte[] packet = new byte[RawPacketHeader.Size];
        WriteHeader(packet.AsSpan(), (PacketId)packetId, playerCarIndex);
        return packet;
    }

    // Writes the 30-byte header fields at the start of the span
    private static void WriteHeader(Span<byte> span, PacketId packetId, byte playerCarIndex)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, 2024);            // offset  0: PacketFormat
        span[6] = (byte)packetId;                                        // offset  6: PacketId
        BinaryPrimitives.WriteUInt64LittleEndian(span[8..], 0xDEADBEEFCAFEBABEul); // offset 8: SessionUID
        span[28] = playerCarIndex;                                       // offset 28: PlayerCarIndex
    }
}
