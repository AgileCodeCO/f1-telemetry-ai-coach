using System.ComponentModel.DataAnnotations;

namespace F1Telemetry.Ingestion.Options;

public sealed class UdpOptions
{
    public const string SectionName = "Udp";

    [Range(1024, 65535)]
    public int Port { get; set; } = 20777;

    [Range(1024, 65536)]
    public int BufferSize { get; set; } = 4096;

    public int ChannelCapacity { get; set; } = 4096;
}
