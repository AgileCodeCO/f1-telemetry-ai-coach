using F1Telemetry.IntegrationTests.Harness;

namespace F1Telemetry.IntegrationTests;

internal sealed class Simulator
{
    public static async Task<int> Main(string[] args)
    {
        if (!args.Contains("--simulate"))
        {
            Console.Error.WriteLine("This project is an xUnit test suite. Use 'dotnet test' to run tests.");
            Console.Error.WriteLine("To simulate laps against a running app:");
            Console.Error.WriteLine("  dotnet run --project tests/F1Telemetry.IntegrationTests -- --simulate [--laps N] [--port N] [--fps N]");
            return 1;
        }

        int laps = ParseInt(args, "--laps", defaultValue: 3);
        int port = ParseInt(args, "--port", defaultValue: 20777);
        float fps = ParseFloat(args, "--fps", defaultValue: 60f);
        float intervalMs = fps > 0 ? 1000f / fps : 0f;

        Console.WriteLine($"Simulating {laps} lap(s) → UDP :{port} at {fps:F0} fps");
        Console.WriteLine("Press Ctrl+C to abort.");

        SessionReplay replay = SessionReplayBuilder.Build(o =>
        {
            o.LapCount = laps;
            o.BaseLapTimeMs = 85_000;
            o.LapTimeVarianceMs = 500;
            o.FramesPerLap = 1_200;
            o.SessionUid = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        });

        await using UdpGameSimulator simulator = new(targetPort: port);

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            for (int lap = 1; lap <= laps; lap++)
            {
                Console.Write($"  Lap {lap}/{laps} ... ");
                await simulator.SendLapAsync(replay, lap, intervalMs, cts.Token);
                Console.WriteLine("done");
            }

            Console.WriteLine("Simulation complete.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nAborted.");
            return 1;
        }

        return 0;
    }

    private static int ParseInt(string[] args, string flag, int defaultValue)
    {
        int idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int v) ? v : defaultValue;
    }

    private static float ParseFloat(string[] args, string flag, float defaultValue)
    {
        int idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length && float.TryParse(args[idx + 1], out float v) ? v : defaultValue;
    }
}
