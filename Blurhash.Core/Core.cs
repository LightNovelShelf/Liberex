using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Blurhash
{
    public static class Core
    {
        /// <summary>
        /// Encodes a 2-dimensional array of pixels into a Blurhash string
        /// </summary>
        /// <param name="pixels">The 2-dimensional array of pixels to encode</param>
        /// <param name="componentsX">The number of components used on the X-Axis for the DCT</param>
        /// <param name="componentsY">The number of components used on the Y-Axis for the DCT</param>
        /// <param name="progressCallback">An optional progress handler to receive progress updates</param>
        /// <returns>The resulting Blurhash string</returns>
        public static string Encode(Pixel[,] pixels, int componentsX, int componentsY)
        {
            if (componentsX < 1) throw new ArgumentException("componentsX needs to be at least 1");
            if (componentsX > 9) throw new ArgumentException("componentsX needs to be at most 9");
            if (componentsY < 1) throw new ArgumentException("componentsY needs to be at least 1");
            if (componentsY > 9) throw new ArgumentException("componentsY needs to be at most 9");

            Span<Pixel> factors = stackalloc Pixel[componentsX * componentsY];
            Span<char> resultBuffer = stackalloc char[4 + 2 * componentsX * componentsY];

            var factorCount = componentsX * componentsY;

            var width = pixels.GetLength(0);
            var height = pixels.GetLength(1);

            var xCosines = new float[width];
            var yCosines = new float[height];

            for (var yComponent = 0; yComponent < componentsY; yComponent++)
                for (var xComponent = 0; xComponent < componentsX; xComponent++)
                {
                    float r = 0, g = 0, b = 0;
                    float normalization = (xComponent == 0 && yComponent == 0) ? 1 : 2;

                    for (var xPixel = 0; xPixel < width; xPixel++)
                    {
                        xCosines[xPixel] = MathF.Cos(MathF.PI * xComponent * xPixel / width);
                    }

                    for (var yPixel = 0; yPixel < height; yPixel++)
                    {
                        yCosines[yPixel] = MathF.Cos(MathF.PI * yComponent * yPixel / height);
                    }

                    for (var xPixel = 0; xPixel < width; xPixel++)
                        for (var yPixel = 0; yPixel < height; yPixel++)
                        {
                            var basis = xCosines[xPixel] * yCosines[yPixel];
                            var pixel = pixels[xPixel, yPixel];
                            r += basis * pixel.Red;
                            g += basis * pixel.Green;
                            b += basis * pixel.Blue;
                        }

                    var scale = normalization / (width * height);
                    factors[componentsX * yComponent + xComponent].Red = r * scale;
                    factors[componentsX * yComponent + xComponent].Green = g * scale;
                    factors[componentsX * yComponent + xComponent].Blue = b * scale;
                }

            var dc = factors[0];
            var acCount = componentsX * componentsY - 1;

            var sizeFlag = (componentsX - 1) + (componentsY - 1) * 9;
            sizeFlag.EncodeBase83(resultBuffer.Slice(0, 1));

            float maximumValue;
            if (acCount > 0)
            {
                // Get maximum absolute value of all AC components
                // ignore DC component
                Span<float> floats = MemoryMarshal.Cast<byte, float>(MemoryMarshal.AsBytes(factors[1..]));
                var actualMaximumValue = ((ReadOnlySpan<float>)floats).AbsMax();
                var quantizedMaximumValue = (int)Math.Max(0.0, Math.Min(82.0, Math.Floor(actualMaximumValue * 166 - 0.5)));
                maximumValue = ((float)quantizedMaximumValue + 1) / 166;
                quantizedMaximumValue.EncodeBase83(resultBuffer.Slice(1, 1));
            }
            else
            {
                maximumValue = 1;
                resultBuffer[1] = '0';
            }

            EncodeDc(dc.Red, dc.Green, dc.Blue).EncodeBase83(resultBuffer.Slice(2, 4));

            for (var yComponent = 0; yComponent < componentsY; yComponent++)
                for (var xComponent = 0; xComponent < componentsX; xComponent++)
                {
                    // Ignore DC component
                    if (xComponent == 0 && yComponent == 0) continue;

                    var factorIndex = componentsX * yComponent + xComponent;

                    EncodeAc(factors[factorIndex].Red, factors[factorIndex].Green, factors[factorIndex].Blue, maximumValue)
                        .EncodeBase83(resultBuffer.Slice(6 + (factorIndex - 1) * 2, 2));
                }

            return resultBuffer.ToString();
        }

        /// <summary>
        /// Decodes a Blurhash string into a 2-dimensional array of pixels
        /// </summary>
        /// <param name="blurhash">The blurhash string to decode</param>
        /// <param name="pixels">
        ///     A two-dimensional array that will be filled with the pixel data.<br />
        ///     First dimension is the width, second dimension is the height
        /// </param>
        /// <param name="punch">A value that affects the contrast of the decoded image. 1 means normal, smaller values will make the effect more subtle, and larger values will make it stronger.</param>
        /// <param name="progressCallback">An optional progress handler to receive progress updates</param>
        /// <returns>A 2-dimensional array of <see cref="Pixel"/>s </returns>
        public static void Decode(string blurhash, Pixel[,] pixels, float punch = 1.0f, IProgress<int> progressCallback = null)
        {
            if (blurhash.Length < 6)
            {
                throw new ArgumentException("Blurhash value needs to be at least 6 characters", nameof(blurhash));
            }

            var blurhashSpan = blurhash.AsSpan();

            var outputWidth = pixels.GetLength(0);
            var outputHeight = pixels.GetLength(1);

            var sizeFlag = blurhashSpan.Slice(0, 1).DecodeBase83();

            var componentsY = sizeFlag / 9 + 1;
            var componentsX = sizeFlag % 9 + 1;
            var componentCount = componentsX * componentsY;

            if (blurhash.Length != 4 + 2 * componentsX * componentsY)
            {
                throw new ArgumentException("Blurhash value is missing data", nameof(blurhash));
            }

            var quantizedMaximumValue = (float)blurhashSpan.Slice(1, 1).DecodeBase83();
            var maximumValue = (quantizedMaximumValue + 1.0f) / 166.0f;

            var coefficients = new Pixel[componentsX, componentsY];

            var componentIndex = 0;
            for (var yComponent = 0; yComponent < componentsY; yComponent++)
                for (var xComponent = 0; xComponent < componentsX; xComponent++)
                {
                    if (xComponent == 0 && yComponent == 0)
                    {
                        var value = blurhashSpan.Slice(2, 4).DecodeBase83();
                        coefficients[xComponent, yComponent] = DecodeDc(value);
                    }
                    else
                    {
                        var value = blurhashSpan.Slice(4 + componentIndex * 2, 2).DecodeBase83();
                        coefficients[xComponent, yComponent] = DecodeAc(value, maximumValue * punch);
                    }

                    componentIndex++;
                }

            for (var xPixel = 0; xPixel < outputWidth; xPixel++)
                for (var yPixel = 0; yPixel < outputHeight; yPixel++)
                {
                    ref var result = ref pixels[xPixel, yPixel];

                    result.Red = 0.0f;
                    result.Green = 0.0f;
                    result.Blue = 0.0f;
                }

            var xCosines = new float[outputWidth];
            var yCosines = new float[outputHeight];

            componentIndex = 1;
            for (var componentX = 0; componentX < componentsX; componentX++)
                for (var componentY = 0; componentY < componentsY; componentY++)
                {
                    for (var xPixel = 0; xPixel < outputWidth; xPixel++)
                    {
                        xCosines[xPixel] = MathF.Cos((MathF.PI * xPixel * componentX) / outputWidth);
                    }

                    for (var yPixel = 0; yPixel < outputHeight; yPixel++)
                    {
                        yCosines[yPixel] = MathF.Cos((MathF.PI * yPixel * componentY) / outputHeight);
                    }

                    var coefficient = coefficients[componentX, componentY];

                    for (var xPixel = 0; xPixel < outputWidth; xPixel++)
                        for (var yPixel = 0; yPixel < outputHeight; yPixel++)
                        {
                            ref var result = ref pixels[xPixel, yPixel];

                            var basis = xCosines[xPixel] * yCosines[yPixel];

                            result.Red += coefficient.Red * basis;
                            result.Green += coefficient.Green * basis;
                            result.Blue += coefficient.Blue * basis;
                        }

                    progressCallback?.Report(componentIndex * 100 / componentCount);
                    componentIndex++;
                }
        }

        private static int EncodeAc(float r, float g, float b, float maximumValue)
        {
            var quantizedR = (int)Math.Max(0, Math.Min(18, Math.Floor(MathUtils.SignPow(r / maximumValue, 0.5f) * 9 + 9.5)));
            var quantizedG = (int)Math.Max(0, Math.Min(18, Math.Floor(MathUtils.SignPow(g / maximumValue, 0.5f) * 9 + 9.5)));
            var quantizedB = (int)Math.Max(0, Math.Min(18, Math.Floor(MathUtils.SignPow(b / maximumValue, 0.5f) * 9 + 9.5)));

            return quantizedR * 19 * 19 + quantizedG * 19 + quantizedB;
        }

        private static int EncodeDc(float r, float g, float b)
        {
            var roundedR = MathUtils.LinearTosRgb(r);
            var roundedG = MathUtils.LinearTosRgb(g);
            var roundedB = MathUtils.LinearTosRgb(b);
            return (roundedR << 16) + (roundedG << 8) + roundedB;
        }

        private static Pixel DecodeDc(BigInteger value)
        {
            var intR = (int)value >> 16;
            var intG = (int)(value >> 8) & 255;
            var intB = (int)value & 255;
            return new Pixel(MathUtils.SRgbToLinear(intR), MathUtils.SRgbToLinear(intG), MathUtils.SRgbToLinear(intB));
        }

        private static Pixel DecodeAc(BigInteger value, float maximumValue)
        {
            var quantizedR = (float)(value / (19 * 19));
            var quantizedG = (float)((value / 19) % 19);
            var quantizedB = (float)(value % 19);

            var result = new Pixel(
                MathUtils.SignPow((quantizedR - 9.0f) / 9.0f, 2.0f) * maximumValue,
                MathUtils.SignPow((quantizedG - 9.0f) / 9.0f, 2.0f) * maximumValue,
                MathUtils.SignPow((quantizedB - 9.0f) / 9.0f, 2.0f) * maximumValue
            );

            return result;
        }
    }
}
