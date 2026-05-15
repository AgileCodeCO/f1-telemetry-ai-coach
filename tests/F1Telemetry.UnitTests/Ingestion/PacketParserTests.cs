using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Parsing;
using F1Telemetry.UnitTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace F1Telemetry.UnitTests.Ingestion;

public class PacketParserTests
{
    private readonly PacketParser _parser = new(NullLogger<PacketParser>.Instance);

    [Fact]
    public void Parse_CarTelemetryPacket_ReturnsCorrectSpeed()
    {
        byte[] raw = TelemetryFixtures.CarTelemetryPacket(speedKmh: 290f);

        var result = _parser.Parse(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<PacketCarTelemetryData>()
              .Which.SpeedKmh.Should().BeApproximately(290f, precision: 0.5f);
    }

    [Fact]
    public void Parse_CarTelemetryPacket_ReturnsCorrectThrottleAndBrake()
    {
        byte[] raw = TelemetryFixtures.CarTelemetryPacket(throttle: 0.75f, brake: 0.0f);

        var result = _parser.Parse(raw);

        var packet = result.Value.Should().BeOfType<PacketCarTelemetryData>().Subject;
        packet.Throttle.Should().BeApproximately(0.75f, precision: 0.001f);
        packet.Brake.Should().BeApproximately(0.0f, precision: 0.001f);
    }

    [Fact]
    public void Parse_CarTelemetryPacket_ReturnsCorrectSessionUid()
    {
        byte[] raw = TelemetryFixtures.CarTelemetryPacket();

        var result = _parser.Parse(raw);

        result.Value.Should().BeOfType<PacketCarTelemetryData>()
              .Which.Header.SessionUid.Should().Be(0xDEADBEEFCAFEBABEul);
    }

    [Fact]
    public void Parse_LapDataPacket_ReturnsCorrectLapNumber()
    {
        byte[] raw = TelemetryFixtures.LapDataPacket(currentLapNum: 3);

        var result = _parser.Parse(raw);

        result.Value.Should().BeOfType<PacketLapData>()
              .Which.CurrentLapNum.Should().Be(3);
    }

    [Fact]
    public void Parse_LapDataPacket_ReturnsCorrectLapTime()
    {
        byte[] raw = TelemetryFixtures.LapDataPacket(lastLapTimeMs: 84_500);

        var result = _parser.Parse(raw);

        result.Value.Should().BeOfType<PacketLapData>()
              .Which.LastLapTimeMs.Should().Be(84_500u);
    }

    [Fact]
    public void Parse_LapDataPacket_ReturnsInvalidFlag()
    {
        byte[] raw = TelemetryFixtures.LapDataPacket(currentLapInvalid: true);

        var result = _parser.Parse(raw);

        result.Value.Should().BeOfType<PacketLapData>()
              .Which.CurrentLapInvalid.Should().BeTrue();
    }

    [Fact]
    public void Parse_UnknownPacketId_ReturnsFail()
    {
        byte[] raw = TelemetryFixtures.PacketWithId(packetId: 99);

        var result = _parser.Parse(raw);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unknown packet ID");
    }

    [Fact]
    public void Parse_PacketTooShort_ReturnsFail()
    {
        byte[] raw = new byte[10]; // shorter than 29-byte header

        var result = _parser.Parse(raw);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("too short");
    }

    [Fact]
    public void Parse_CarTelemetryPacket_HeaderPacketIdIsCarTelemetry()
    {
        byte[] raw = TelemetryFixtures.CarTelemetryPacket();

        var result = _parser.Parse(raw);

        result.Value.Should().BeOfType<PacketCarTelemetryData>()
              .Which.Header.PacketId.Should().Be(PacketId.CarTelemetry);
    }
}
