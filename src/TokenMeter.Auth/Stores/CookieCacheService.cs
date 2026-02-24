using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TokenMeter.Auth.Stores;

/// <summary>
/// A simple service to cache successfully extracted cookies to avoid
/// repeated browser extraction (which can sometimes lock files or trigger AV).
/// This service uses the existing <see cref="ITokenStore"/> to securely store the cache.
/// </summary>
public class CookieCacheService
{
    private readonly ITokenStore _tokenStore;

    public CookieCacheService(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Try to get a valid, cached cookie for the given provider key.
    /// In a real implementation, you might parse the cookie string to check expiration,
    /// but for now we rely on the provider probe to report an auth failure 
    /// which should trigger a cache invalidation.
    /// </summary>
    public async Task<string?> GetCachedCookieAsync(string providerKey, CancellationToken ct = default)
    {
        var cacheKey = $"{providerKey}_cached";
        var value = await _tokenStore.GetAsync(cacheKey, ct);

        if (!string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"[CookieCache] HIT for {providerKey}");
            return value;
        }

        Console.WriteLine($"[CookieCache] MISS for {providerKey}");
        return null;
    }

    /// <summary>
    /// Save a valid cookie header string to the secure cache.
    /// </summary>
    public async Task SaveCachedCookieAsync(string providerKey, string cookieHeader, CancellationToken ct = default)
    {
        var cacheKey = $"{providerKey}_cached";
        await _tokenStore.SetAsync(cacheKey, cookieHeader, ct);
        Console.WriteLine($"[CookieCache] SAVED for {providerKey}");
    }

    /// <summary>
    /// Invalidate/delete a cached cookie for a provider (e.g., when it expires).
    /// </summary>
    public async Task InvalidateCacheAsync(string providerKey, CancellationToken ct = default)
    {
        var cacheKey = $"{providerKey}_cached";
        await _tokenStore.DeleteAsync(cacheKey, ct);
        Console.WriteLine($"[CookieCache] INVALIDATED for {providerKey}");
    }
}
