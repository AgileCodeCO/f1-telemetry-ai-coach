using System.Runtime.InteropServices;

namespace F1Telemetry.Ingestion.Parsing.Structs;

// F1 2024 per-car damage data — leading tyre wear fields (16 bytes).
// Wire order: RL=0, RR=1, FL=2, FR=3.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct RawCarDamageData
{
    public float TyresWear0;  // RL
    public float TyresWear1;  // RR
    public float TyresWear2;  // FL
    public float TyresWear3;  // FR

    public const int MinSize = 16;
    public const int FullSize = 36; // full per-car size for offset arithmetic
}
