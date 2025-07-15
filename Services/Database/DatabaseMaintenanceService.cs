using CijeneScraper.Data;
using Microsoft.EntityFrameworkCore;

public class DatabaseMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseMaintenanceService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PerformMaintenanceAsync();
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task PerformMaintenanceAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Analyze tables for optimal query planning
        await dbContext.Database.ExecuteSqlRawAsync("ANALYZE");

        // Reindex if needed (for heavily updated tables)
        await dbContext.Database.ExecuteSqlRawAsync("REINDEX TABLE \"Prices\"");
    }
}