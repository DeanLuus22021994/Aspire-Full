using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Aspire_Full.DockerRegistry.Native;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.DockerRegistry.Services;

public class SafetensorsArtifactHandler
{
    private readonly ILogger<SafetensorsArtifactHandler> _logger;

    public SafetensorsArtifactHandler(ILogger<SafetensorsArtifactHandler> logger)
    {
        _logger = logger;
    }

    public async Task ProcessArtifactAsync(Stream stream, CancellationToken cancellationToken)
    {
        var reader = PipeReader.Create(stream);

        while (true)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (buffer.Length > 0)
            {
                ProcessBuffer(buffer);
            }

            reader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
    }

    private void ProcessBuffer(ReadOnlySequence<byte> buffer)
    {
        // Zero-copy processing if possible
        // In a real implementation, we would pin the memory and pass it to the native layer
        // For now, we just log the size

        // Example of pinning (simplified):
        // foreach (var segment in buffer)
        // {
        //     fixed (byte* p = segment.Span)
        //     {
        //         NativeTensorContext.ValidateTensorContent((float*)p, ...);
        //     }
        // }

        _logger.LogDebug("Processed buffer of size {Size}", buffer.Length);
    }
}
