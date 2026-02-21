using System;
using System.Collections.Generic;
using System.IO;

namespace TokenMeter.Auth.Browser;

public enum BrowserType
{
    Chrome,
    Edge,
    Brave,
    Firefox
}

public class BrowserProfile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public string GetCookiesDbPath(BrowserType type)
    {
        if (type == BrowserType.Firefox)
            return System.IO.Path.Combine(Path, "cookies.sqlite");

        return System.IO.Path.Combine(Path, "Network", "Cookies");
    }
}

public class DetectedBrowser
{
    public BrowserType Type { get; set; }
    public string UserDataDir { get; set; } = string.Empty;
    public List<BrowserProfile> Profiles { get; set; } = new();

    public string GetLocalStatePath()
    {
        return Path.Combine(UserDataDir, "Local State");
    }
}

public static class BrowserDetector
{
    // Common paths in LocalAppData / AppData
    private static readonly Dictionary<BrowserType, string> BrowserPaths = new()
    {
        { BrowserType.Chrome, @"Google\Chrome\User Data" },
        { BrowserType.Edge, @"Microsoft\Edge\User Data" },
        { BrowserType.Brave, @"BraveSoftware\Brave-Browser\User Data" },
        { BrowserType.Firefox, @"Mozilla\Firefox\Profiles" } // Firefox is in Roaming AppData usually, or Local. We'll check both.
    };

    public static List<DetectedBrowser> DetectAll()
    {
        var result = new List<DetectedBrowser>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var kvp in BrowserPaths)
        {
            var type = kvp.Key;
            var subPath = kvp.Value;

            // Firefox uses Roaming for profiles
            var basePath = type == BrowserType.Firefox ? roamingAppData : localAppData;
            var fullPath = Path.Combine(basePath, subPath);

            if (Directory.Exists(fullPath))
            {
                var browser = new DetectedBrowser { Type = type, UserDataDir = fullPath };

                if (type == BrowserType.Firefox)
                {
                    // Firefox profiles are subdirectories
                    foreach (var d in Directory.GetDirectories(fullPath))
                    {
                        var name = Path.GetFileName(d);
                        if (name.EndsWith(".default-release") || name.EndsWith(".default"))
                        {
                            browser.Profiles.Add(new BrowserProfile { Name = name, Path = d });
                        }
                        else
                        {
                            // Sometimes they just have random names like "abc123yz.default" 
                            // We can just add all of them that have a cookies.sqlite
                            if (File.Exists(Path.Combine(d, "cookies.sqlite")))
                            {
                                browser.Profiles.Add(new BrowserProfile { Name = name, Path = d });
                            }
                        }
                    }
                }
                else
                {
                    // Chromium browsers have "Default" and "Profile X"
                    if (Directory.Exists(Path.Combine(fullPath, "Default")))
                        browser.Profiles.Add(new BrowserProfile { Name = "Default", Path = Path.Combine(fullPath, "Default") });

                    foreach (var d in Directory.GetDirectories(fullPath, "Profile *"))
                    {
                        browser.Profiles.Add(new BrowserProfile { Name = Path.GetFileName(d), Path = d });
                    }
                }

                if (browser.Profiles.Count > 0)
                {
                    result.Add(browser);
                }
            }
        }

        return result;
    }
}
