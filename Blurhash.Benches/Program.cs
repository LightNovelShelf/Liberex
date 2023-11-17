using BenchmarkDotNet.Running;

//BenchmarkRunner.Run<Benches.BlurhashBenches>();
//BenchmarkRunner.Run<Blurhash.Benches.ProcessPixelRowsBenches>();
//BenchmarkRunner.Run<Blurhash.Benches.MaxBenches>();
//BenchmarkRunner.Run<Blurhash.Benches.EncodeBenches>();

var benches = new Blurhash.Benches.EncodeBenches();
benches.Setup();
benches.Encode();