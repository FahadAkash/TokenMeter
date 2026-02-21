using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenMeter.Auth.Browser;

using TokenMeter.Core.Models;

namespace TokenMeter.Auth.Runners
{
    public class CursorCookieLoginRunner : IAuthRunner
    {
        public UsageProvider Provider => UsageProvider.Cursor;
        public string DisplayName => "Browser Extraction (Cursor)";

        private readonly ITokenStore _tokenStore;

        public async Task<bool> AuthenticateAsync(Action<string> statusLogger, CancellationToken ct = default)
        {
            statusLogger("Scanning browsers for Cursor session...");
            var result = await TryExtractAndSaveSilentlyAsync(ct);
            statusLogger(result ? "Cursor session found and saved." : "Cursor session not found.");
            return result;
        }

        public CursorCookieLoginRunner(ITokenStore tokenStore)
        {
            _tokenStore = tokenStore;
        }

        public async Task<bool> TryExtractAndSaveSilentlyAsync(CancellationToken ct = default)
        {
            var browsers = BrowserDetector.DetectAll();

            foreach (var browser in browsers)
            {
                var cookies = CookieExtractor.ExtractForDomain(browser, "cursor.com");
                if (cookies.Count == 0)
                {
                    cookies = CookieExtractor.ExtractForDomain(browser, "cursor.sh");
                }

                if (cookies.Count > 0)
                {
                    var cookieHeader = CookieExtractor.BuildCookieHeader(cookies);
                    await _tokenStore.SetAsync("cursor_cookie", cookieHeader, ct);
                    Console.WriteLine($"Successfully extracted Cursor session from {browser.Type}");
                    return true;
                }
            }

            Console.WriteLine("Could not find an active Cursor session in any installed browser.");
            return false;
        }
    }
}
