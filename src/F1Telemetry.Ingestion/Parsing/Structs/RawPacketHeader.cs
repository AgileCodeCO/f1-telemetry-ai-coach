using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 2024 UDP packet header — 30 bytes. Verify against official F1 24 UDP specification.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawPacketHeader
{
    public ushort PacketFormat;           // offset  0 — 2024
    public byte GameYear;                 // offset  2
    public byte GameMajorVersion;         // offset  3
    public byte GameMinorVersion;         // offset  4
    public byte PacketVersion;            // offset  5
    public byte PacketId;                 // offset  6
    public byte SessionLinkIdentifier;    // offset  7
    public ulong SessionUid;              // offset  8
    public float SessionTime;             // offset 16
    public uint FrameIdentifier;          // offset 20
    public uint OverallFrameIdentifier;   // offset 24
    public byte PlayerCarIndex;           // offset 28
    public byte SecondaryPlayerCarIndex;  // offset 29

    public const int Size = 30;
}
