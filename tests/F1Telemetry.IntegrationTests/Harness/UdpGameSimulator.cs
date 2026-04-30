using System.Net;
using System.Net.Sockets;

namespace F1Telemetry.IntegrationTests.Harness;

/// <summary>
/// Simulates the PS5 F1 game's UDP telemetry broadcast.
/// Sends correctly structured packets to a local port for integration testing.
/// </summary>
internal sealed class UdpGameSimulator : IAsyncDisposable
{
    private readonly UdpClient _client;
    private readonly IPEndPoint _target;

    public UdpGameSimulator(int targetPort = 20777)
    {
        _client = new UdpClient();
        _target = new IPEndPoint(IPAddress.Loopback, targetPort);
    }

    /// <summary>
    /// Sends all frames for the specified lap, then a boundary packet that increments the lap number.
    /// </summary>
    public async Task SendLapAsync(
        SessionReplay replay,
        int lapNumber,
        float intervalMs = 1f,
        CancellationToken ct = default)
    {
        LapReplay lap = replay.Laps.First(l => l.LapNumber == lapNumber);
        const byte playerCarIndex = 0;

        foreach (FrameReplay frame in lap.Frames)
        {
            ct.ThrowIfCancellationRequested();

            await _client.SendAsync(
                PacketBuilder.CarTelemetry(replay.SessionUid, playerCarIndex,
                    frame.SpeedKmh, frame.Throttle, frame.Brake).AsMemory(),
                _target, ct);

            await _client.SendAsync(
                PacketBuilder.LapData(replay.SessionUid, playerCarIndex, (byte)lapNumber).AsMemory(),
                _target, ct);

            if (intervalMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(intervalMs), ct);
            }
        }

        // Trigger lap boundary: lap number increments, last lap time is included
        await _client.SendAsync(
            PacketBuilder.LapData(replay.SessionUid, playerCarIndex,
                (byte)(lapNumber + 1), lap.LapTimeMs).AsMemory(),
            _target, ct);
    }

    /// <summary>
    /// Sends a single raw byte array immediately. Use for targeted scenario tests.
    /// </summary>
    public async Task SendPacketAsync(byte[] packet, CancellationToken ct = default) =>
        await _client.SendAsync(packet.AsMemory(), _target, ct);

    /// <summary>
    /// Sends packets at maximum rate with no delay — use for channel capacity / memory tests.
    /// </summary>
    public async Task FloodAsync(int packetCount, CancellationToken ct = default)
    {
        byte[] packet = PacketBuilder.CarTelemetry(0xDEADBEEFCAFEBABEul, 0, 200f, 1f, 0f);
        for (int i = 0; i < packetCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _client.SendAsync(packet.AsMemory(), _target, ct);
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
