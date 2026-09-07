using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DotsAndBoxes.Server.Authentication;

public interface IGooglePlayAuthService
{
    Task<VerifiedGooglePlayer> Verify_async(string authCode, CancellationToken cancellationToken);
}

public sealed class GooglePlayAuthService(HttpClient httpClient, IOptions<GooglePlayOptions> options) : IGooglePlayAuthService
{
    private readonly HttpClient HTTP_CLIENT = httpClient;
    private readonly GooglePlayOptions OPTIONS = options.Value;

    public async Task<VerifiedGooglePlayer> Verify_async(string authCode, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage exchange = new(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code", ["code"] = authCode,
                    ["client_id"] = OPTIONS.ClientId, ["client_secret"] = OPTIONS.ClientSecret,
                    ["redirect_uri"] = string.Empty
                })
            };
            using HttpResponseMessage tokenResponse = await HTTP_CLIENT.SendAsync(exchange, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                // Invalid client credentials are a server configuration problem, not a bad player.
                if (tokenResponse.StatusCode == HttpStatusCode.BadRequest)
                {
                    using JsonDocument error = await ReadJson_async(tokenResponse, cancellationToken);
                    if (ReadString(error.RootElement, "error") == "invalid_grant")
                        throw new GoogleAuthenticationException();
                }
                throw new GoogleUnavailableException();
            }
            using JsonDocument token = await ReadJson_async(tokenResponse, cancellationToken);
            string accessToken = ReadString(token.RootElement, "access_token") ?? throw new GoogleUnavailableException();

            // Application ID is trusted server configuration. No client player ID is accepted.
            using HttpResponseMessage verification = await Get_async(
                $"https://games.googleapis.com/games/v1/applications/{Uri.EscapeDataString(OPTIONS.ApplicationId)}/verify",
                accessToken, cancellationToken);
            if (verification.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new GoogleAuthenticationException();
            if (!verification.IsSuccessStatusCode) throw new GoogleUnavailableException();
            using JsonDocument identity = await ReadJson_async(verification, cancellationToken);
            string playerId = ReadString(identity.RootElement, "player_id") ?? throw new GoogleAuthenticationException();

            // Profile is optional: storage preserves the previous name or uses its default.
            string? displayName = null;
            using HttpResponseMessage profile = await Get_async(
                $"https://games.googleapis.com/games/v1/players/{Uri.EscapeDataString(playerId)}",
                accessToken, cancellationToken);
            if (profile.IsSuccessStatusCode)
            {
                using JsonDocument data = await ReadJson_async(profile, cancellationToken);
                if (ReadString(data.RootElement, "playerId") != playerId)
                    throw new GoogleAuthenticationException();
                displayName = ReadString(data.RootElement, "displayName");
            }
            return new VerifiedGooglePlayer(playerId, displayName);
        }
        catch (HttpRequestException) { throw new GoogleUnavailableException(); }
        catch (JsonException) { throw new GoogleUnavailableException(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new GoogleUnavailableException(); }
    }

    private async Task<HttpResponseMessage> Get_async(string url, string token, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await HTTP_CLIENT.SendAsync(request, cancellationToken);
    }

    private static async Task<JsonDocument> ReadJson_async(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
    }
}
