using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 2024 session data — leading fields only (packet contains ~700 bytes total).
// We only deserialize the fields we need from the start of the struct.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawSessionData
{
    public byte Weather;
    public sbyte TrackTemperature;
    public sbyte AirTemperature;
    public byte TotalLaps;
    public ushort TrackLength;
    public byte SessionType;
    public sbyte TrackId;

    public const int MinSize = 9; // bytes needed to read the above fields
}
