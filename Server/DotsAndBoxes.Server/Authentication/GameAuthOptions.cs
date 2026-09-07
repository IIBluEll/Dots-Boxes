using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DotsAndBoxes.Server.Authentication;

public sealed class GooglePlayOptions
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class GameJwtOptions
{
    public string Issuer { get; set; } = "DotsAndBoxes.Server";
    public string Audience { get; set; } = "DotsAndBoxes.Game";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;

    public TokenValidationParameters CreateValidationParameters()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32 ||
            string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience) ||
            LifetimeMinutes < 1 || LifetimeMinutes > 1440)
            throw new InvalidOperationException("GameJwt configuration is missing or invalid. Inject a random SigningKey of at least 32 bytes via server secrets.");

        return new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Issuer,
            ValidateAudience = true, ValidAudience = Audience,
            ValidateLifetime = true, RequireExpirationTime = true,
            ValidateIssuerSigningKey = true, RequireSignedTokens = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub"
        };
    }
}

public sealed record GoogleLoginRequest(string? AuthCode);
public sealed record GameLoginResponse(Guid UserId, string DisplayName, string AccessToken, DateTimeOffset ExpiresAtUtc);
public sealed record VerifiedGooglePlayer(string PlayerId, string? DisplayName);

public sealed class GoogleAuthenticationException : Exception
{
    public GoogleAuthenticationException() : base("Google authentication was rejected.") { }
}

public sealed class GoogleUnavailableException : Exception
{
    public GoogleUnavailableException() : base("Google authentication is temporarily unavailable.") { }
}
