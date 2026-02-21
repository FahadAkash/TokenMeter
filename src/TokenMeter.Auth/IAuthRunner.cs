using System;
using System.Threading;
using System.Threading.Tasks;
using TokenMeter.Core.Models;

namespace TokenMeter.Auth;

/// <summary>
/// Unified interface for various authentication strategies (OAuth, Cookie Extraction, etc.)
/// </summary>
public interface IAuthRunner
{
    /// <summary>
    /// The provider this runner handles.
    /// </summary>
    UsageProvider Provider { get; }

    /// <summary>
    /// Display name for this authentication method.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Attempts to authenticate.
    /// </summary>
    /// <param name="statusLogger">Callback for real-time status updates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if authentication succeeded and token was stored.</returns>
    Task<bool> AuthenticateAsync(Action<string> statusLogger, CancellationToken ct = default);
}
