using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TokenMeter.Auth.Browser;

public static class FirefoxCookieExtractor
{
    public static List<Cookie> ExtractCookies(BrowserProfile profile, string domain)
    {
        var cookiesDb = Path.Combine(profile.Path, "cookies.sqlite");
        if (!File.Exists(cookiesDb))
            return new List<Cookie>();

        var tempDb = CopyToTemp(cookiesDb);
        var cookies = new List<Cookie>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={tempDb}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT name, value, host, path, expiry, isSecure, isHttpOnly
                FROM moz_cookies
                WHERE host LIKE $domain OR host LIKE $dotDomain";

            command.Parameters.AddWithValue("$domain", $"%{domain}");
            command.Parameters.AddWithValue("$dotDomain", $".{domain}");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cookies.Add(new Cookie
                {
                    Name = reader.GetString(0),
                    Value = reader.GetString(1),
                    Domain = reader.GetString(2),
                    Path = reader.GetString(3),
                    ExpiresUtc = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                    IsSecure = !reader.IsDBNull(5) && reader.GetInt32(5) != 0,
                    IsHttpOnly = !reader.IsDBNull(6) && reader.GetInt32(6) != 0
                });
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }

        return cookies;
    }

    private static string CopyToTemp(string path)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"codexbar_{Guid.NewGuid()}_{Path.GetFileName(path)}");

        // Firefox might lock the database, so we open it in FileShare.ReadWrite mode and copy the stream
        using (var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var destStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            sourceStream.CopyTo(destStream);
        }

        return tempFile;
    }
}
