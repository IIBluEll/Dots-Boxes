using Microsoft.EntityFrameworkCore;

namespace DotsAndBoxes.Server.Accounts;

public static class AccountServiceCollectionExtensions
{
    public static IServiceCollection AddGameAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<GameDbContext>(options =>
        {
            // Lazy validation preserves existing game-only startup without DB configuration.
            string connectionString = configuration.GetConnectionString("GameDatabase")
                ?? throw new InvalidOperationException("ConnectionStrings:GameDatabase is required for account storage.");
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", GameDbContext.SCHEMA_NAME));
        });
        services.AddScoped<IExternalAccountService, ExternalAccountService>();
        return services;
    }
}
