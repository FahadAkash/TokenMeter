using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenMeter.Auth.Browser;

using TokenMeter.Core.Models;

namespace TokenMeter.Auth.Runners;

public class ClaudeCookieLoginRunner : IAuthRunner
{
    public UsageProvider Provider => UsageProvider.Claude;
    public string DisplayName => "Browser Extraction (Claude)";

    private readonly ITokenStore _tokenStore;

    public async Task<bool> AuthenticateAsync(Action<string> statusLogger, CancellationToken ct = default)
    {
        statusLogger("Scanning browsers for Claude session...");
        var result = await TryExtractAndSaveSilentlyAsync(ct);
        statusLogger(result ? "Claude session found and saved." : "Claude session not found.");
        return result;
    }

    public ClaudeCookieLoginRunner(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Attempts to silently extract claude.ai cookies from natively installed web browsers
    /// and save them into the Windows Credential Store.
    /// </summary>
    public async Task<bool> TryExtractAndSaveSilentlyAsync(CancellationToken ct = default)
    {
        var browsers = BrowserDetector.DetectAll();

        foreach (var browser in browsers)
        {
            var cookies = CookieExtractor.ExtractForDomain(browser, "claude.ai");
            if (cookies.Count > 0)
            {
                // We mainly need "sessionKey" to authenticate successfully.
                var sessionKey = cookies.FirstOrDefault(c => c.Name == "sessionKey");
                if (sessionKey != null && !string.IsNullOrEmpty(sessionKey.Value))
                {
                    var cookieHeader = CookieExtractor.BuildCookieHeader(cookies);
                    await _tokenStore.SetAsync("claude_cookie", cookieHeader, ct);
                    Console.WriteLine($"Successfully extracted Claude session from {browser.Type}");
                    return true;
                }
            }

            // Claude also uses console.anthropic.com or auth.anthropic.com occasionally
            cookies = CookieExtractor.ExtractForDomain(browser, "anthropic.com");
            if (cookies.Count > 0)
            {
                var sessionKey = cookies.FirstOrDefault(c => c.Name == "sessionKey");
                if (sessionKey != null && !string.IsNullOrEmpty(sessionKey.Value))
                {
                    var cookieHeader = CookieExtractor.BuildCookieHeader(cookies);
                    await _tokenStore.SetAsync("claude_cookie", cookieHeader, ct);
                    Console.WriteLine($"Successfully extracted Anthropic session from {browser.Type}");
                    return true;
                }
            }
        }

        Console.WriteLine("Could not find an active Claude session in any installed browser.");
        return false;
    }
}
