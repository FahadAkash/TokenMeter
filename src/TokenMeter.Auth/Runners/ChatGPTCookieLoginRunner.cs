using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenMeter.Auth.Browser;
using TokenMeter.Core.Models;

namespace TokenMeter.Auth.Runners;

public class ChatGPTCookieLoginRunner : IAuthRunner
{
    public UsageProvider Provider => UsageProvider.ChatGPT;
    public string DisplayName => "Browser Extraction (ChatGPT)";

    private readonly ITokenStore _tokenStore;

    public ChatGPTCookieLoginRunner(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public async Task<bool> AuthenticateAsync(Action<string> statusLogger, CancellationToken ct = default)
    {
        statusLogger("Scanning browsers for ChatGPT session...");
        var result = await TryExtractAndSaveSilentlyAsync(ct);
        statusLogger(result ? "ChatGPT session found and saved." : "ChatGPT session not found.");
        return result;
    }

    public async Task<bool> TryExtractAndSaveSilentlyAsync(CancellationToken ct = default)
    {
        var browsers = BrowserDetector.DetectAll();

        foreach (var browser in browsers)
        {
            var cookies = CookieExtractor.ExtractForDomain(browser, "chatgpt.com");
            if (cookies.Count > 0)
            {
                var sessionToken = cookies.FirstOrDefault(c => c.Name == "__Secure-next-auth.session-token");
                if (sessionToken != null && !string.IsNullOrEmpty(sessionToken.Value))
                {
                    var cookieHeader = CookieExtractor.BuildCookieHeader(cookies);
                    await _tokenStore.SetAsync("chatgpt_cookie", cookieHeader, ct);
                    return true;
                }
            }

            // Fallback to openai.com
            cookies = CookieExtractor.ExtractForDomain(browser, "openai.com");
            if (cookies.Count > 0)
            {
                var sessionToken = cookies.FirstOrDefault(c => c.Name == "__Secure-next-auth.session-token");
                if (sessionToken != null && !string.IsNullOrEmpty(sessionToken.Value))
                {
                    var cookieHeader = CookieExtractor.BuildCookieHeader(cookies);
                    await _tokenStore.SetAsync("chatgpt_cookie", cookieHeader, ct);
                    return true;
                }
            }
        }

        return false;
    }
}
