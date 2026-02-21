using System;
using System.Collections.Generic;
using System.Linq;
using TokenMeter.Core.Models;

namespace TokenMeter.Auth;

public sealed class AuthRegistry
{
    private readonly IEnumerable<IAuthRunner> _runners;

    public AuthRegistry(IEnumerable<IAuthRunner> runners)
    {
        _runners = runners;
    }

    public IAuthRunner? GetRunner(UsageProvider provider)
    {
        return _runners.FirstOrDefault(r => r.Provider == provider);
    }

    public IEnumerable<IAuthRunner> GetAllRunners()
    {
        return _runners;
    }
}
