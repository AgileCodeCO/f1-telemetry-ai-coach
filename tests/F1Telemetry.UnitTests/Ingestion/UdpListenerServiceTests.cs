using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Metrics;
using F1Telemetry.Ingestion.Options;
using F1Telemetry.Ingestion.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace F1Telemetry.UnitTests.Ingestion;

public class UdpListenerServiceTests
{
    private const int TestPort = 29777; // avoid collision with real port 20777

    private static UdpListenerService CreateService(
        ChannelWriter<RawPacket> writer, int port = TestPort)
    {
        var opts = Options.Create(new UdpOptions { Port = port, BufferSize = 4096 });
        return new UdpListenerService(writer, opts, new PacketMetrics(), NullLogger<UdpListenerService>.Instance);
    }

    [Fact]
    public async Task StopAsync_WhenCancelled_StopsGracefully()
    {
        var channel = Channel.CreateUnbounded<RawPacket>();
        var service = CreateService(channel.Writer);
        using var cts = new CancellationTokenSource();

        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(50); // allow service to start and bind

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        runTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveAsync_WhenPacketArrives_WritesToChannel()
    {
        var channel = Channel.CreateUnbounded<RawPacket>();
        var service = CreateService(channel.Writer, TestPort + 1);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await service.StartAsync(cts.Token);
        await Task.Delay(50); // allow binding

        // Send a test datagram
        using var udp = new UdpClient();
        byte[] payload = [1, 2, 3, 4, 5];
        await udp.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, TestPort + 1));

        // Wait for the packet to be received
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var packet = await channel.Reader.ReadAsync(readCts.Token);

        packet.Length.Should().Be(5);
        packet.Buffer[..packet.Length].Should().BeEquivalentTo(payload);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }
}
