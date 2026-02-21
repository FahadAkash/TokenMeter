namespace TokenMeter.Auth;

/// <summary>
/// Abstraction over secure credential storage.
/// On Windows this is backed by the Credential Manager via DPAPI.
/// </summary>
public interface ITokenStore
{
    /// <summary>Retrieve a stored secret by key, or null if not found.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Store a secret under the given key.</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Delete a stored secret.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>List all keys that have stored secrets.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default);
}
