using System.Threading.Channels;
using F1Telemetry.Contracts;
using F1Telemetry.Ingestion.Internal;
using F1Telemetry.Ingestion.Options;
using F1Telemetry.Ingestion.Parsing;
using F1Telemetry.Ingestion.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace F1Telemetry.Ingestion.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddOptions<UdpOptions>()
            .BindConfiguration(UdpOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPacketParser, PacketParser>();
        services.AddSingleton<IEventBus, InProcessEventBus>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<UdpOptions>>().Value;
            return Channel.CreateBounded<RawPacket>(new BoundedChannelOptions(opts.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });
        });

        services.AddSingleton(sp => sp.GetRequiredService<Channel<RawPacket>>().Writer);
        services.AddSingleton(sp => sp.GetRequiredService<Channel<RawPacket>>().Reader);

        services.AddHostedService<UdpListenerService>();

        // Register SessionManager as singleton so both ISessionManager and IHostedService share the same instance
        services.AddSingleton<SessionManager>();
        services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
        services.AddHostedService(sp => sp.GetRequiredService<SessionManager>());

        return services;
    }
}
