using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TokenMeter.Auth.Browser;

public static class ChromiumCookieExtractor
{
    public static List<Cookie> ExtractCookies(DetectedBrowser browser, BrowserProfile profile, string domain)
    {
        var cookiesDb = profile.GetCookiesDbPath(browser.Type);
        if (!File.Exists(cookiesDb))
            return new List<Cookie>();

        var localStatePath = browser.GetLocalStatePath();
        var encryptionKey = GetChromiumEncryptionKey(localStatePath);
        if (encryptionKey == null)
            return new List<Cookie>();

        var tempDb = CopyToTemp(cookiesDb);
        var cookies = new List<Cookie>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={tempDb}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT name, encrypted_value, host_key, path, expires_utc, is_secure, is_httponly
                FROM cookies
                WHERE host_key LIKE $domain OR host_key LIKE $dotDomain";

            command.Parameters.AddWithValue("$domain", $"%{domain}");
            command.Parameters.AddWithValue("$dotDomain", $".{domain}");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var encryptedValueBlob = reader["encrypted_value"] as byte[];
                var hostKey = reader.GetString(2);
                var path = reader.GetString(3);
                var expiresUtc = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                var isSecure = !reader.IsDBNull(5) && reader.GetInt32(5) != 0;
                var isHttpOnly = !reader.IsDBNull(6) && reader.GetInt32(6) != 0;

                string value = string.Empty;
                if (encryptedValueBlob != null && encryptedValueBlob.Length > 0)
                {
                    value = DecryptChromiumCookie(encryptedValueBlob, encryptionKey);
                }

                cookies.Add(new Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = hostKey,
                    Path = path,
                    ExpiresUtc = expiresUtc > 0 ? expiresUtc : null,
                    IsSecure = isSecure,
                    IsHttpOnly = isHttpOnly
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

    private static byte[]? GetChromiumEncryptionKey(string localStatePath)
    {
        if (!File.Exists(localStatePath)) return null;

        string content;
        using (var stream = new FileStream(localStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            content = reader.ReadToEnd();
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("os_crypt", out var osCrypt) &&
                osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyStr))
            {
                var base64Key = encryptedKeyStr.GetString();
                if (string.IsNullOrEmpty(base64Key)) return null;

                var encryptedKey = Convert.FromBase64String(base64Key);

                // Remove "DPAPI" prefix (first 5 bytes)
                if (encryptedKey.Length < 5 ||
                    encryptedKey[0] != 'D' || encryptedKey[1] != 'P' ||
                    encryptedKey[2] != 'A' || encryptedKey[3] != 'P' || encryptedKey[4] != 'I')
                {
                    return null;
                }

                var dpapiBlob = new byte[encryptedKey.Length - 5];
                Array.Copy(encryptedKey, 5, dpapiBlob, 0, dpapiBlob.Length);

                // Decrypt with DPAPI ProtectedData
#pragma warning disable CA1416 // Validate platform compatibility
                return ProtectedData.Unprotect(dpapiBlob, null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            }
        }
        catch (Exception)
        {
            // Parsing or DPAPI failure
        }
        return null;
    }

    private static string DecryptChromiumCookie(byte[] encryptedValue, byte[] key)
    {
        if (encryptedValue.Length == 0) return string.Empty;

        // Check for v10 or v11 prefix (AES-256-GCM)
        if (encryptedValue.Length >= 31 &&
            encryptedValue[0] == 'v' &&
            (encryptedValue[1] == '1' && (encryptedValue[2] == '0' || encryptedValue[2] == '1')))
        {
            var nonce = new byte[12];
            Array.Copy(encryptedValue, 3, nonce, 0, 12);

            var ciphertextLength = encryptedValue.Length - 15 - 16;
            if (ciphertextLength < 0) return string.Empty;

            var ciphertext = new byte[ciphertextLength];
            Array.Copy(encryptedValue, 15, ciphertext, 0, ciphertextLength);

            var tag = new byte[16];
            Array.Copy(encryptedValue, encryptedValue.Length - 16, tag, 0, 16);

            var plaintext = new byte[ciphertextLength];

#pragma warning disable CA1416 // Validate platform compatibility
            using (var aesGcm = new AesGcm(key, 16))
            {
                try
                {
                    aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                }
                catch
                {
                    return string.Empty;
                }
            }
#pragma warning restore CA1416

            // Chromium 127+ adds a 32-byte App-Bound Encryption wrapper prefix
            if (plaintext.Length > 32)
            {
                bool hasGarbagePrefix = false;
                for (int i = 0; i < 32; i++)
                {
                    if (plaintext[i] > 127 || plaintext[i] < 32)
                    {
                        hasGarbagePrefix = true;
                        break;
                    }
                }

                if (hasGarbagePrefix)
                {
                    int start = 0;
                    for (int i = 0; i < plaintext.Length; i++)
                    {
                        if (char.IsLetterOrDigit((char)plaintext[i]) || plaintext[i] == '"' || plaintext[i] == '{')
                        {
                            start = i;
                            break;
                        }
                    }

                    int actualStart = (start < 32 && plaintext.Length > 32) ? 32 : start;
                    var stripped = new byte[plaintext.Length - actualStart];
                    Array.Copy(plaintext, actualStart, stripped, 0, stripped.Length);
                    return Encoding.UTF8.GetString(stripped);
                }
            }

            return Encoding.UTF8.GetString(plaintext);
        }
        else
        {
            // Old format, pure DPAPI
            try
            {
#pragma warning disable CA1416 
                var decrypted = ProtectedData.Unprotect(encryptedValue, null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private static string CopyToTemp(string path)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"codexbar_{Guid.NewGuid()}_{Path.GetFileName(path)}");
        using (var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var destStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            sourceStream.CopyTo(destStream);
        }
        return tempFile;
    }
}
