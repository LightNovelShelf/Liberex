using BenchmarkDotNet.Attributes;
using Blurhash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blurhash.Benches;

[MemoryDiagnoser]
[InProcess]
public class MaxBenches
{
    private float[] _data;
    private float result;

    [Params(1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _data = new float[N];
        var rand = new Random(42);
        for (int i = 0; i < N; i++)
        {
            _data[i] = (float)rand.NextDouble();
        }
        result = AbsMaxExtensions.AbsMax((ReadOnlySpan<float>)_data.AsSpan());
    }

    [Benchmark]
    public float MaxAbs() => AbsMaxExtensions.AbsMax(_data);

    [Benchmark]
    public float MaxFallback() => AbsMaxExtensions.AbsMaxFallback(_data);

    [Benchmark]
    public float MaxLinq() => _data.Select(Math.Abs).Max();
}