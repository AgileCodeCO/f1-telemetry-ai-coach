using F1Telemetry.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Storage.Services;

internal sealed partial class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        F1TelemetryDbContext db = scope.ServiceProvider.GetRequiredService<F1TelemetryDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        LogDatabaseReady(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "SQLite database schema verified")]
    private static partial void LogDatabaseReady(ILogger logger);
}
