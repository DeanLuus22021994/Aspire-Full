using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.VectorStore;

namespace ArcFaceSandbox.UsersKernel.Tests.Fakes;

internal sealed class FakeArcFaceEmbeddingService : IArcFaceEmbeddingService
{
    private readonly ReadOnlyMemory<float> _vector;

    public FakeArcFaceEmbeddingService()
    {
        var data = new float[SandboxVectorStoreOptions.DefaultVectorSize];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = i / 100f;
        }

        _vector = data;
    }

    public ArcFaceModelInfo ModelInfo { get; } = new(
        "fake",
        "1.0.0",
        "cpu",
        "sha256",
        DateTime.UtcNow,
        SandboxVectorStoreOptions.DefaultVectorSize,
        112);

    public Task<ReadOnlyMemory<float>> GenerateAsync(Stream alignedFace, CancellationToken cancellationToken = default)
        => Task.FromResult(_vector);

    public async IAsyncEnumerable<ReadOnlyMemory<float>> GenerateBatchAsync(
        IEnumerable<Stream> alignedFaces,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var _ in alignedFaces)
        {
            yield return await GenerateAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        }
    }
}
