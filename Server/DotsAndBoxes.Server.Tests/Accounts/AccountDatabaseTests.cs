using System.Data.Common;
using DotsAndBoxes.Server.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DotsAndBoxes.Server.Tests.Accounts;

[TestFixture]
[NonParallelizable]
public sealed class AccountDatabaseTests
{
    private string _runtime = string.Empty;
    private string _migration = string.Empty;
    private string _application = string.Empty;

    [OneTimeSetUp]
    public void CheckTestDatabase()
    {
        _runtime = Environment.GetEnvironmentVariable("GAME_DATABASE_TEST_RUNTIME") ?? string.Empty;
        _migration = Environment.GetEnvironmentVariable("GAME_DATABASE_TEST_MIGRATION") ?? string.Empty;
        if (_runtime.Length == 0 || _migration.Length == 0)
        {
            Assert.Ignore("PostgreSQL integration tests require explicit test-DB credentials.");
            return;
        }
        // Refuse all production DB names even if somebody supplies a wrong environment file.
        Assert.That(new NpgsqlConnectionStringBuilder(_runtime).Database, Is.EqualTo("dotsandboxes_test"));
        Assert.That(new NpgsqlConnectionStringBuilder(_migration).Database, Is.EqualTo("dotsandboxes_test"));
    }

    [SetUp]
    public void NewApplicationScope() => _application = "integration-" + Guid.NewGuid().ToString("N");

    [Test]
    public async Task MigrationIsIdempotent_async()
    {
        await using GameDbContext db = CreateContext(_migration);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.That(await db.Database.GetPendingMigrationsAsync(), Is.Empty);
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
    }

