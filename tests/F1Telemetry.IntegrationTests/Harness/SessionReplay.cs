namespace F1Telemetry.IntegrationTests.Harness;

internal sealed class SessionReplay
{
    public required ulong SessionUid { get; init; }
    public required IReadOnlyList<LapReplay> Laps { get; init; }
}

internal sealed class LapReplay
{
    public required int LapNumber { get; init; }
    public required uint LapTimeMs { get; init; }
    public required IReadOnlyList<FrameReplay> Frames { get; init; }
}

internal sealed class FrameReplay
{
    public required float SpeedKmh { get; init; }
    public required float Throttle { get; init; }
    public required float Brake { get; init; }
}

internal sealed class SessionReplayOptions
{
    public int LapCount { get; set; } = 3;
    public uint BaseLapTimeMs { get; set; } = 85_000;
    public uint LapTimeVarianceMs { get; set; } = 500;
    public int FramesPerLap { get; set; } = 60;
    public ulong SessionUid { get; set; } = 0xDEADBEEF00000001UL;
}

internal static class SessionReplayBuilder
{
    public static SessionReplay Build(Action<SessionReplayOptions> configure)
    {
        SessionReplayOptions opts = new();
        configure(opts);
        return BuildFrom(opts);
    }

    private static SessionReplay BuildFrom(SessionReplayOptions opts)
    {
        Random rng = new(42);
        List<LapReplay> laps = new(opts.LapCount);

        for (int lap = 1; lap <= opts.LapCount; lap++)
        {
            uint lapMs = opts.BaseLapTimeMs + (uint)(rng.NextSingle() * opts.LapTimeVarianceMs);
            laps.Add(new LapReplay
            {
                LapNumber = lap,
                LapTimeMs = lapMs,
                Frames = BuildFrames(opts.FramesPerLap, rng)
            });
        }

        return new SessionReplay { SessionUid = opts.SessionUid, Laps = laps };
    }

    private static List<FrameReplay> BuildFrames(int count, Random rng)
    {
        List<FrameReplay> frames = new(count);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            frames.Add(new FrameReplay
            {
                SpeedKmh = 150f + 100f * MathF.Sin(t * MathF.PI),
                Throttle = t < 0.5f ? 1f : 0f,
                Brake = t >= 0.5f ? 0.8f : 0f
            });
        }
        return frames;
    }
}
