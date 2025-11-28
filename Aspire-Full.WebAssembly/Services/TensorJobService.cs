using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Aspire_Full.WebAssembly.Services;

public sealed class TensorJobService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
        var response = await _httpClient.GetFromJsonAsync<List<TensorJobSummary>>(uri, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return response ?? new List<TensorJobSummary>();
    }

    public async Task<TensorJobStatus?> SubmitJobAsync(TensorJobSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var uri = BuildUri("/api/tensor-tasks/jobs");
        var httpResponse = await _httpClient.PostAsJsonAsync(uri, submission, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (!httpResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return await httpResponse.Content.ReadFromJsonAsync<TensorJobStatus>(SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TensorJobStatus?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Job id must be provided", nameof(jobId));
        }

        var uri = BuildUri($"/api/tensor-tasks/jobs/{jobId}");
        return await _httpClient.GetFromJsonAsync<TensorJobStatus>(uri, SerializerOptions, cancellationToken).ConfigureAwait(false);
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
                chunk = JsonSerializer.Deserialize<TensorInferenceChunk>(line, SerializerOptions);
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
        var response = await _httpClient.GetFromJsonAsync<List<TensorModelSummary>>(uri, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return response ?? new List<TensorModelSummary>();
    }

    private Uri BuildUri(string relativePath)
    {
        var env = _registry.GetByKey(_environmentKey);
        return new Uri(new Uri(env.ApiBaseAddress), relativePath);
    }
}

public sealed record TensorJobSubmission
{
    public required string ModelId { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string? InputImageUrl { get; init; }
    public string ExecutionProvider { get; init; } = string.Empty;
    public bool PersistToVectorStore { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorJobSummary
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ExecutionProvider { get; init; }
    public string PromptPreview { get; init; } = string.Empty;
}

public sealed record TensorJobStatus
{
    public required Guid Id { get; init; }
    public required string ModelId { get; init; }
    public required string Status { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string PromptPreview { get; init; } = string.Empty;
    public string? InputImageUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? ExecutionProvider { get; init; }
    public string? VectorDocumentId { get; init; }
    public IList<TensorInferenceChunk> Output { get; init; } = new List<TensorInferenceChunk>();
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record TensorInferenceChunk
{
    public required string Type { get; init; }
    public required string Content { get; init; }
    public int Sequence { get; init; }
    public double? Confidence { get; init; }
}

public sealed record TensorModelSummary
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DocumentationUri { get; init; }
    public string? RecommendedExecutionProvider { get; init; }
}
