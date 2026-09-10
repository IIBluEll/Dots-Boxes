using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DotsAndBoxes.Server.Accounts;

public sealed record ExternalUserResult(Guid UserId, string DisplayName);

/// <summary>Internal storage boundary. Call only AFTER Google server verification;
/// application ID must come from server configuration, never a client request.</summary>
public interface IExternalAccountService
{
    Task<ExternalUserResult> GetOrCreateExternalUser_async(
        string provider, string providerApplicationId, string providerPlayerId,
        string? verifiedDisplayName, CancellationToken cancellationToken = default);
}

public sealed class ExternalAccountService(IDbContextFactory<GameDbContext> contextFactory) : IExternalAccountService
{
    public const string GOOGLE_PLAY_GAMES = "GOOGLE_PLAY_GAMES";
    public const string DEFAULT_DISPLAY_NAME = "플레이어";
    private readonly IDbContextFactory<GameDbContext> CONTEXT_FACTORY = contextFactory;

    public async Task<ExternalUserResult> GetOrCreateExternalUser_async(
        string provider, string providerApplicationId, string providerPlayerId,
        string? verifiedDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerApplicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPlayerId);
        if (provider != GOOGLE_PLAY_GAMES)
            throw new ArgumentException("Unsupported verified identity provider.", nameof(provider));

        // Never trim/case-fold the opaque provider player ID.
        await using GameDbContext db = await CONTEXT_FACTORY.CreateDbContextAsync(cancellationToken);
        ExternalIdentity? existing = await FindIdentity_async(db, provider, providerApplicationId, providerPlayerId, cancellationToken);
        if (existing != null)
            return await UpdateName_async(db, existing.User, verifiedDisplayName, cancellationToken);

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            GameUser user = new()
            {
                UserId = Guid.NewGuid(),
                DisplayName = string.IsNullOrWhiteSpace(verifiedDisplayName) ? DEFAULT_DISPLAY_NAME : verifiedDisplayName,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.ExternalIdentities.Add(new ExternalIdentity
            {
                Provider = provider,
                ProviderApplicationId = providerApplicationId,
                ProviderPlayerId = providerPlayerId,
                User = user,
                UserId = user.UserId,
                CreatedAtUtc = now
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new ExternalUserResult(user.UserId, user.DisplayName);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_external_identities" })
            {
                // A competing request won. Roll back BOTH inserts, including the new user.
                // Rollback must still run if the request was cancelled in the meantime.
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
        }

        // The failed transaction is disposed before querying the committed winner.
        existing = await FindIdentity_async(db, provider, providerApplicationId, providerPlayerId, cancellationToken)
            ?? throw new InvalidOperationException("Concurrent identity disappeared; explicit account deletion is not supported.");
        return await UpdateName_async(db, existing.User, verifiedDisplayName, cancellationToken);
    }

    private static async Task<ExternalIdentity?> FindIdentity_async(GameDbContext db, string provider,
        string applicationId, string playerId, CancellationToken cancellationToken)
    {
        return await db.ExternalIdentities.Include(x => x.User).SingleOrDefaultAsync(x =>
            x.Provider == provider && x.ProviderApplicationId == applicationId && x.ProviderPlayerId == playerId,
            cancellationToken);
    }

    private static async Task<ExternalUserResult> UpdateName_async(GameDbContext db, GameUser user,
        string? displayName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(displayName) && displayName != user.DisplayName)
        {
            user.DisplayName = displayName;
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return new ExternalUserResult(user.UserId, user.DisplayName);
    }
}
