using System.Globalization;
using F1Telemetry.Agents.Extensions;
using F1Telemetry.App.Api;
using F1Telemetry.App.Components;
using F1Telemetry.App.HealthChecks;
using F1Telemetry.App.Hubs;
using F1Telemetry.App.Services;
using F1Telemetry.Ingestion.Extensions;
using F1Telemetry.Ingestion.Metrics;
using F1Telemetry.Storage.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) =>
    {
        string logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "f1telemetry", "logs");
        Directory.CreateDirectory(logsDir);

        config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logsDir, "app-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    });

    builder.Services.AddIngestion();
    builder.Services.AddStorage(builder.Configuration);
    builder.Services.AddAgents(builder.Configuration);

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<TelemetryState>();
    builder.Services.AddHostedService<TelemetryBroadcastService>();
    builder.Services.AddHostedService<ReplayHostedService>();

    builder.Services.AddHealthChecks()
        .AddCheck<InfluxDbHealthCheck>("influxdb", tags: ["ready"])
        .AddCheck<UdpListenerHealthCheck>("udp_listener", tags: ["live"]);

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";
            var result = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    data = e.Value.Data
                })
            };
            await ctx.Response.WriteAsJsonAsync(result);
        }
    });

    app.MapGet("/api/metrics/packets", (PacketMetrics m) => new
    {
        totalReceived = m.TotalReceived,
        totalDropped = m.TotalDropped,
        dropRate = m.DropRate,
        dropRatePercent = m.DropRate * 100.0
    });

    app.MapSessionsApi();
    app.MapHub<TelemetryHub>("/hubs/telemetry");

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Console.Error.WriteLine($"[FTL] Application startup failed: {ex}");
}

public partial class Program { }
