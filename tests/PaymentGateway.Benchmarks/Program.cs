using BenchmarkDotNet.Running;
using PaymentGateway.Benchmarks;

var summary = BenchmarkRunner.Run<JsonSerializationBenchmark>();
