using BenchmarkDotNet.Running;
using F1Telemetry.Benchmarks.Ingestion;

BenchmarkRunner.Run<PacketParserBenchmarks>();
