using Aspire_Full.Pipeline.Services;

namespace Aspire_Full.Pipeline.Modules.Discovery;

public class DiscoveryModule
{
    private readonly DiscoveryService _service = new();

    public async Task RunAsync()
    {
        await _service.RunDiscoveryAsync();
    }
}
