using System.Runtime.InteropServices;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Parsing.Structs;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Ingestion.Parsing;

internal sealed partial class PacketParser(ILogger<PacketParser> logger) : IPacketParser
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Unknown packet ID {PacketId} — ignored")]
    private static partial void LogUnknownPacketId(ILogger logger, byte packetId);

    public ParseResult<IParsedPacket> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < RawPacketHeader.Size)
        {
            return ParseResult.Fail<IParsedPacket>($"Packet too short: {data.Length} bytes");
        }

        var rawHeader = MemoryMarshal.Read<RawPacketHeader>(data);
        var header = MapHeader(rawHeader);

        return (PacketId)rawHeader.PacketId switch
        {
            PacketId.CarTelemetry => ParseCarTelemetry(data, header, rawHeader.PlayerCarIndex),
            PacketId.LapData      => ParseLapData(data, header, rawHeader.PlayerCarIndex),
            PacketId.Motion       => ParseMotionData(data, header, rawHeader.PlayerCarIndex),
            PacketId.Session      => ParseSessionData(data, header),
            PacketId.CarStatus    => ParseCarStatus(data, header, rawHeader.PlayerCarIndex),
            PacketId.CarDamage    => ParseCarDamage(data, header, rawHeader.PlayerCarIndex),
            _ => UnknownPacket(rawHeader.PacketId)
        };
    }

    private static ParseResult<IParsedPacket> ParseCarTelemetry(
        ReadOnlySpan<byte> data, PacketHeader header, byte carIndex)
    {
        int offset = RawPacketHeader.Size + carIndex * RawCarTelemetryData.Size;
        if (data.Length < offset + RawCarTelemetryData.Size)
        {
            return ParseResult.Fail<IParsedPacket>(
                $"CarTelemetry packet too short for player car (carIndex={carIndex}, got={data.Length}, need={offset + RawCarTelemetryData.Size}, perCarSize={RawCarTelemetryData.Size})");
        }

        var raw = MemoryMarshal.Read<RawCarTelemetryData>(data[offset..]);
        return ParseResult.Ok<IParsedPacket>(new PacketCarTelemetryData
        {
            Header    = header,
            SpeedKmh  = raw.Speed,
            Throttle  = raw.Throttle,
            Brake     = raw.Brake,
            Gear      = raw.Gear,
            EngineRpm = raw.EngineRpm,
            Drs       = raw.Drs == 1,
            // Wire: RL=0, RR=1, FL=2, FR=3
            TyreTempRl = raw.TyresSurfaceTemperature0,
            TyreTempRr = raw.TyresSurfaceTemperature1,
            TyreTempFl = raw.TyresSurfaceTemperature2,
            TyreTempFr = raw.TyresSurfaceTemperature3
        });
    }

    private static ParseResult<IParsedPacket> ParseLapData(
        ReadOnlySpan<byte> data, PacketHeader header, byte carIndex)
    {
        int offset = RawPacketHeader.Size + carIndex * RawLapData.Size;
        if (data.Length < offset + RawLapData.Size)
        {
            return ParseResult.Fail<IParsedPacket>(
                $"LapData packet too short for player car (carIndex={carIndex}, got={data.Length}, need={offset + RawLapData.Size}, perCarSize={RawLapData.Size})");
        }

        var raw = MemoryMarshal.Read<RawLapData>(data[offset..]);
        uint s1Ms = raw.Sector1TimeMinutes * 60_000u + raw.Sector1TimeInMs;
        uint s2Ms = raw.Sector2TimeMinutes * 60_000u + raw.Sector2TimeInMs;

        return ParseResult.Ok<IParsedPacket>(new PacketLapData
        {
            Header              = header,
            LastLapTimeMs       = raw.LastLapTimeInMs,
            CurrentLapTimeMs    = raw.CurrentLapTimeInMs,
            Sector1TimeMs       = s1Ms,
            Sector2TimeMs       = s2Ms,
            CurrentLapNum       = raw.CurrentLapNum,
            PitStatus           = raw.PitStatus,
            CurrentLapInvalid   = raw.CurrentLapInvalid == 1
        });
    }

    private static ParseResult<IParsedPacket> ParseMotionData(
        ReadOnlySpan<byte> data, PacketHeader header, byte carIndex)
    {
        int offset = RawPacketHeader.Size + carIndex * RawMotionData.Size;
        if (data.Length < offset + RawMotionData.Size)
        {
            return ParseResult.Fail<IParsedPacket>(
                $"MotionData packet too short for player car (carIndex={carIndex}, got={data.Length}, need={offset + RawMotionData.Size}, perCarSize={RawMotionData.Size})");
        }

        var raw = MemoryMarshal.Read<RawMotionData>(data[offset..]);
        return ParseResult.Ok<IParsedPacket>(new PacketMotionData
        {
            Header             = header,
            WorldPositionX     = raw.WorldPositionX,
            WorldPositionY     = raw.WorldPositionY,
            WorldPositionZ     = raw.WorldPositionZ,
            WorldVelocityX     = raw.WorldVelocityX,
            WorldVelocityY     = raw.WorldVelocityY,
            WorldVelocityZ     = raw.WorldVelocityZ,
            GForceLateral      = raw.GForceLateral,
            GForceLongitudinal = raw.GForceLongitudinal,
            GForceVertical     = raw.GForceVertical
        });
    }

    private static ParseResult<IParsedPacket> ParseSessionData(
        ReadOnlySpan<byte> data, PacketHeader header)
    {
        int offset = RawPacketHeader.Size;
        if (data.Length < offset + RawSessionData.MinSize)
        {
            return ParseResult.Fail<IParsedPacket>("SessionData packet too short");
        }

        var raw = MemoryMarshal.Read<RawSessionData>(data[offset..]);
        return ParseResult.Ok<IParsedPacket>(new PacketSessionData
        {
            Header      = header,
            SessionType = (SessionType)raw.SessionType,
            TrackId     = raw.TrackId,
            TotalLaps   = raw.TotalLaps,
            TrackLength = raw.TrackLength
        });
    }

    private static ParseResult<IParsedPacket> ParseCarStatus(
        ReadOnlySpan<byte> data, PacketHeader header, byte carIndex)
    {
        int offset = RawPacketHeader.Size + carIndex * RawCarStatusData.Size;
        if (data.Length < offset + RawCarStatusData.Size)
        {
            return ParseResult.Fail<IParsedPacket>(
                $"CarStatus packet too short for player car (carIndex={carIndex}, got={data.Length}, need={offset + RawCarStatusData.Size}, perCarSize={RawCarStatusData.Size})");
        }

        var raw = MemoryMarshal.Read<RawCarStatusData>(data[offset..]);
        return ParseResult.Ok<IParsedPacket>(new PacketCarStatusData
        {
            Header             = header,
            VisualTyreCompound = (TyreCompound)raw.VisualTyreCompound,
            TyresAgeLaps       = raw.TyresAgeLaps,
            FuelInTank         = raw.FuelInTank
        });
    }

    private static ParseResult<IParsedPacket> ParseCarDamage(
        ReadOnlySpan<byte> data, PacketHeader header, byte carIndex)
    {
        int offset = RawPacketHeader.Size + carIndex * RawCarDamageData.FullSize;
        if (data.Length < offset + RawCarDamageData.MinSize)
        {
            return ParseResult.Fail<IParsedPacket>(
                $"CarDamage packet too short for player car (carIndex={carIndex}, got={data.Length}, need={offset + RawCarDamageData.MinSize}, perCarSize={RawCarDamageData.FullSize})");
        }

        var raw = MemoryMarshal.Read<RawCarDamageData>(data[offset..]);
        // Wire: RL=0, RR=1, FL=2, FR=3
        return ParseResult.Ok<IParsedPacket>(new PacketCarDamageData
        {
            Header     = header,
            TyreWearRl = raw.TyresWear0,
            TyreWearRr = raw.TyresWear1,
            TyreWearFl = raw.TyresWear2,
            TyreWearFr = raw.TyresWear3
        });
    }

    private ParseResult<IParsedPacket> UnknownPacket(byte packetId)
    {
        LogUnknownPacketId(logger, packetId);
        return ParseResult.Fail<IParsedPacket>($"Unknown packet ID {packetId}");
    }

    private static PacketHeader MapHeader(RawPacketHeader raw) =>
        new(raw.PacketFormat,
            (PacketId)raw.PacketId,
            raw.SessionUid,
            raw.SessionTime,
            raw.FrameIdentifier,
            raw.PlayerCarIndex);
}
