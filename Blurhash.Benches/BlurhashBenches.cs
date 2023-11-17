using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using Blurhash.Benches.Properties;
using SixLabors.ImageSharp.PixelFormats;

namespace Blurhash.Benches;

[DotTraceDiagnoser]
[InProcess]
public class EncodeImageBenches
{
    [Benchmark]
    public void Encode()
    {
        var image = SixLabors.ImageSharp.Image.Load<Rgba32>(Resources.TestImage);
        // image.Mutate(x => x.Resize(image.Width * 300 / image.Height, 300));
        Blurhash.ImageSharp.Blurhasher.Encode(image, 2, 3);
    }
}