using System.Threading.RateLimiting;
using DotsAndBoxes.Server.Accounts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DotsAndBoxes.Server.Authentication;

public static class GameAuthExtensions
{
    public static IServiceCollection AddGameAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GooglePlayOptions>().Bind(configuration.GetSection("GooglePlay"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId) && !string.IsNullOrWhiteSpace(o.ClientSecret) &&
                !string.IsNullOrWhiteSpace(o.ApplicationId), "GooglePlay server credentials are required.").ValidateOnStart();
        services.AddOptions<GameJwtOptions>().Bind(configuration.GetSection("GameJwt"))
            .Validate(o => { o.CreateValidationParameters(); return true; }).ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<GameTokenService>();
        services.AddHttpClient<IGooglePlayAuthService, GooglePlayAuthService>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<GameJwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = jwt.Value.CreateValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out Guid id) || id == Guid.Empty)
                            context.Fail("Invalid user identity.");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();
        services.AddSingleton<IUserIdProvider, GameUserIdProvider>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Global bound also works behind an existing reverse proxy without trusting forwarded headers.
            options.AddConcurrencyLimiter("google-login", limiter =>
            { limiter.PermitLimit = 16; limiter.QueueLimit = 0; limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; });
        });
        return services;
    }

    public static void MapGameAuthentication(this WebApplication app)
    {
        app.MapPost("/auth/google-play", async (GoogleLoginRequest request, HttpContext context,
            IGooglePlayAuthService google, IExternalAccountService accounts, GameTokenService tokens,
            IOptions<GooglePlayOptions> settings, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(request.AuthCode) || request.AuthCode.Length > 8192)
                return Results.BadRequest(new { error = "invalid_request" });
            try
            {
                VerifiedGooglePlayer player = await google.Verify_async(request.AuthCode, cancellationToken);
                ExternalUserResult user = await accounts.GetOrCreateExternalUser_async(
                    ExternalAccountService.GOOGLE_PLAY_GAMES, settings.Value.ApplicationId,
                    player.PlayerId, player.DisplayName, cancellationToken);
                return Results.Ok(tokens.Issue(user));
            }
            catch (GoogleAuthenticationException) { return Results.Unauthorized(); }
            catch (GoogleUnavailableException)
            { return Results.Json(new { error = "google_unavailable" }, statusCode: 503); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // Do not log payloads, codes, tokens, connection strings, or exception messages.
                loggerFactory.CreateLogger("GameAuthentication").LogError("Login failed: {ErrorType}", exception.GetType().Name);
                return Results.Json(new { error = "login_unavailable" }, statusCode: 503);
            }
        }).RequireRateLimiting("google-login").WithMetadata(new RequestSizeLimitAttribute(16384));
    }
}

public sealed class GameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(connection.User.FindFirst("sub")?.Value, out Guid userId) && userId != Guid.Empty
            ? userId.ToString("D") : null;
    }
}
