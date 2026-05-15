using F1Telemetry.Contracts;
using F1Telemetry.Storage.Data;
using F1Telemetry.Storage.Options;
using F1Telemetry.Storage.Repositories;
using F1Telemetry.Storage.Services;
using InfluxDB.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace F1Telemetry.Storage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(o => configuration.GetSection("Storage").Bind(o));
        services.Configure<InfluxDbOptions>(o => configuration.GetSection("InfluxDb").Bind(o));

        string sqliteConnectionString = ExpandHome(
            configuration.GetConnectionString("Sqlite") ?? "Data Source=f1.db");

        services.AddDbContext<F1TelemetryDbContext>(opts =>
            opts.UseSqlite(sqliteConnectionString));

        services.AddSingleton<IInfluxDBClient>(_ =>
        {
            InfluxDbOptions influxOpts = new();
            configuration.GetSection("InfluxDb").Bind(influxOpts);
            return new InfluxDBClient(influxOpts.Url, influxOpts.Token);
        });

        services.AddScoped<ILapRepository, SqliteLapRepository>();
        services.AddSingleton<ILapArchive, FileLapArchive>();
        services.AddSingleton<ITelemetryRepository, InfluxTelemetryRepository>();

        services.AddHostedService<LapStorageService>();
        services.AddHostedService<DatabaseMigrationService>();

        services.AddSingleton<InfluxDbWatchdogService>();
        services.AddHostedService(sp => sp.GetRequiredService<InfluxDbWatchdogService>());

        return services;
    }

    private static string ExpandHome(string value)
    {
        if (!value.Contains('~'))
        {
            return value;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return value.Replace("~", home);
    }
}
