using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotsAndBoxes.Server.Accounts;

public sealed class GameDbContextDesignFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        // Separate administrative setting; runtime credentials never receive DDL rights.
        string connectionString = Environment.GetEnvironmentVariable("GameDatabaseMigration")
            ?? "Host=server-db;Database=dotsandboxes;Username=NOT_CONFIGURED";
        return new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", GameDbContext.SCHEMA_NAME)).Options);
    }
}
