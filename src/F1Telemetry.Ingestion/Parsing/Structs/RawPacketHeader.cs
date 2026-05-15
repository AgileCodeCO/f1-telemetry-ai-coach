using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 25 UDP packet header — 29 bytes. SessionLinkIdentifier removed vs F1 24.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawPacketHeader
{
    public ushort PacketFormat;           // offset  0 — 2025
    public byte GameYear;                 // offset  2
    public byte GameMajorVersion;         // offset  3
    public byte GameMinorVersion;         // offset  4
    public byte PacketVersion;            // offset  5
    public byte PacketId;                 // offset  6
    public ulong SessionUid;              // offset  7
    public float SessionTime;             // offset 15
    public uint FrameIdentifier;          // offset 19
    public uint OverallFrameIdentifier;   // offset 23
    public byte PlayerCarIndex;           // offset 27
    public byte SecondaryPlayerCarIndex;  // offset 28

    public const int Size = 29;
}
