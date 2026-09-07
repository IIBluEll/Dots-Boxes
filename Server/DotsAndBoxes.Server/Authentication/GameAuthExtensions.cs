using System.Threading.RateLimiting;
using DotsAndBoxes.Server.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DotsAndBoxes.Server.Authentication;

public static class GameAuthExtensions
{
    public static IServiceCollection AddGameAuthentication(
        this IServiceCollection services ,
        IConfiguration configuration)
    {
        services.AddOptions<GooglePlayOptions>()
            .Bind(configuration.GetSection("GooglePlay"))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ClientSecret) &&
                    !string.IsNullOrWhiteSpace(options.ApplicationId) ,
                "GooglePlay server credentials are required.")
            .ValidateOnStart();

        services.AddOptions<GameJwtOptions>()
            .Bind(configuration.GetSection("GameJwt"))
            .Validate(options =>
            {
                options.CreateValidationParameters();
                return true;
            })
            .ValidateOnStart();

        // 비활성화 상태에서도 서버가 정상적으로 시작될 수 있도록
        // TestLogin 설정은 ValidateOnStart를 사용하지 않습니다.
        services.AddOptions<TestLoginOptions>()
            .Bind(configuration.GetSection(
                TestLoginOptions.SECTION_NAME));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<GameTokenService>();

        services.AddHttpClient<
            IGooglePlayAuthService ,
            GooglePlayAuthService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<GameJwtOptions>>(
                (options , jwtOptions) =>
                {
                    options.MapInboundClaims = false;
                    options.IncludeErrorDetails = false;

                    options.TokenValidationParameters =
                        jwtOptions.Value.CreateValidationParameters();

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            string? userIdClaim =
                                context.Principal?
                                    .FindFirst("sub")?
                                    .Value;

                            if ( !Guid.TryParse(
                                    userIdClaim ,
                                    out Guid userId) ||
                                userId == Guid.Empty )
                            {
                                context.Fail(
                                    "Invalid user identity.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

        services.AddAuthorization();

        services.AddSingleton<
            IUserIdProvider ,
            GameUserIdProvider>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            options.AddConcurrencyLimiter(
                "google-login" ,
                limiterOptions =>
                {
                    limiterOptions.PermitLimit = 16;
                    limiterOptions.QueueLimit = 0;
                    limiterOptions.QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst;
                });

            options.AddConcurrencyLimiter(
                "test-login" ,
                limiterOptions =>
                {
                    limiterOptions.PermitLimit = 4;
                    limiterOptions.QueueLimit = 0;
                    limiterOptions.QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst;
                });
        });

        return services;
    }

    public static void MapGameAuthentication(
        this WebApplication app)
    {
        MapGoogleLogin(app);
        MapTestLogin(app);
    }

    private static void MapGoogleLogin(
        WebApplication app)
    {
        app.MapPost(
                "/auth/google-play" ,
                async (
                    GoogleLoginRequest request ,
                    HttpContext context ,
                    IGooglePlayAuthService googlePlayAuthService ,
                    IExternalAccountService externalAccountService ,
                    GameTokenService gameTokenService ,
                    IOptions<GooglePlayOptions> googlePlayOptions ,
                    ILoggerFactory loggerFactory ,
                    CancellationToken cancellationToken) =>
                {
                    context.Response.Headers.CacheControl = "no-store";

                    if ( string.IsNullOrWhiteSpace(request.AuthCode) ||
                        request.AuthCode.Length > 8192 )
                    {
                        return Results.BadRequest(new
                        {
                            error = "invalid_request"
                        });
                    }

                    try
                    {
                        VerifiedGooglePlayer player =
                            await googlePlayAuthService.Verify_async(
                                request.AuthCode,
                                cancellationToken);

                        ExternalUserResult user =
                            await externalAccountService
                                .GetOrCreateExternalUser_async(
                                    ExternalAccountService
                                        .GOOGLE_PLAY_GAMES,
                                    googlePlayOptions.Value
                                        .ApplicationId,
                                    player.PlayerId,
                                    player.DisplayName,
                                    cancellationToken);

                        return Results.Ok(
                            gameTokenService.Issue(user));
                    }
                    catch ( GoogleAuthenticationException )
                    {
                        return Results.Unauthorized();
                    }
                    catch ( GoogleUnavailableException )
                    {
                        return Results.Json(
                            new
                            {
                                error = "google_unavailable"
                            } ,
                            statusCode:
                                StatusCodes
                                    .Status503ServiceUnavailable);
                    }
                    catch ( OperationCanceledException )
                        when ( cancellationToken
                            .IsCancellationRequested )
                    {
                        throw;
                    }
                    catch ( Exception exception )
                    {
                        loggerFactory
                            .CreateLogger("GameAuthentication")
                            .LogError(
                                "Login failed: {ErrorType}" ,
                                exception.GetType().Name);

                        return Results.Json(
                            new
                            {
                                error = "login_unavailable"
                            } ,
                            statusCode:
                                StatusCodes
                                    .Status503ServiceUnavailable);
                    }
                })
            .RequireRateLimiting("google-login")
            .WithMetadata(
                new RequestSizeLimitAttribute(16384));
    }

    private static void MapTestLogin(
        WebApplication app)
    {
        app.MapPost(
                "/auth/test-login" ,
                async (
                    HttpContext context ,
                    IOptions<TestLoginOptions> testLoginOptions ,
                    IDbContextFactory<GameDbContext> contextFactory ,
                    GameTokenService gameTokenService ,
                    ILoggerFactory loggerFactory ,
                    CancellationToken cancellationToken) =>
                {
                    context.Response.Headers.CacheControl = "no-store";

                    TestLoginOptions settings =
                        testLoginOptions.Value;

                    // 정식 출시 때 Enabled를 false로 바꾸면
                    // 존재하지 않는 API처럼 404를 반환합니다.
                    if ( !settings.Enabled )
                    {
                        return Results.NotFound();
                    }

                    if ( settings.UserId == Guid.Empty ||
                        string.IsNullOrWhiteSpace(
                            settings.DisplayName) )
                    {
                        return Results.Json(
                            new
                            {
                                error =
                                    "test_login_misconfigured"
                            } ,
                            statusCode:
                                StatusCodes
                                    .Status503ServiceUnavailable);
                    }

                    try
                    {
                        ExternalUserResult user =
                            await GetOrCreateTestUser_async(
                                contextFactory,
                                settings,
                                cancellationToken);

                        return Results.Ok(
                            gameTokenService.Issue(user));
                    }
                    catch ( OperationCanceledException )
                        when ( cancellationToken
                            .IsCancellationRequested )
                    {
                        throw;
                    }
                    catch ( Exception exception )
                    {
                        loggerFactory
                            .CreateLogger("TestLogin")
                            .LogError(
                                "Test login failed: {ErrorType}" ,
                                exception.GetType().Name);

                        return Results.Json(
                            new
                            {
                                error =
                                    "test_login_unavailable"
                            } ,
                            statusCode:
                                StatusCodes
                                    .Status503ServiceUnavailable);
                    }
                })
            .RequireRateLimiting("test-login")
            .WithMetadata(
                new RequestSizeLimitAttribute(1024));
    }

    private static async Task<ExternalUserResult>
        GetOrCreateTestUser_async(
            IDbContextFactory<GameDbContext> contextFactory ,
            TestLoginOptions settings ,
            CancellationToken cancellationToken)
    {
        await using GameDbContext db =
            await contextFactory.CreateDbContextAsync(
                cancellationToken);

        GameUser? user =
            await db.Users.SingleOrDefaultAsync(
                user => user.UserId == settings.UserId,
                cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if ( user == null )
        {
            user = new GameUser
            {
                UserId = settings.UserId ,
                DisplayName = settings.DisplayName ,
                CreatedAtUtc = now ,
                UpdatedAtUtc = now
            };

            db.Users.Add(user);
        }
        else if ( user.DisplayName != settings.DisplayName )
        {
            user.DisplayName = settings.DisplayName;
            user.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ExternalUserResult(
            user.UserId ,
            user.DisplayName);
    }
}

public sealed class GameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(
        HubConnectionContext connection)
    {
        bool isAuthenticated =
            connection.User?
                .Identity?
                .IsAuthenticated == true;

        string? userIdClaim =
            connection.User?
                .FindFirst("sub")?
                .Value;

        return isAuthenticated &&
               Guid.TryParse(
                   userIdClaim ,
                   out Guid userId) &&
               userId != Guid.Empty
            ? userId.ToString("D")
            : null;
    }
}