    [Test]
    public async Task FirstAndRepeatedLoginPreserveIdentityAndUnicode_async()
    {
        ExternalAccountService service = CreateService();
        ExternalUserResult first = await Login_async(service, "Opaque-AbC-001", "한글 🎮");
        ExternalUserResult repeated = await Login_async(service, "Opaque-AbC-001", "한글 🎮");
        await using GameDbContext db = CreateContext(_runtime);
        Assert.That(first.UserId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(repeated, Is.EqualTo(first));
        Assert.That(await db.ExternalIdentities.CountAsync(x => x.ProviderApplicationId == _application), Is.EqualTo(1));
        Assert.That(await db.Users.CountAsync(x => x.UserId == first.UserId), Is.EqualTo(1));
    }

    [Test]
    public async Task NicknameAndApplicationDoNotMergeAccounts_async()
    {
        ExternalAccountService service = CreateService();
        var first = await Login_async(service, "AbC001", "같은 닉네임");
        var second = await Login_async(service, "abc001", "같은 닉네임");
        var otherApp = await service.GetOrCreateExternalUser_async(ExternalAccountService.GOOGLE_PLAY_GAMES,
            _application + "-other", "AbC001", "같은 닉네임");
        Assert.That(new[] { first.UserId, second.UserId, otherApp.UserId }.Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task RenamePreservesCreatedTimeAndMissingNamePreservesProfile_async()
    {
        ExternalAccountService service = CreateService();
        var first = await Login_async(service, "rename", "원래 이름");
        await using GameDbContext before = CreateContext(_runtime);
        var original = await before.Users.AsNoTracking().SingleAsync(x => x.UserId == first.UserId);
        var renamed = await Login_async(service, "rename", "변경된 이름 🎲");
        var missing = await Login_async(service, "rename", " \t\u3000");
        await using GameDbContext after = CreateContext(_runtime);
        var actual = await after.Users.SingleAsync(x => x.UserId == first.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(renamed.UserId, Is.EqualTo(first.UserId));
            Assert.That(missing.DisplayName, Is.EqualTo("변경된 이름 🎲"));
            Assert.That(actual.CreatedAtUtc, Is.EqualTo(original.CreatedAtUtc));
            Assert.That(actual.UpdatedAtUtc, Is.GreaterThanOrEqualTo(original.UpdatedAtUtc));
        });
        Assert.That((await Login_async(service, "no-profile", null)).DisplayName,
            Is.EqualTo(ExternalAccountService.DEFAULT_DISPLAY_NAME));
    }

    [Test]
    public async Task ForcedConcurrentInsertRollsBackLosersAndConverges_async()
    {
        const int REQUEST_COUNT = 8;
        RaceBarrier barrier = new(REQUEST_COUNT);
        UniqueFailureCounter failures = new();
        ExternalAccountService service = CreateService(barrier, failures);
        var replies = await Task.WhenAll(Enumerable.Range(0, REQUEST_COUNT)
            .Select(_ => Login_async(service, "race", "동시 요청")));
        await using GameDbContext db = CreateContext(_runtime);
        Assert.That(replies.Select(x => x.UserId).Distinct().Count(), Is.EqualTo(1));
        Assert.That(failures.Count, Is.EqualTo(REQUEST_COUNT - 1), "The unique-conflict recovery path must actually execute.");
        Assert.That(await db.ExternalIdentities.CountAsync(x => x.ProviderApplicationId == _application), Is.EqualTo(1));
        Assert.That(await db.Users.CountAsync(x => !db.ExternalIdentities.Any(i => i.UserId == x.UserId)), Is.Zero);
    }

    [Test]
    public async Task CancellationAfterInsertsRollsBackUserAndIdentity_async()
    {
        ExternalAccountService service = CreateService(new CancelAfterSave());
        Assert.ThrowsAsync<OperationCanceledException>(async () => await Login_async(service, "cancel", "취소"));
        await using GameDbContext db = CreateContext(_runtime);
        Assert.That(await db.ExternalIdentities.CountAsync(x => x.ProviderApplicationId == _application), Is.Zero);
        Assert.That(await db.Users.CountAsync(x => !db.ExternalIdentities.Any(i => i.UserId == x.UserId)), Is.Zero);
    }

    [Test]
    public void BlankProviderFieldsAreRejected()
    {
        var service = CreateService();
        Assert.ThrowsAsync<ArgumentException>(async () => await service.GetOrCreateExternalUser_async(" ", _application, "id", "name"));
        Assert.ThrowsAsync<ArgumentException>(async () => await service.GetOrCreateExternalUser_async(ExternalAccountService.GOOGLE_PLAY_GAMES, "\t", "id", "name"));
        Assert.ThrowsAsync<ArgumentException>(async () => await Login_async(service, "\u3000", "name"));
    }

    [Test]
    public async Task DatabaseConstraintsAndForeignKeyRestrictInvalidWrites_async()
    {
        await using GameDbContext db = CreateContext(_runtime);
        Guid id = Guid.NewGuid();
        await RejectSql_async(db, "23514", "INSERT INTO accounts.users VALUES ('00000000-0000-0000-0000-000000000000', 'name', now(), now())");
        await RejectSql_async(db, "23514", $"INSERT INTO accounts.users VALUES ('{id}', chr(9)||chr(160)||chr(12288), now(), now())");
        await RejectSql_async(db, "23503", $"INSERT INTO accounts.external_identities VALUES ('GOOGLE_PLAY_GAMES', '{_application}', 'missing', '{id}', now())");
        var user = await Login_async(CreateService(), "valid", "이름");
        foreach (string column in new[] { "provider", "provider_application_id", "provider_player_id" })
            await RejectSql_async(db, "23514", $"UPDATE accounts.external_identities SET {column} = chr(9)||chr(12288) WHERE user_id = '{user.UserId}'");
        await RejectSql_async(db, "23503", $"DELETE FROM accounts.users WHERE user_id = '{user.UserId}'");
    }

    [Test]
    public async Task RuntimeRoleCanUseAccountsButCannotChangeSchema_async()
    {
        await using GameDbContext db = CreateContext(_runtime);
        await Login_async(CreateService(), "permission", "런타임 계정");
        await RejectSql_async(db, "42501", "CREATE TABLE accounts.forbidden_probe (id integer)");
        await RejectSql_async(db, "42501", "CREATE TABLE public.forbidden_probe (id integer)");
        await RejectSql_async(db, "42501", "ALTER TABLE accounts.users ADD COLUMN forbidden integer");
        await RejectSql_async(db, "42501", "SELECT * FROM accounts.\"__EFMigrationsHistory\"");
        await using var connection = new NpgsqlConnection(_runtime);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname = current_user", connection);
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(false));
    }

    [Test]
    public async Task DependencyInjectionResolvesRuntimeService_async()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:GameDatabase"] = _runtime }).Build();
        var services = new ServiceCollection();
        services.AddGameAccounts(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IExternalAccountService>()
            .GetOrCreateExternalUser_async(ExternalAccountService.GOOGLE_PLAY_GAMES, _application, "di", "DI 사용자");
        Assert.That(result.UserId, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task ProcessPersistenceProbe_async()
    {
        // Intentionally stable test-only key. Run in two independent dotnet test processes.
        _application = "integration-process-restart";
        await using GameDbContext db = CreateContext(_runtime);
        var before = await db.ExternalIdentities.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ProviderApplicationId == _application && x.ProviderPlayerId == "persistent-player");
        var result = await Login_async(CreateService(), "persistent-player", "재실행 확인");
        if (before != null) Assert.That(result.UserId, Is.EqualTo(before.UserId));
        TestContext.Progress.WriteLine($"PERSISTENCE_PROBE existing={before != null} UserId={result.UserId}");
    }

    private Task<ExternalUserResult> Login_async(ExternalAccountService service, string player, string? display) =>
        service.GetOrCreateExternalUser_async(ExternalAccountService.GOOGLE_PLAY_GAMES, _application, player, display);

    private GameDbContext CreateContext(string connection) => new(BuildOptions(connection));

    private static DbContextOptions<GameDbContext> BuildOptions(string connection, params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(connection, options =>
            options.MigrationsHistoryTable("__EFMigrationsHistory", GameDbContext.SCHEMA_NAME))
            .AddInterceptors(interceptors).Options;

    private ExternalAccountService CreateService(params IInterceptor[] interceptors) =>
        new(new ContextFactory(BuildOptions(_runtime, interceptors)));

    private static async Task RejectSql_async(GameDbContext db, string sqlState, string sql)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); Assert.Fail("Invalid operation unexpectedly succeeded."); }
        catch (PostgresException exception) { Assert.That(exception.SqlState, Is.EqualTo(sqlState)); }
    }

    private sealed class ContextFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext() => new(options);
    }

    private sealed class RaceBarrier(int participants) : DbCommandInterceptor
    {
        private int _arrived;
        private readonly TaskCompletionSource RELEASE = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("external_identities"))
            {
                int arrived = Interlocked.Increment(ref _arrived);
                if (arrived == participants) RELEASE.TrySetResult();
                if (arrived <= participants) await RELEASE.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class UniqueFailureCounter : SaveChangesInterceptor
    {
        private int _count;
        public int Count => _count;
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } })
                Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }
    }

    private sealed class CancelAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new OperationCanceledException("Injected cancellation after inserts, before commit.");
    }
}
