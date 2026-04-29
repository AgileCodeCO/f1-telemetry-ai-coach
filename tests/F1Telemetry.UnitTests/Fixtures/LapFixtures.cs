using F1Telemetry.Contracts;

namespace F1Telemetry.UnitTests.Fixtures;

internal static class LapFixtures
{
    private static readonly SessionId DefaultSession = SessionId.From(0xDEADBEEF00000001);

    public static CompletedLap CreateLap(
        int lapTimeMs = 85_000,
        int sector1Ms = 28_000,
        int sector2Ms = 30_000,
        int sector3Ms = 27_000,
        bool isValid = true,
        int lapNumber = 1,
        SessionId? sessionId = null) =>
        new(
            SessionId: sessionId ?? DefaultSession,
            LapNumber: lapNumber,
            LapTime: TimeSpan.FromMilliseconds(lapTimeMs),
            Sector1: TimeSpan.FromMilliseconds(sector1Ms),
            Sector2: TimeSpan.FromMilliseconds(sector2Ms),
            Sector3: TimeSpan.FromMilliseconds(sector3Ms),
            IsValid: isValid,
            Frames: []);
}
