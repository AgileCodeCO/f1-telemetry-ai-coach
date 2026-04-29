using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 2024 per-car status data — 29 bytes. Leading fields only.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawCarStatusData
{
    public byte TractionControl;
    public byte AntiLockBrakes;
    public byte FuelMix;
    public byte FrontBrakeBias;
    public byte PitLimiterStatus;
    public float FuelInTank;
    public float FuelCapacity;
    public float FuelRemainingLaps;
    public ushort MaxRpm;
    public ushort IdleRpm;
    public byte MaxGears;
    public byte DrsAllowed;
    public ushort DrsActivationDistance;
    public byte ActualTyreCompound;
    public byte VisualTyreCompound;
    public byte TyresAgeLaps;

    public const int Size = 29;
}
