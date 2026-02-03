using BenchmarkDotNet.Running;
using Sharpy.Compiler.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(CompilerBenchmarks).Assembly).Run(args);
