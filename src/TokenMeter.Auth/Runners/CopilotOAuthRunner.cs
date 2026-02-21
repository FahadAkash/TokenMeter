using System.Diagnostics;
using TokenMeter.Core.Models;
using TokenMeter.Auth.OAuth;

namespace TokenMeter.Auth.Runners;

public sealed class CopilotOAuthRunner : IAuthRunner
{
    public UsageProvider Provider => UsageProvider.Copilot;
    public string DisplayName => "GitHub Device Flow (OAuth)";

    private readonly GitHubDeviceFlow _flow;
    private readonly ITokenStore _tokenStore;

    public CopilotOAuthRunner(GitHubDeviceFlow flow, ITokenStore tokenStore)
    {
        _flow = flow;
        _tokenStore = tokenStore;
    }

    public async Task<bool> AuthenticateAsync(Action<string> statusLogger, CancellationToken ct = default)
    {
        var token = await LoginAsync(statusLogger, ct);
        return !string.IsNullOrEmpty(token);
    }

    private async Task<string?> LoginAsync(Action<string> logger, CancellationToken ct = default)
    {
        try
        {
            logger("Requesting device code from GitHub...");
            var deviceCode = await _flow.RequestDeviceCodeAsync(ct);

            logger($"AUTHENTICATION CODE: {deviceCode.UserCode}");

            // Open browser
            Process.Start(new ProcessStartInfo(deviceCode.VerificationUri) { UseShellExecute = true });

            logger($"Enter code {deviceCode.UserCode} in your browser. Polling for authorization...");
            var token = await _flow.PollForTokenAsync(deviceCode, msg => logger($"{msg} (CODE: {deviceCode.UserCode})"), ct);

            if (!string.IsNullOrEmpty(token))
            {
                await _tokenStore.SetAsync("copilot_token", token, ct);
                logger("Successfully authenticated with GitHub Copilot!");
                return token;
            }
        }
        catch (Exception ex)
        {
            logger($"Error during Copilot login: {ex.Message}");
        }

        return null;
    }
}
