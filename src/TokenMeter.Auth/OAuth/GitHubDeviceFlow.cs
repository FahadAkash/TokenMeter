using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TokenMeter.Auth.OAuth;

public sealed class GitHubDeviceFlow
{
    private const string ClientId = "Iv1.b507a08c87ecfe98"; // Public VS Code Client ID
    private const string Scopes = "read:user";

    private readonly HttpClient _httpClient;

    public GitHubDeviceFlow(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public record DeviceCodeResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval
    );

    public record AccessTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("scope")] string Scope
    );

    public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code");
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("scope", Scopes)
        });
        request.Content = content;

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken: ct)
            ?? throw new Exception("Failed to decode device code response.");
    }

    public async Task<string?> PollForTokenAsync(DeviceCodeResponse deviceCode, Action<string>? onStep = null, CancellationToken ct = default)
    {
        var url = "https://github.com/login/oauth/access_token";
        var interval = deviceCode.Interval > 0 ? deviceCode.Interval : 5;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("device_code", deviceCode.DeviceCode),
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
            });
            request.Content = content;

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) continue;

            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);

            if (json.TryGetProperty("error", out var errorElem))
            {
                var error = errorElem.GetString();
                if (error == "authorization_pending")
                {
                    onStep?.Invoke("Waiting for approval in browser...");
                    continue;
                }
                if (error == "slow_down")
                {
                    interval += 5;
                    continue;
                }
                if (error == "expired_token")
                {
                    throw new TimeoutException("The device code has expired.");
                }
                throw new Exception($"OAuth error: {error}");
            }

            if (json.TryGetProperty("access_token", out var tokenElem))
            {
                return tokenElem.GetString();
            }
        }

        return null;
    }
}
