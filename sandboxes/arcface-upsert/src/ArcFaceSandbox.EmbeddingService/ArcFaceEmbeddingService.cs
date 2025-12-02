using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Default ArcFace embedding implementation backed by ONNX Runtime.
/// </summary>
public sealed class ArcFaceEmbeddingService : IArcFaceEmbeddingService, IDisposable
{
    private const string InputName = "data";
    private const int VectorLength = 512;
    private static readonly ActivitySource ActivitySource = new("ArcFaceSandbox.EmbeddingService");
    private static readonly Meter Meter = new("ArcFaceSandbox.EmbeddingService", "1.0.0");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("arcface_embedding_latency_ms", unit: "ms");
    private static readonly Counter<long> BatchCounter = Meter.CreateCounter<long>("arcface_embedding_batch_total");

    private readonly ILogger<ArcFaceEmbeddingService> _logger;
    private readonly ArcFaceEmbeddingOptions _options;
    private readonly IArcFaceInferenceRunner _runner;
    private readonly int _effectiveBatchSize;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ArcFaceModelInfo _modelInfo;
    private bool _disposed;

    public ArcFaceEmbeddingService(
        IOptions<ArcFaceEmbeddingOptions> options,
        ILogger<ArcFaceEmbeddingService> logger,
        IArcFaceInferenceRunner runner)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _effectiveBatchSize = Math.Max(1, (int)Math.Floor(_options.MaxBatchSize * (1.0 - _options.TensorCoreHeadroom)));
        _concurrencySemaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));
        _modelInfo = runner.ModelInfo;

        _logger.LogInformation(
            "ArcFace model {Model} ({Version}) loaded with provider {Provider} | batchSize={Batch} | headroom={Headroom:P0}",
            _modelInfo.ModelName,
            _modelInfo.ModelVersion,
            _modelInfo.ExecutionProvider,
            _effectiveBatchSize,
            _options.TensorCoreHeadroom);
    }

    public ArcFaceModelInfo ModelInfo => _modelInfo;

    public async Task<ReadOnlyMemory<float>> GenerateAsync(Stream alignedFace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alignedFace);
        await foreach (var vector in GenerateBatchAsync(new[] { alignedFace }, cancellationToken).ConfigureAwait(false))
        {
            return vector;
        }

        throw new InvalidOperationException("ArcFace embedding generation produced no output.");
    }

    public async IAsyncEnumerable<ReadOnlyMemory<float>> GenerateBatchAsync(IEnumerable<Stream> alignedFaces, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alignedFaces);
        using var activity = ActivitySource.StartActivity("ArcFace.GenerateBatch");

        var tensors = new List<DenseTensor<float>>(_effectiveBatchSize);
        foreach (var face in alignedFaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tensors.Add(await ArcFacePreprocessor.ToTensorAsync(face, cancellationToken).ConfigureAwait(false));

            if (tensors.Count >= _effectiveBatchSize)
            {
                foreach (var embedding in await RunInferenceAsync(tensors, cancellationToken).ConfigureAwait(false))
                {
                    yield return embedding;
                }

                tensors.Clear();
            }
        }

        if (tensors.Count > 0)
        {
            foreach (var embedding in await RunInferenceAsync(tensors, cancellationToken).ConfigureAwait(false))
            {
                yield return embedding;
            }
        }
    }

    private async Task<IReadOnlyList<ReadOnlyMemory<float>>> RunInferenceAsync(IReadOnlyList<DenseTensor<float>> tensors, CancellationToken cancellationToken)
    {
        if (tensors.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var rawOutput = _runner.Run(InputName, Concatenate(tensors));
            stopwatch.Stop();

            LatencyHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
            BatchCounter.Add(tensors.Count);

            if (_options.EnableVerboseLogging)
            {
                _logger.LogDebug("ArcFace inference finished for batch {Batch} in {Elapsed} ms", tensors.Count, stopwatch.Elapsed.TotalMilliseconds);
            }

            return SliceEmbeddings(rawOutput, tensors.Count);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private static DenseTensor<float> Concatenate(IReadOnlyList<DenseTensor<float>> tensors)
    {
        if (tensors.Count == 1)
        {
            return tensors[0];
        }

        var tensor = new DenseTensor<float>(new[] { tensors.Count, 3, ArcFacePreprocessor.TargetSize, ArcFacePreprocessor.TargetSize });
        for (var batch = 0; batch < tensors.Count; batch++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                for (var y = 0; y < ArcFacePreprocessor.TargetSize; y++)
                {
                    for (var x = 0; x < ArcFacePreprocessor.TargetSize; x++)
                    {
                        tensor[batch, channel, y, x] = tensors[batch][0, channel, y, x];
                    }
                }
            }
        }

        return tensor;
    }

    private static IReadOnlyList<ReadOnlyMemory<float>> SliceEmbeddings(float[] output, int batchSize)
    {
        if (output.Length != batchSize * VectorLength)
        {
            throw new InvalidOperationException($"Unexpected vector size from ArcFace model. Expected {batchSize * VectorLength}, got {output.Length}.");
        }

        var embeddings = new List<ReadOnlyMemory<float>>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var vector = new float[VectorLength];
            Array.Copy(output, i * VectorLength, vector, 0, VectorLength);
            NormalizeInPlace(vector);
            embeddings.Add(vector);
        }

        return embeddings;
    }

    private static void NormalizeInPlace(Span<float> vector)
    {
        double sum = 0;
        foreach (var value in vector)
        {
            sum += value * value;
        }

        var magnitude = Math.Sqrt(sum);
        if (magnitude < 1e-9)
        {
            return;
        }

        var scale = 1.0 / magnitude;
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] * scale);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _concurrencySemaphore.Dispose();
        _disposed = true;
    }
}

internal static class ArcFacePreprocessor
{
    public const int TargetSize = 112;
    private const float Mean = 127.5f;
    private const float Std = 128f;

    public static async Task<DenseTensor<float>> ToTensorAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        using var image = await Image.LoadAsync<Rgb24>(source, cancellationToken).ConfigureAwait(false);
        if (image.Width != TargetSize || image.Height != TargetSize)
        {
            image.Mutate(ctx => ctx.Resize(TargetSize, TargetSize));
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < TargetSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < TargetSize; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = Normalize(pixel.B);
                    tensor[0, 1, y, x] = Normalize(pixel.G);
                    tensor[0, 2, y, x] = Normalize(pixel.R);
                }
            }
        });

        return tensor;
    }

    private static float Normalize(byte value) => (value - Mean) / Std;
}
