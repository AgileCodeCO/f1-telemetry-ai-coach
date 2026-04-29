namespace F1Telemetry.Contracts;

public interface IPacketParser
{
    ParseResult<IParsedPacket> Parse(ReadOnlySpan<byte> data);
}
