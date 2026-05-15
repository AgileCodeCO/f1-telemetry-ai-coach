using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 25 per-car lap data — 57 bytes (unchanged from F1 24).
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawLapData
{
    public uint LastLapTimeInMs;           // offset  0
    public uint CurrentLapTimeInMs;        // offset  4
    public ushort Sector1TimeInMs;         // offset  8 — fractional ms
    public byte Sector1TimeMinutes;        // offset 10
    public ushort Sector2TimeInMs;         // offset 11
    public byte Sector2TimeMinutes;        // offset 13
    public ushort DeltaToCarInFrontInMs;   // offset 14
    public byte DeltaToCarInFrontMinutes;  // offset 16
    public ushort DeltaToRaceLeaderInMs;   // offset 17
    public byte DeltaToRaceLeaderMinutes;  // offset 19
    public float LapDistance;              // offset 20
    public float TotalDistance;            // offset 24
    public float SafetyCarDelta;           // offset 28
    public byte CarPosition;               // offset 32
    public byte CurrentLapNum;             // offset 33
    public byte PitStatus;                 // offset 34
    public byte NumPitStops;               // offset 35
    public byte Sector;                    // offset 36 — 0=S1,1=S2,2=S3
    public byte CurrentLapInvalid;         // offset 37 — 1=invalid
    public byte Penalties;                 // offset 38
    public byte TotalWarnings;             // offset 39
    public byte CornerCuttingWarnings;     // offset 40
    public byte NumUnservedDriveThroughPens; // offset 41
    public byte NumUnservedStopGoPens;     // offset 42
    public byte GridPosition;              // offset 43
    public byte DriverStatus;              // offset 44
    public byte ResultStatus;              // offset 45
    public byte PitLaneTimerActive;        // offset 46
    public ushort PitLaneTimeInLaneInMs;   // offset 47
    public ushort PitStopTimerInMs;        // offset 49
    public byte PitStopShouldServePen;     // offset 51
    public float SpeedTrapFastestSpeed;    // offset 52
    public byte SpeedTrapFastestLap;       // offset 56

    public const int Size = 57;
}
