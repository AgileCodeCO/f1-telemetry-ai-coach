using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace F1Telemetry.Ingestion.Services;

internal sealed partial class UdpListenerService(
    ChannelWriter<RawPacket> channelWriter,
    IOptions<UdpOptions> options,
    ILogger<UdpListenerService> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "UDP listener bound to port {Port}")]
    private static partial void LogListenerBound(ILogger logger, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel full — packet dropped (port {Port})")]
    private static partial void LogPacketDropped(ILogger logger, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UDP socket error on port {Port}")]
    private static partial void LogSocketError(ILogger logger, Exception ex, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "UDP listener stopped")]
    private static partial void LogListenerStopped(ILogger logger);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, opts.Port));
        LogListenerBound(logger, opts.Port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(ct);
                var packet = new RawPacket(result.Buffer, result.Buffer.Length, DateTimeOffset.UtcNow);

                if (!channelWriter.TryWrite(packet))
                {
                    LogPacketDropped(logger, opts.Port);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                LogSocketError(logger, ex, opts.Port);
            }
        }

        LogListenerStopped(logger);
    }
}
