using Microsoft.EntityFrameworkCore;

namespace DotsAndBoxes.Server.Accounts;

public sealed record MatchDisplayNames(
    string PlayerOneDisplayName ,
    string PlayerTwoDisplayName);

public interface IPlayerProfileService
{
    Task<MatchDisplayNames> GetMatchDisplayNames_async(
        Guid playerOneUserId ,
        Guid playerTwoUserId);
}

public sealed class PlayerProfileService : IPlayerProfileService
{
    private const string DEFAULT_DISPLAY_NAME = "플레이어";

    private readonly IDbContextFactory<GameDbContext> CONTEXT_FACTORY;
    private readonly ILogger<PlayerProfileService> LOGGER;

    public PlayerProfileService(
        IDbContextFactory<GameDbContext> contextFactory ,
        ILogger<PlayerProfileService> logger)
    {
        CONTEXT_FACTORY = contextFactory ??
            throw new ArgumentNullException(nameof(contextFactory));

        LOGGER = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MatchDisplayNames> GetMatchDisplayNames_async(
        Guid playerOneUserId ,
        Guid playerTwoUserId)
    {
        if ( playerOneUserId == Guid.Empty )
        {
            throw new ArgumentException(
                "플레이어 1 UserId가 비어 있습니다." ,
                nameof(playerOneUserId));
        }

        if ( playerTwoUserId == Guid.Empty )
        {
            throw new ArgumentException(
                "플레이어 2 UserId가 비어 있습니다." ,
                nameof(playerTwoUserId));
        }

        // 표시용 정보 조회가 매칭을 오래 지연시키지 않도록 제한합니다.
        using CancellationTokenSource timeoutSource =
            new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            await using GameDbContext db =
                await CONTEXT_FACTORY.CreateDbContextAsync(
                    timeoutSource.Token);

            var users = await db.Users
                .AsNoTracking()
                .Where(user =>
                    user.UserId == playerOneUserId ||
                    user.UserId == playerTwoUserId)
                .Select(user => new
                {
                    user.UserId,
                    user.DisplayName
                })
                .ToListAsync(timeoutSource.Token);

            string? playerOneName = users
                .FirstOrDefault(user => user.UserId == playerOneUserId)
                ?.DisplayName;

            string? playerTwoName = users
                .FirstOrDefault(user => user.UserId == playerTwoUserId)
                ?.DisplayName;

            return new MatchDisplayNames(
                GetDisplayName(playerOneName) ,
                GetDisplayName(playerTwoName));
        }
        catch ( OperationCanceledException )
            when ( timeoutSource.IsCancellationRequested )
        {
            LOGGER.LogWarning(
                "Match profile lookup timed out. Using default names.");
        }
        catch ( Exception exception )
        {
            // 연결 문자열 등 민감한 정보가 포함될 수 있어
            // 예외 메시지 전체는 기록하지 않습니다.
            LOGGER.LogWarning(
                "Match profile lookup failed. ErrorType={ErrorType}" ,
                exception.GetType().Name);
        }

        return new MatchDisplayNames(
            DEFAULT_DISPLAY_NAME ,
            DEFAULT_DISPLAY_NAME);
    }

    private static string GetDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? DEFAULT_DISPLAY_NAME
            : displayName;
    }
}