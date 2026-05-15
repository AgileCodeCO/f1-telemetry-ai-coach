using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Parsing;
using F1Telemetry.Ingestion.Parsing.Structs;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers.Binary;

namespace F1Telemetry.Benchmarks.Ingestion;

/// <summary>
/// Goal: PacketParser must process one packet in under 50µs.
/// Run with: dotnet run -c Release --project tests/F1Telemetry.Benchmarks
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class PacketParserBenchmarks
{
    private PacketParser _parser = null!;
    private byte[] _carTelemetryPacket = null!;
    private byte[] _lapDataPacket = null!;
    private byte[] _motionPacket = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new PacketParser(NullLogger<PacketParser>.Instance);
        _carTelemetryPacket = BuildCarTelemetryPacket();
        _lapDataPacket = BuildLapDataPacket();
        _motionPacket = BuildMotionPacket();
    }

    [Benchmark(Baseline = true)]
    public ParseResult<IParsedPacket> Parse_CarTelemetry() => _parser.Parse(_carTelemetryPacket);

    [Benchmark]
    public ParseResult<IParsedPacket> Parse_LapData() => _parser.Parse(_lapDataPacket);

    [Benchmark]
    public ParseResult<IParsedPacket> Parse_Motion() => _parser.Parse(_motionPacket);

    private static byte[] BuildCarTelemetryPacket()
    {
        int size = RawPacketHeader.Size + 22 * RawCarTelemetryData.Size + 3;
        byte[] buf = new byte[size];
        WriteHeader(buf, packetId: 6); // CarTelemetry
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(RawPacketHeader.Size), 250); // speed
        return buf;
    }

    private static byte[] BuildLapDataPacket()
    {
        int size = RawPacketHeader.Size + 22 * RawLapData.Size + 2;
        byte[] buf = new byte[size];
        WriteHeader(buf, packetId: 2); // LapData
        buf[RawPacketHeader.Size + 33] = 1; // currentLapNum
        return buf;
    }

    private static byte[] BuildMotionPacket()
    {
        int size = RawPacketHeader.Size + 22 * RawMotionData.Size;
        byte[] buf = new byte[size];
        WriteHeader(buf, packetId: 0); // Motion
        return buf;
    }

    private static void WriteHeader(byte[] buf, byte packetId)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 2025); // PacketFormat
        buf[6] = packetId;
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(7), 0xDEADBEEFCAFEBABEUL);
        buf[27] = 0; // playerCarIndex
    }
}
