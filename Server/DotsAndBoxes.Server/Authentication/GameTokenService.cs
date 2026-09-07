using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DotsAndBoxes.Server.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DotsAndBoxes.Server.Authentication;

public sealed class GameTokenService(IOptions<GameJwtOptions> options, TimeProvider timeProvider)
{
    private readonly GameJwtOptions OPTIONS = options.Value;
    private readonly TimeProvider TIME_PROVIDER = timeProvider;

    public GameLoginResponse Issue(ExternalUserResult user)
    {
        if (user.UserId == Guid.Empty) throw new ArgumentException("UserId is empty.", nameof(user));
        DateTimeOffset now = TIME_PROVIDER.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(OPTIONS.LifetimeMinutes);
        JwtSecurityToken token = new(OPTIONS.Issuer, OPTIONS.Audience,
            new[] { new Claim("sub", user.UserId.ToString("D")), new Claim("jti", Guid.NewGuid().ToString("N")) },
            now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(OPTIONS.CreateValidationParameters().IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        return new GameLoginResponse(user.UserId, user.DisplayName, new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
