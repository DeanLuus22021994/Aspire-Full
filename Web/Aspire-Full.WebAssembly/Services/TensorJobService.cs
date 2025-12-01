using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Aspire_Full.Shared;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.WebAssembly.Services;

public sealed class TensorJobService
{
    private readonly HttpClient _httpClient;
    private readonly FrontendEnvironmentRegistry _registry;
    private readonly string _environmentKey;

    public TensorJobService(HttpClient httpClient, FrontendEnvironmentRegistry registry, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _registry = registry;
        _environmentKey = configuration["FRONTEND_ENVIRONMENT_KEY"] ?? FrontendEnvironmentKeys.DevelopmentDocs;
    }

    public async Task<IReadOnlyList<TensorJobSummary>> GetRecentJobsAsync(CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("/api/tensor-tasks/jobs?limit=25");
        var response = await _httpClient.GetFromJsonAsync(uri, AppJsonContext.Default.ListTensorJobSummary, cancellationToken).ConfigureAwait(false);
        return response ?? new List<TensorJobSummary>();
    }

    public async Task<TensorJobStatus?> SubmitJobAsync(TensorJobSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var uri = BuildUri("/api/tensor-tasks/jobs");
        var httpResponse = await _httpClient.PostAsJsonAsync(uri, submission, AppJsonContext.Default.TensorJobSubmission, cancellationToken).ConfigureAwait(false);
        if (!httpResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return await httpResponse.Content.ReadFromJsonAsync(AppJsonContext.Default.TensorJobStatus, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TensorJobStatus?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Job id must be provided", nameof(jobId));
        }

        var uri = BuildUri($"/api/tensor-tasks/jobs/{jobId}");
        return await _httpClient.GetFromJsonAsync(uri, AppJsonContext.Default.TensorJobStatus, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<TensorInferenceChunk> StreamJobOutputAsync(Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            yield break;
        }

        var uri = BuildUri($"/api/tensor-tasks/jobs/{jobId}/stream");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            TensorInferenceChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize(line, AppJsonContext.Default.TensorInferenceChunk);
            }
            catch
            {
                // Ignore malformed chunks and continue streaming
            }

            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    public async Task<IReadOnlyList<TensorModelSummary>> GetServerCatalogAsync(CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("/api/tensor-tasks/catalog");
        var response = await _httpClient.GetFromJsonAsync(uri, AppJsonContext.Default.ListTensorModelSummary, cancellationToken).ConfigureAwait(false);
        return response ?? new List<TensorModelSummary>();
    }

    private Uri BuildUri(string relativePath)
    {
        var env = _registry.GetByKey(_environmentKey);
        return new Uri(new Uri(env.ApiBaseAddress), relativePath);
    }
}
