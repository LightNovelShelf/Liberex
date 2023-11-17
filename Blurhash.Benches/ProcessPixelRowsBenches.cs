using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using Blurhash;
using Blurhash.Benches.Properties;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Blurhash.Benches;

[InProcess]
public class ProcessPixelRowsBenches
{
    readonly Image<Rgba32> sourceBitmap = Image.Load<Rgba32>(Resources.TestImage);
    int width, height, bytesPerPixel;
    Pixel[,] result;

    [GlobalSetup]
    public void Setup()
    {
        width = sourceBitmap.Width;
        height = sourceBitmap.Height;
        bytesPerPixel = sourceBitmap.PixelType.BitsPerPixel / 8;
        result = new Pixel[width, height];
    }

    [Benchmark]
    public void ProcessPixelRows()
    {
        sourceBitmap.ProcessPixelRows(pixelAccessor =>
        {
            for (var y = 0; y < pixelAccessor.Height; y++)
            {
                var rgbValues = MemoryMarshal.AsBytes(pixelAccessor.GetRowSpan(y));

                var index = 0;

                for (var x = 0; x < width; x++)
                {
                    result[x, y].Red = MathUtils.SRgbToLinear(rgbValues[index]);
                    result[x, y].Green = MathUtils.SRgbToLinear(rgbValues[index + 1]);
                    result[x, y].Blue = MathUtils.SRgbToLinear(rgbValues[index + 2]);
                    index += bytesPerPixel;
                }
            }
        });
    }
}