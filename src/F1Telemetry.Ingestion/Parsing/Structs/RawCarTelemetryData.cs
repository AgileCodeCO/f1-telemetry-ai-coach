using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 2024 per-car telemetry — 56 bytes. Array fields expanded to named fields to avoid unsafe code.
// Wire order for tyre arrays: RL=0, RR=1, FL=2, FR=3.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawCarTelemetryData
{
    public ushort Speed;                   // km/h
    public float Throttle;                 // 0.0–1.0
    public float Brake;                    // 0.0–1.0
    public byte Clutch;                    // 0–100
    public sbyte Gear;                     // -1=R, 0=N, 1-8
    public ushort EngineRpm;
    public byte Drs;                       // 0=off, 1=on
    public byte RevLightsPercent;
    public ushort RevLightsBitValue;
    public ushort BrakesTemperature0;      // RL
    public ushort BrakesTemperature1;      // RR
    public ushort BrakesTemperature2;      // FL
    public ushort BrakesTemperature3;      // FR
    public byte TyresSurfaceTemperature0;  // RL (°C)
    public byte TyresSurfaceTemperature1;  // RR
    public byte TyresSurfaceTemperature2;  // FL
    public byte TyresSurfaceTemperature3;  // FR
    public byte TyresInnerTemperature0;    // RL
    public byte TyresInnerTemperature1;    // RR
    public byte TyresInnerTemperature2;    // FL
    public byte TyresInnerTemperature3;    // FR
    public ushort EngineTemperature;       // °C
    public float TyresPressure0;           // RL (PSI)
    public float TyresPressure1;           // RR
    public float TyresPressure2;           // FL
    public float TyresPressure3;           // FR
    public byte SurfaceType0;              // RL
    public byte SurfaceType1;              // RR
    public byte SurfaceType2;              // FL
    public byte SurfaceType3;              // FR

    public const int Size = 56;
}
