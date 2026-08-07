using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Sharpy.Compiler.Benchmarks;

// Run all benchmarks
var summaries = BenchmarkSwitcher.FromAssembly(typeof(CompilerBenchmarks).Assembly).Run(args);

// BenchmarkDotNet reports a benchmark that never ran — a GlobalSetup that threw, a validation
// error — under "Benchmarks with issues:" and then exits 0. That is how a corpus guard (#1224)
// would announce a broken input in the log while CI stayed green, which is the failure mode the
// guard exists to prevent. Translate issues into a non-zero exit so `benchmarks.yml` fails.
var failures = new List<string>();
foreach (var summary in summaries)
{
    foreach (var validationError in summary.ValidationErrors.Where(e => e.IsCritical))
        failures.Add($"{validationError.BenchmarkCase?.DisplayInfo ?? summary.Title}: {validationError.Message}");

    foreach (var report in summary.Reports.Where(r => !r.Success))
        failures.Add($"{report.BenchmarkCase.DisplayInfo}: did not run to completion");
}

if (failures.Count == 0)
    return 0;

Console.Error.WriteLine("Benchmark run failed — the following benchmarks did not produce results:");
foreach (var failure in failures)
    Console.Error.WriteLine($"  {failure}");

return 1;
