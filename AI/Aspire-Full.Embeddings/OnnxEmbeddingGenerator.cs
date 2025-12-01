using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Aspire_Full.Embeddings;

public class OnnxEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly ILogger<OnnxEmbeddingGenerator> _logger;
    private readonly int _embeddingDimension;
    private readonly string _modelPath;

    public EmbeddingGeneratorMetadata Metadata { get; }

    public OnnxEmbeddingGenerator(string modelPath, string vocabPath, ILogger<OnnxEmbeddingGenerator> logger)
    {
        _modelPath = modelPath;
        _logger = logger;

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model not found at {modelPath}");
        }

        // Configure session options for CUDA (GPU)
        var sessionOptions = new SessionOptions();
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA(0); // Use GPU 0
            _logger.LogInformation("CUDA execution provider enabled for ONNX Runtime.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enable CUDA execution provider. Falling back to CPU.");
            sessionOptions.AppendExecutionProvider_CPU();
        }

        _session = new InferenceSession(modelPath, sessionOptions);

        // Initialize BertTokenizer from vocab file
        // Microsoft.ML.Tokenizers.BertTokenizer.Create(vocabPath)
        _tokenizer = BertTokenizer.Create(vocabPath);

        // Infer dimension from output shape (usually [batch, tokens, dim] or [batch, dim])
        // For sentence-transformers, it's usually [batch, tokens, dim] and we need to pool.
        // Assuming all-MiniLM-L6-v2 output shape is [-1, -1, 384]
        var outputNode = _session.OutputMetadata.Values.First();
        _embeddingDimension = outputNode.Dimensions.Last();

        Metadata = new EmbeddingGeneratorMetadata("OnnxRuntime-BERT", new Uri("file://" + modelPath), _modelPath);
        _logger.LogInformation("OnnxEmbeddingGenerator initialized. Model: {ModelPath}, Dim: {Dim}", modelPath, _embeddingDimension);
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>();
        var inputs = values.ToList();

        if (inputs.Count == 0)
            return Task.FromResult(result);

        // Batch processing
        // For simplicity in this implementation, we process the whole batch.
        // In production, you might want to chunk this if the batch is too large for GPU memory.

        // Tokenize
        // We need to pad the batch to the longest sequence
        // Using EncodeToIds to get token IDs directly
        var tokenizedIdsBatch = inputs.Select(text => _tokenizer.EncodeToIds(text)).ToList();
        int maxLen = tokenizedIdsBatch.Max(x => x.Count);
        int batchSize = inputs.Count;

        var inputIds = new DenseTensor<long>(new[] { batchSize, maxLen });
        var attentionMask = new DenseTensor<long>(new[] { batchSize, maxLen });
        var tokenTypeIds = new DenseTensor<long>(new[] { batchSize, maxLen });

        for (int i = 0; i < batchSize; i++)
        {
            var ids = tokenizedIdsBatch[i];
            for (int j = 0; j < maxLen; j++)
            {
                if (j < ids.Count)
                {
                    inputIds[i, j] = ids[j];
                    attentionMask[i, j] = 1;
                    tokenTypeIds[i, j] = 0; // Segment 0
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

        // Run Inference
        using var outputs = _session.Run(inputsMap);

        // Get last_hidden_state (usually the first output)
        // Shape: [batch_size, seq_len, hidden_size]
        var outputTensor = outputs.First().AsTensor<float>();

        // Mean Pooling
        for (int i = 0; i < batchSize; i++)
        {
            var embedding = new float[_embeddingDimension];
            int validTokens = 0;

            for (int j = 0; j < maxLen; j++)
            {
                if (attentionMask[i, j] == 1)
                {
                    for (int k = 0; k < _embeddingDimension; k++)
                    {
                        embedding[k] += outputTensor[i, j, k];
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

            result.Add(new Embedding<float>(embedding));
        }

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
