using System;
using System.Collections.Generic;
using System.Linq;
using Aspire_Full.DockerRegistry.Abstractions;

namespace Aspire_Full.DockerRegistry.Services;

public class RegistryFactory
{
    private readonly IEnumerable<IRegistryProvider> _providers;

    public RegistryFactory(IEnumerable<IRegistryProvider> providers)
    {
        _providers = providers;
    }

    public IRegistryProvider GetProvider(string repository)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(repository));
        if (provider == null)
        {
             throw new DockerRegistryException(DockerRegistryErrorCode.Unknown, $"No registry provider found for repository '{repository}'");
        }
        return provider;
    }
}
