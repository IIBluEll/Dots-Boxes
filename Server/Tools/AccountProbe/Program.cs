using DotsAndBoxes.Server.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Read-only runtime probe: no login, migration, insert or schema mutation.
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var services = new ServiceCollection();
services.AddGameAccounts(configuration);
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
_ = scope.ServiceProvider.GetRequiredService<IExternalAccountService>();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
await using var db = await factory.CreateDbContextAsync();
int users = await db.Users.CountAsync();
int identities = await db.ExternalIdentities.CountAsync();
Console.WriteLine($"Runtime account DI and read access OK. Users={users}; ExternalIdentities={identities}");
