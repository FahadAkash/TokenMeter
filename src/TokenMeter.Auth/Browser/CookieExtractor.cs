using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenMeter.Auth.Browser;

public class Cookie
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long? ExpiresUtc { get; set; }
    public bool IsSecure { get; set; }
    public bool IsHttpOnly { get; set; }

    public string ToHeaderValue() => $"{Name}={Value}";
}

public static class CookieExtractor
{
    public static List<Cookie> ExtractForDomain(DetectedBrowser browser, string domain)
    {
        var allCookies = new List<Cookie>();

        foreach (var profile in browser.Profiles)
        {
            try
            {
                var profileCookies = ExtractProfileCookies(browser, profile, domain);
                allCookies.AddRange(profileCookies);
            }
            catch (Exception)
            {
                // Soft fail if DB is locked or unreadable
            }
        }

        return allCookies;
    }

    private static List<Cookie> ExtractProfileCookies(DetectedBrowser browser, BrowserProfile profile, string domain)
    {
        if (browser.Type == BrowserType.Firefox)
        {
            return FirefoxCookieExtractor.ExtractCookies(profile, domain);
        }
        else
        {
            return ChromiumCookieExtractor.ExtractCookies(browser, profile, domain);
        }
    }

    public static string BuildCookieHeader(IEnumerable<Cookie> cookies)
    {
        return string.Join("; ", cookies.Select(c => c.ToHeaderValue()));
    }
}
