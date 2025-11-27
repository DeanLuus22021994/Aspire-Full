using Microsoft.ML.OnnxRuntime.Tensors;

namespace ArcFaceSandbox.EmbeddingService;

/// <summary>
/// Executes ArcFace ONNX inference for a batch tensor and returns raw float outputs.
/// Abstracted for easier testing of the embedding service.
/// </summary>
public interface IArcFaceInferenceRunner : IDisposable
{
    ArcFaceModelInfo ModelInfo { get; }

    /// <summary>
    /// Runs inference for the supplied batch tensor and returns the raw float output.
    /// </summary>
    /// <param name="inputName">The ONNX input name.</param>
    /// <param name="batchTensor">Batch tensor in NCHW format.</param>
    float[] Run(string inputName, DenseTensor<float> batchTensor);
}
