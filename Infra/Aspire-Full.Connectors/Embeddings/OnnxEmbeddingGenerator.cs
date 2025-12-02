using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Aspire_Full.Connectors.Embeddings;

/// <summary>
/// ONNX-based embedding generator with CUDA/GPU support.
/// Implements <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> for Microsoft.Extensions.AI compatibility.
/// Uses BertTokenizer for text tokenization and InferenceSession for ONNX model inference.
/// </summary>
public sealed class OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly ILogger<OnnxEmbeddingGenerator> _logger;
    private readonly int _embeddingDimension;
    private readonly string _modelPath;
    private bool _disposed;

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="OnnxEmbeddingGenerator"/>.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file.</param>
    /// <param name="vocabPath">Path to the vocabulary file for tokenization.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="FileNotFoundException">Thrown when model file is not found.</exception>
    public OnnxEmbeddingGenerator(string modelPath, string vocabPath, ILogger<OnnxEmbeddingGenerator> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vocabPath);
        ArgumentNullException.ThrowIfNull(logger);

        _modelPath = modelPath;
        _logger = logger;

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model not found at {modelPath}");
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException($"Vocabulary file not found at {vocabPath}");
        }

        var sessionOptions = CreateSessionOptions();
        _session = new InferenceSession(modelPath, sessionOptions);
        _tokenizer = BertTokenizer.Create(vocabPath);

        // Infer dimension from output shape (usually [batch, tokens, dim])
        var outputNode = _session.OutputMetadata.Values.First();
        _embeddingDimension = outputNode.Dimensions.Last();

        Metadata = new EmbeddingGeneratorMetadata("OnnxRuntime-BERT", new Uri("file://" + modelPath), _modelPath);
        _logger.LogInformation("OnnxEmbeddingGenerator initialized. Model: {ModelPath}, Dim: {Dim}", modelPath, _embeddingDimension);
    }

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new GeneratedEmbeddings<Embedding<float>>();
        var inputs = values.ToList();

        if (inputs.Count == 0)
            return Task.FromResult(result);

        cancellationToken.ThrowIfCancellationRequested();

        // Tokenize batch
        var tokenizedIdsBatch = inputs.Select(text => _tokenizer.EncodeToIds(text)).ToList();
        int maxLen = tokenizedIdsBatch.Max(x => x.Count);
        int batchSize = inputs.Count;

        var inputIds = new DenseTensor<long>([batchSize, maxLen]);
        var attentionMask = new DenseTensor<long>([batchSize, maxLen]);
        var tokenTypeIds = new DenseTensor<long>([batchSize, maxLen]);

        for (int i = 0; i < batchSize; i++)
        {
            var ids = tokenizedIdsBatch[i];
            for (int j = 0; j < maxLen; j++)
            {
                if (j < ids.Count)
                {
                    inputIds[i, j] = ids[j];
                    attentionMask[i, j] = 1;
                    tokenTypeIds[i, j] = 0;
                }
                else
                {
                    inputIds[i, j] = 0; // Pad
                    attentionMask[i, j] = 0;
                    tokenTypeIds[i, j] = 0;
                }
            }
        }

        var inputsMap = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
        };

        using var outputs = _session.Run(inputsMap);
        var outputTensor = outputs.First().AsTensor<float>();

        // Mean Pooling over valid tokens
        for (int i = 0; i < batchSize; i++)
        {
            var embedding = MeanPool(outputTensor, attentionMask, i, maxLen);
            result.Add(new Embedding<float>(embedding));
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    private SessionOptions CreateSessionOptions()
    {
        var sessionOptions = new SessionOptions();
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA(0);
            _logger.LogInformation("CUDA execution provider enabled for ONNX Runtime.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enable CUDA execution provider. Falling back to CPU.");
            sessionOptions.AppendExecutionProvider_CPU();
        }
        return sessionOptions;
    }

    private float[] MeanPool(Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> outputTensor, DenseTensor<long> attentionMask, int batchIndex, int maxLen)
    {
        var embedding = new float[_embeddingDimension];
        int validTokens = 0;

        for (int j = 0; j < maxLen; j++)
        {
            if (attentionMask[batchIndex, j] == 1)
            {
                for (int k = 0; k < _embeddingDimension; k++)
                {
                    embedding[k] += outputTensor[batchIndex, j, k];
                }
                validTokens++;
            }
        }

        if (validTokens > 0)
        {
            for (int k = 0; k < _embeddingDimension; k++)
            {
                embedding[k] /= validTokens;
            }
        }

        return embedding;
    }
}
