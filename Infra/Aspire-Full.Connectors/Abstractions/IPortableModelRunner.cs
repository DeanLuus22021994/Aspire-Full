using System.Runtime.CompilerServices;

namespace Aspire_Full.Connectors.Abstractions;

/// <summary>
/// Portable model runner abstraction with zero host dependencies.
/// Provides a containerized-first approach to model inference.
/// </summary>
public interface IPortableModelRunner
{
    /// <summary>
    /// Runs inference on the model without any host mount dependencies.
    /// Uses container-native storage and networking only.
    /// </summary>
    Task<InferenceResult> InferAsync(
        InferenceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Streams inference results without host dependencies.
    /// </summary>
    IAsyncEnumerable<InferenceChunk> StreamAsync(
        InferenceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the model's capabilities without accessing host resources.
    /// </summary>
    ModelCapabilities GetCapabilities();

    /// <summary>
    /// Health check that doesn't require host mounts.
    /// </summary>
    Task<HealthStatus> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Request for model inference - fully portable, no host paths.
/// </summary>
public sealed class InferenceRequest
{
    /// <summary>
    /// Input text for inference.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Model identifier (container-local or registry reference).
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// Temperature for sampling (0-2).
    /// </summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Additional inference options.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}

/// <summary>
/// Result from model inference.
/// </summary>
public sealed class InferenceResult
{
    public required string Output { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ModelId { get; init; }
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
}

/// <summary>
/// Streaming chunk from inference.
/// </summary>
public sealed class InferenceChunk
{
    public required string Text { get; init; }
    public bool IsComplete { get; init; }
    public int TokenIndex { get; init; }
}

/// <summary>
/// Model capabilities - all container-local.
/// </summary>
public sealed class ModelCapabilities
{
    public required string ModelId { get; init; }
    public required int MaxContextLength { get; init; }
    public required bool SupportsStreaming { get; init; }
    public required bool SupportsEmbeddings { get; init; }
    public required ComputeBackend Backend { get; init; }
    public IReadOnlyList<string> SupportedFormats { get; init; } = [];
}

/// <summary>
/// Compute backend type - portable across environments.
/// </summary>
public enum ComputeBackend
{
    /// <summary>CPU-only inference.</summary>
    Cpu,

    /// <summary>NVIDIA CUDA acceleration.</summary>
    Cuda,

    /// <summary>AMD ROCm acceleration.</summary>
    Rocm,

    /// <summary>Apple Metal acceleration.</summary>
    Metal,

    /// <summary>Automatic detection.</summary>
    Auto
}

/// <summary>
/// Health status for portable operations.
/// </summary>
public sealed class HealthStatus
{
    public required bool IsHealthy { get; init; }
    public string? Message { get; init; }
    public IReadOnlyDictionary<string, object>? Details { get; init; }
}

/// <summary>
/// Portable model runner that uses HTTP API endpoints only.
/// No host mounts, no local file access.
/// </summary>
public sealed class HttpModelRunner : IPortableModelRunner, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly TimeProvider _timeProvider;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a new HTTP model runner with internal HttpClient (recommended for DI).
    /// </summary>
    public HttpModelRunner(
        string baseUrl = "http://localhost:12434",
        TimeProvider? timeProvider = null)
        : this(new HttpClient(), baseUrl, timeProvider, ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Creates a new HTTP model runner with provided HttpClient.
    /// </summary>
    public HttpModelRunner(
        HttpClient httpClient,
        string baseUrl = "http://localhost:12434",
        TimeProvider? timeProvider = null)
        : this(httpClient, baseUrl, timeProvider, ownsHttpClient: false)
    {
    }

    private HttpModelRunner(
        HttpClient httpClient,
        string baseUrl,
        TimeProvider? timeProvider,
        bool ownsHttpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl.TrimEnd('/');
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ownsHttpClient = ownsHttpClient;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public async Task<InferenceResult> InferAsync(
        InferenceRequest request,
        CancellationToken ct = default)
    {
        var startTime = _timeProvider.GetTimestamp();

        try
        {
            // Use OpenAI-compatible API endpoint
            var endpoint = $"{_baseUrl}/engines/v1/chat/completions";

            var payload = new
            {
                model = request.ModelId,
                messages = new[]
                {
                    new { role = "user", content = request.Input }
                },
                max_tokens = request.MaxTokens,
                temperature = request.Temperature,
                stream = false
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(endpoint, content, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct)
                .ConfigureAwait(false);

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var output = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var usage = root.GetProperty("usage");
            var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

            var elapsed = _timeProvider.GetElapsedTime(startTime);

            return new InferenceResult
            {
                Output = output,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Duration = elapsed,
                ModelId = request.ModelId,
                Success = true
            };
        }
        catch (Exception ex)
        {
            var elapsed = _timeProvider.GetElapsedTime(startTime);

            return new InferenceResult
            {
                Output = string.Empty,
                InputTokens = 0,
                OutputTokens = 0,
                Duration = elapsed,
                ModelId = request.ModelId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(
        InferenceRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var endpoint = $"{_baseUrl}/engines/v1/chat/completions";

        var payload = new
        {
            model = request.ModelId,
            messages = new[]
            {
                new { role = "user", content = request.Input }
            },
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            stream = true
        };

        using var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        using var reader = new System.IO.StreamReader(stream);

        var tokenIndex = 0;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data: ".Length..];

            if (data == "[DONE]")
            {
                yield return new InferenceChunk
                {
                    Text = string.Empty,
                    IsComplete = true,
                    TokenIndex = tokenIndex
                };
                yield break;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(data);
            var choices = doc.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentProp))
                {
                    var text = contentProp.GetString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return new InferenceChunk
                        {
                            Text = text,
                            IsComplete = false,
                            TokenIndex = tokenIndex++
                        };
                    }
                }
            }
        }
    }

    public ModelCapabilities GetCapabilities()
    {
        return new ModelCapabilities
        {
            ModelId = "http-runner",
            MaxContextLength = 128000,
            SupportsStreaming = true,
            SupportsEmbeddings = true,
            Backend = ComputeBackend.Auto,
            SupportedFormats = ["gguf", "safetensors"]
        };
    }

    public async Task<HealthStatus> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var endpoint = $"{_baseUrl}/engines/v1/models";

            using var response = await _httpClient.GetAsync(endpoint, ct)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new HealthStatus
                {
                    IsHealthy = true,
                    Message = "Model runner is healthy"
                };
            }

            return new HealthStatus
            {
                IsHealthy = false,
                Message = $"Status code: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Message = ex.Message
            };
        }
    }
}
