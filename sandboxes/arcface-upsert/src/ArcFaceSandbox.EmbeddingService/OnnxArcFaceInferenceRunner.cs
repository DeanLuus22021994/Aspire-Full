using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Concrete inference runner that hosts the ONNX runtime session.
/// </summary>
internal sealed class OnnxArcFaceInferenceRunner : IArcFaceInferenceRunner
{
    private readonly ArcFaceEmbeddingOptions _options;
    private readonly ILogger<OnnxArcFaceInferenceRunner> _logger;
    private readonly InferenceSession _session;
    private readonly ArcFaceModelInfo _modelInfo;
    private bool _disposed;

    public OnnxArcFaceInferenceRunner(IOptions<ArcFaceEmbeddingOptions> options, ILogger<OnnxArcFaceInferenceRunner> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ValidateOptions(_options);

        if (_options.VerifyModelChecksum && !string.IsNullOrWhiteSpace(_options.ExpectedSha256))
        {
            VerifyModelChecksum(_options.ModelPath, _options.ExpectedSha256);
        }

        var (sessionOptions, provider) = CreateSessionOptions(_options, logger);
        _session = new InferenceSession(_options.ModelPath, sessionOptions);
        _modelInfo = BuildModelInfo(_session, _options, provider);
    }

    public ArcFaceModelInfo ModelInfo => _modelInfo;

    public float[] Run(string inputName, DenseTensor<float> batchTensor)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OnnxArcFaceInferenceRunner));
        }

        var input = NamedOnnxValue.CreateFromTensor(inputName, batchTensor);
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(new[] { input });
        return results.First().AsEnumerable<float>().ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _disposed = true;
    }

    private static void ValidateOptions(ArcFaceEmbeddingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ValidationException("ArcFace model path is required.");
        }

        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException("ArcFace ONNX model not found.", options.ModelPath);
        }
    }

    private static void VerifyModelChecksum(string path, string expectedSha256)
    {
        var normalizedExpected = expectedSha256.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var actual = Convert.ToHexString(sha.ComputeHash(stream));
        if (!actual.Equals(normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"ArcFace model checksum mismatch. Expected {normalizedExpected}, got {actual}.");
        }
    }

    private static (SessionOptions Options, ArcFaceExecutionProvider Provider) CreateSessionOptions(ArcFaceEmbeddingOptions options, ILogger logger)
    {
        var provider = options.ExecutionProvider == ArcFaceExecutionProvider.Auto
            ? DetectDefaultProvider()
            : options.ExecutionProvider;

        var sessionOptions = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
            EnableMemoryPattern = true,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        try
        {
            switch (provider)
            {
                case ArcFaceExecutionProvider.Cuda:
                    sessionOptions.AppendExecutionProvider_CUDA(options.CudaDeviceId);
                    break;
                case ArcFaceExecutionProvider.DirectMl:
                    sessionOptions.AppendExecutionProvider_DML();
                    break;
                default:
                    sessionOptions.AppendExecutionProvider_CPU();
                    break;
            }
        }
        catch (OnnxRuntimeException ex)
        {
            logger.LogWarning(ex, "Falling back to CPU execution provider for ArcFace embeddings.");
            sessionOptions.AppendExecutionProvider_CPU();
            provider = ArcFaceExecutionProvider.Cpu;
        }

        return (sessionOptions, provider);
    }

    private static ArcFaceExecutionProvider DetectDefaultProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ArcFaceExecutionProvider.DirectMl;
        }

        return ArcFaceExecutionProvider.Cpu;
    }

    private static ArcFaceModelInfo BuildModelInfo(InferenceSession session, ArcFaceEmbeddingOptions options, ArcFaceExecutionProvider provider)
    {
        var metadata = session.ModelMetadata;
        var versionValue = metadata?.Version ?? 0;
        return new ArcFaceModelInfo(
            metadata?.GraphName ?? metadata?.GraphDescription ?? "arcface_r100_v1",
            versionValue > 0 ? versionValue.ToString(CultureInfo.InvariantCulture) : metadata?.ProducerName ?? "unknown",
            provider.ToString().ToLowerInvariant(),
            options.ExpectedSha256 ?? "n/a",
            DateTime.UtcNow);
    }
}
