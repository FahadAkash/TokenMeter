using System.Security.Cryptography;
using System.Text;

namespace TokenMeter.Auth.Stores;

/// <summary>
/// Token store backed by Windows DPAPI (<see cref="ProtectedData"/>).
/// Secrets are encrypted per-user and persisted to a local directory.
/// </summary>
public sealed class WindowsCredentialStore : ITokenStore
{
    private readonly string _storeDirectory;

    public WindowsCredentialStore(string? storeDirectory = null)
    {
        _storeDirectory = storeDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TokenMeter", "credentials");
        Directory.CreateDirectory(_storeDirectory);
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var path = KeyPath(key);
        if (!File.Exists(path)) return Task.FromResult<string?>(null);

        var encrypted = File.ReadAllBytes(path);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(decrypted));
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyPath(key), encrypted);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = KeyPath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_storeDirectory, "*.dat");
        var keys = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    private string KeyPath(string key)
    {
        // Sanitise the key to be a safe filename
        var safe = Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .Replace('/', '_').Replace('+', '-').TrimEnd('=');
        return Path.Combine(_storeDirectory, safe + ".dat");
    }
}
