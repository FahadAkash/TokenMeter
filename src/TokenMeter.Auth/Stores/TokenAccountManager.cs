using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TokenMeter.Auth.Stores;

/// <summary>
/// Handles retrieving active accounts when potentially multiple sessions are configured for a given provider key.
/// Maps the older single-string credentials store logic gracefully into a multi-credential JSON array map.
/// </summary>
public class TokenAccountManager
{
    private readonly ITokenStore _store;

    public TokenAccountManager(ITokenStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Returns the active or default token string for the given root key.
    /// Safely differentiates between plain strings (legacy / simple) and JSON configuration objects (multi-account).
    /// </summary>
    public async Task<string?> GetActiveAccountTokenAsync(string baseKey, CancellationToken ct = default)
    {
        var rawValue = await _store.GetAsync(baseKey, ct);
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        // Try parsing as JSON if it suspiciously looks like an array or object
        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            try
            {
                // Simple representation: list of tokens. For now just grab the first valid one
                // Future expansion: AccountId targeting based on active workspace
                var tokens = JsonSerializer.Deserialize<List<string>>(rawValue);
                return tokens?.Count > 0 ? tokens[0] : null;
            }
            catch (JsonException)
            {
                // If it fails to parse as JSON despite starting with {/[, treat it as a raw token
                return rawValue;
            }
        }

        // It's just a raw plain string token
        return rawValue;
    }

    /// <summary>
    /// Saves a new token as the active account.
    /// </summary>
    public async Task SetActiveAccountTokenAsync(string baseKey, string tokenValue, CancellationToken ct = default)
    {
        await _store.SetAsync(baseKey, tokenValue, ct);
    }

    /// <summary>
    /// Resolves the primary session token/cookie based on the provider enum flags.
    /// </summary>
    public async Task<string?> GetPrimaryCredentialsAsync(TokenMeter.Core.Models.UsageProvider provider, CancellationToken ct = default)
    {
        // Special legacy mappings
        var storageKey = provider switch
        {
            TokenMeter.Core.Models.UsageProvider.Claude => "claude_cookie",
            TokenMeter.Core.Models.UsageProvider.Codex => "chatgpt_cookie",
            TokenMeter.Core.Models.UsageProvider.Cursor => "cursor_cookie",
            TokenMeter.Core.Models.UsageProvider.Copilot => "copilot_token",
            _ => $"{provider.ToString().ToLowerInvariant()}_cookie" // Generic fallback syntax for modern APIs
        };

        return await GetActiveAccountTokenAsync(storageKey, ct);
    }
}
