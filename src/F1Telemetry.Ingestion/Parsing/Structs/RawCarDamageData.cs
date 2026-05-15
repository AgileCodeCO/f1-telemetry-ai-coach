using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 25 per-car damage data — 46 bytes. TyreBlisters added vs F1 24. Wire order: RL=0, RR=1, FL=2, FR=3.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawCarDamageData
{
    public float TyresWear0;       // RL
    public float TyresWear1;       // RR
    public float TyresWear2;       // FL
    public float TyresWear3;       // FR
    public byte TyresDamage0;      // RL
    public byte TyresDamage1;      // RR
    public byte TyresDamage2;      // FL
    public byte TyresDamage3;      // FR
    public byte BrakesDamage0;     // RL
    public byte BrakesDamage1;     // RR
    public byte BrakesDamage2;     // FL
    public byte BrakesDamage3;     // FR
    public byte TyreBlisters0;     // RL (new in F1 25)
    public byte TyreBlisters1;     // RR
    public byte TyreBlisters2;     // FL
    public byte TyreBlisters3;     // FR
    public byte FrontLeftWingDamage;
    public byte FrontRightWingDamage;
    public byte RearWingDamage;
    public byte FloorDamage;
    public byte DiffuserDamage;
    public byte SidepodDamage;
    public byte DrsFault;
    public byte ErsFault;
    public byte GearBoxDamage;
    public byte EngineDamage;
    public byte EngineMguhWear;
    public byte EngineEsWear;
    public byte EngineCeWear;
    public byte EngineIceWear;
    public byte EngineMgukWear;
    public byte EngineTcWear;
    public byte EngineBlown;
    public byte EngineSeized;

    public const int MinSize = 16;
    public const int FullSize = 46;
}
