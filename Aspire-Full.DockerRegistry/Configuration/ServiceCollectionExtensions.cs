using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Aspire_Full.DockerRegistry.Abstractions;
using Aspire_Full.DockerRegistry.Services;
using Aspire_Full.DockerRegistry.Workers;
using Aspire_Full.DockerRegistry.GarbageCollection;

namespace Aspire_Full.DockerRegistry.Configuration;

public static class ServiceCollectionExtensions
{
    private const string DefaultConfigSection = "DockerRegistry";

    public static IServiceCollection AddDockerRegistryClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName ?? DefaultConfigSection);
        services.AddOptions<DockerRegistryOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseAddress), "BaseAddress must be provided");

        services.AddHttpClient<IDockerRegistryClient, PatternDockerRegistryClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DockerRegistryOptions>>().Value;
            client.BaseAddress = EnsureTrailingSlash(options.BaseAddress);
            client.Timeout = options.HttpTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : options.HttpTimeout;
            ConfigureAuthentication(client, options.Credentials);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DockerRegistryOptions>>().Value;
            if (!options.AllowInsecureTls)
            {
                return new HttpClientHandler();
            }

            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        });

        services.AddSingleton<IBuildxWorkerFactory, BuildxWorkerFactory>();
        services.AddSingleton<IGarbageCollector, GarbageCollector>();
        services.AddSingleton<IGarbageCollectionPolicy, MaxCountRetentionPolicy>();
        services.AddHostedService<GarbageCollectorService>();

        return services;
    }

    private static Uri EnsureTrailingSlash(string baseAddress)
    {
        var formatted = baseAddress.EndsWith("/", StringComparison.Ordinal) ? baseAddress : baseAddress + "/";
        return new Uri(formatted, UriKind.Absolute);
    }

    private static void ConfigureAuthentication(HttpClient client, DockerRegistryCredentialOptions credentials)
    {
        if (!string.IsNullOrWhiteSpace(credentials.BearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.BearerToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(credentials.Username))
        {
            var password = credentials.Password ?? string.Empty;
            var buffer = Encoding.UTF8.GetBytes($"{credentials.Username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(buffer));
        }
    }
}
