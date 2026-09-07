using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotsAndBoxes.Server.Accounts;
using DotsAndBoxes.Server.Authentication;
using DotsAndBoxes.Server.Matchmaking;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace DotsAndBoxes.Server.Tests.Authentication;

// Google and account storage are isolated here; existing AccountDatabaseTests cover PostgreSQL.
public sealed class GameAuthenticationTests
{
    private AuthFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new AuthFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown() { _client.Dispose(); _factory.Dispose(); }

    [Test]
    public async Task Login_UsesVerifiedIdentity_AndReloginPreservesUserId()
    {
        GameLoginResponse first = await Login_async("first");
        GameLoginResponse second = await Login_async("second");
        Assert.That(second.UserId, Is.EqualTo(first.UserId));
        Assert.That(second.AccessToken, Is.Not.EqualTo(first.AccessToken));
        Assert.That(first.DisplayName, Is.EqualTo("테스트 플레이어"));
        Assert.That(_factory.Accounts.Users.Count, Is.EqualTo(1));
        Assert.That(_factory.Accounts.Users.ContainsKey("trusted-player"), Is.True);
        Assert.That(_factory.Google.Codes, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public async Task Login_ClientSuppliedIdentityDoesNotAuthenticateOrOverrideGoogle()
    {
        using HttpResponseMessage noCode = await _client.PostAsJsonAsync("/auth/google-play", new { playerId = "forged" });
        Assert.That(noCode.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using HttpResponseMessage valid = await _client.PostAsJsonAsync("/auth/google-play",
            new { authCode = "valid", playerId = "forged", displayName = "forged", applicationId = "forged" });
        Assert.That(valid.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.Accounts.Users.ContainsKey("forged"), Is.False);
        Assert.That(_factory.Accounts.Users.ContainsKey("trusted-player"), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Login_EmptyCodeRejected(string code)
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/auth/google-play", new { authCode = code });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(_factory.Accounts.Users, Is.Empty);
    }

    [TestCase("invalid", HttpStatusCode.Unauthorized)]
    [TestCase("bad-config", HttpStatusCode.ServiceUnavailable)]
    [TestCase("wrong-game", HttpStatusCode.Unauthorized)]
    [TestCase("profile-mismatch", HttpStatusCode.Unauthorized)]
    [TestCase("provider-offline-code", HttpStatusCode.ServiceUnavailable)]
    public async Task Login_FailureDoesNotCreateAccountOrExposeCode(string code, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/auth/google-play", new { authCode = code });
        Assert.That(response.StatusCode, Is.EqualTo(expected));
        Assert.That(_factory.Accounts.Users, Is.Empty);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain(code));
    }

    [Test]
    public async Task Login_UsedCodeRejected_FreshCodeAllowsRelogin()
    {
        await Login_async("once");
        using HttpResponseMessage replay = await _client.PostAsJsonAsync("/auth/google-play", new { authCode = "once" });
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await Login_async("fresh");
        Assert.That(_factory.Accounts.Users.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Login_ProfileFailureStillPassesVerifiedIdentityToStorage()
    {
        await Login_async("profile-failure");
        Assert.That(_factory.Accounts.LastName, Is.Null);
        Assert.That(_factory.Accounts.Users.Count, Is.EqualTo(1));
    }

    [TestCase("expired")]
    [TestCase("wrong-key")]
    [TestCase("wrong-audience")]
    [TestCase("wrong-issuer")]
    [TestCase("empty-user")]
    [TestCase("malformed")]
    [TestCase("missing")]
    public async Task Hub_RejectsInvalidToken_EvenWithUserIdQuery(string mode)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/hubs/game/negotiate?negotiateVersion=1&userId={Guid.NewGuid()}");
        if (mode != "missing") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(mode));
        using HttpResponseMessage response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Hub_UsesJwtSubject_NotForgedQueryUserId()
    {
        GameLoginResponse login = await Login_async("hub");
        await using HubConnection connection = new HubConnectionBuilder().WithUrl(
            new Uri(_client.BaseAddress!, $"/hubs/game?userId={Guid.NewGuid()}"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(login.AccessToken);
            }).Build();
        await connection.StartAsync();
        // Entering the queue is allowed only when a valid authenticated Guid was resolved.
        MatchAssignment? assignment = await connection.InvokeAsync<MatchAssignment?>("EnterMatchmaking");
        Assert.That(assignment, Is.Null);
        MatchmakingQueue queue = _factory.Services.GetRequiredService<MatchmakingQueue>();
        Assert.That(queue.Enqueue(login.UserId).State, Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.ALREADY_QUEUED));
        Assert.That(await connection.InvokeAsync<bool>("CancelMatchmaking"), Is.True);
    }

    private async Task<GameLoginResponse> Login_async(string code)
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/auth/google-play", new { authCode = code });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        return (await response.Content.ReadFromJsonAsync<GameLoginResponse>())!;
    }

    private static string CreateToken(string mode)
    {
        if (mode == "malformed") return "not-a-token";
        GameJwtOptions options = AuthFactory.CreateJwtOptions();
        if (mode == "wrong-key") options.SigningKey = new string('z', 48);
        if (mode == "wrong-audience") options.Audience = "wrong";
        if (mode == "wrong-issuer") options.Issuer = "wrong";
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(options.Issuer, options.Audience,
            new[] { new Claim("sub", mode == "empty-user" ? Guid.Empty.ToString() : Guid.NewGuid().ToString()) },
            now.AddHours(-2), mode == "expired" ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(options.CreateValidationParameters().IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class AuthFactory : WebApplicationFactory<Program>
    {
        public FakeAccounts Accounts { get; } = new();
        public GoogleHandler Google { get; } = new();
        public static GameJwtOptions CreateJwtOptions() => new() { SigningKey = new string('x', 48) };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<GooglePlayOptions>(o =>
                { o.ApplicationId = "test-game"; o.ClientId = "test-client"; o.ClientSecret = "test-only-secret"; });
                services.PostConfigure<GameJwtOptions>(o => o.SigningKey = CreateJwtOptions().SigningKey);
                services.RemoveAll<IExternalAccountService>();
                services.AddSingleton<IExternalAccountService>(Accounts);
                services.AddHttpClient<IGooglePlayAuthService, GooglePlayAuthService>()
                    .ConfigurePrimaryHttpMessageHandler(() => Google);
            });
        }
    }

    private sealed class FakeAccounts : IExternalAccountService
    {
        public ConcurrentDictionary<string, Guid> Users { get; } = new();
        public string? LastName { get; private set; }
        public Task<ExternalUserResult> GetOrCreateExternalUser_async(string provider, string providerApplicationId,
            string providerPlayerId, string? verifiedDisplayName, CancellationToken cancellationToken = default)
        {
            Assert.That(provider, Is.EqualTo(ExternalAccountService.GOOGLE_PLAY_GAMES));
            Assert.That(providerApplicationId, Is.EqualTo("test-game"));
            LastName = verifiedDisplayName;
            return Task.FromResult(new ExternalUserResult(Users.GetOrAdd(providerPlayerId, _ => Guid.NewGuid()), verifiedDisplayName ?? "플레이어"));
        }
    }

    private sealed class GoogleHandler : HttpMessageHandler
    {
        public List<string> Codes { get; } = new();
        private string _currentCode = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "oauth2.googleapis.com")
            {
                string body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var form = body.Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1].Replace("+", " ")));
                Assert.That(form["client_id"], Is.EqualTo("test-client"));
                Assert.That(form["client_secret"], Is.EqualTo("test-only-secret"));
                _currentCode = form["code"];
                if (Codes.Contains(_currentCode) || _currentCode == "invalid") return Json(400, new { error = "invalid_grant" });
                Codes.Add(_currentCode);
                if (_currentCode == "bad-config") return Json(400, new { error = "invalid_client" });
                if (_currentCode == "provider-offline-code") return Json(503, new { error = "unavailable" });
                return Json(200, new { access_token = "google-test-token" });
            }
            Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("google-test-token"));
            if (request.RequestUri.AbsolutePath.EndsWith("/verify"))
            {
                Assert.That(request.RequestUri.AbsolutePath, Does.Contain("/applications/test-game/"));
                return _currentCode == "wrong-game" ? Json(403, new { error = "forbidden" }) : Json(200, new { player_id = "trusted-player" });
            }
            if (_currentCode == "profile-failure") return Json(503, new { error = "unavailable" });
            return Json(200, new { playerId = _currentCode == "profile-mismatch" ? "other" : "trusted-player", displayName = "테스트 플레이어" });
        }

        private static HttpResponseMessage Json(int status, object body) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
    }
}
