using System.Linq;
using ArcFaceSandbox.EmbeddingService;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ArcFaceSandbox.EmbeddingService.Tests;

public sealed class ArcFacePreprocessorTests
{
    [Fact]
    public async Task ToTensorAsync_ResizesAndNormalizes()
    {
        using var image = new Image<Rgb24>(ArcFacePreprocessor.TargetSize * 2, ArcFacePreprocessor.TargetSize * 2, new Rgb24(10, 20, 30));
        await using var stream = new MemoryStream();
        await image.SaveAsync(stream, new PngEncoder());
        stream.Position = stream.Length / 2; // ensure the method rewinds seekable streams

        var tensor = await ArcFacePreprocessor.ToTensorAsync(stream, CancellationToken.None);

        Assert.Equal(new[] { 1, 3, ArcFacePreprocessor.TargetSize, ArcFacePreprocessor.TargetSize }, tensor.Dimensions.ToArray());
        var blueChannelValue = tensor[0, 0, 0, 0];
        var normalizedBlue = (30 - 127.5f) / 128f;
        Assert.Equal(normalizedBlue, blueChannelValue, precision: 5);
    }
}
