using System.ComponentModel.DataAnnotations;

namespace Aspire_Full.WebAssembly.Components;

public sealed class TensorJobComposerSubmission
{
    [Required]
    public string ModelId { get; set; } = string.Empty;

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public string? InputImageUrl { get; set; }

    public string ExecutionProvider { get; set; } = string.Empty;

    public bool PersistToVectorStore { get; set; }
}
