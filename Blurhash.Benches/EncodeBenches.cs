using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using Blurhash.Benches.Properties;
using Blurhash.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Blurhash.Benches;

[InProcess]
public class EncodeBenches
{
    Pixel[,] sourceBitmap;
    string result = "LKNcQ++|O;ELQ4O=+vSd.7x[$+s+";

    [GlobalSetup]
    public void Setup()
    {
        sourceBitmap = Blurhasher.ConvertBitmap(Image.Load<Rgba32>(Resources.TestImage));
        // result = Core.Encode(sourceBitmap, 4, 3);
    }

    // 36.78 ms
    [Benchmark]
    public void Encode()
    {
        var newResult = Core.Encode(sourceBitmap, 4, 3);
        if (newResult != result) throw new Exception("Encode failed");
    }
}