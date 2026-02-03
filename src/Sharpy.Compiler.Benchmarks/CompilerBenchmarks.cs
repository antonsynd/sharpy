using BenchmarkDotNet.Attributes;

namespace Sharpy.Compiler.Benchmarks;

/// <summary>
/// Benchmarks for the Sharpy compiler.
/// Uses only the public Compiler API to measure end-to-end compilation performance.
/// </summary>
[MemoryDiagnoser]
public class CompilerBenchmarks
{
    private string _helloWorldSource = null!;
    private string _fibonacciSource = null!;
    private string _classesSource = null!;
    private string _comprehensionsSource = null!;
    private string _largeFunctionsSource = null!;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

        _helloWorldSource = File.ReadAllText(Path.Combine(corpusDir, "hello_world.spy"));
        _fibonacciSource = File.ReadAllText(Path.Combine(corpusDir, "fibonacci.spy"));
        _classesSource = File.ReadAllText(Path.Combine(corpusDir, "classes.spy"));
        _comprehensionsSource = File.ReadAllText(Path.Combine(corpusDir, "comprehensions.spy"));
        _largeFunctionsSource = File.ReadAllText(Path.Combine(corpusDir, "large_functions.spy"));
    }

    [Benchmark(Description = "Hello World (4 lines)")]
    public CompilationResult Compile_HelloWorld()
    {
        var compiler = new Compiler();
        return compiler.Compile(_helloWorldSource, "hello_world.spy");
    }

    [Benchmark(Description = "Fibonacci (22 lines)")]
    public CompilationResult Compile_Fibonacci()
    {
        var compiler = new Compiler();
        return compiler.Compile(_fibonacciSource, "fibonacci.spy");
    }

    [Benchmark(Description = "Classes + Inheritance (35 lines)")]
    public CompilationResult Compile_Classes()
    {
        var compiler = new Compiler();
        return compiler.Compile(_classesSource, "classes.spy");
    }

    [Benchmark(Description = "Comprehensions (26 lines)")]
    public CompilationResult Compile_Comprehensions()
    {
        var compiler = new Compiler();
        return compiler.Compile(_comprehensionsSource, "comprehensions.spy");
    }

    [Benchmark(Description = "Large Functions (73 lines)")]
    public CompilationResult Compile_LargeFunctions()
    {
        var compiler = new Compiler();
        return compiler.Compile(_largeFunctionsSource, "large_functions.spy");
    }
}

/// <summary>
/// Throughput benchmark measuring compilation of combined corpus.
/// </summary>
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private string _combinedSource = null!;
    private int _lineCount;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

        // Combine all corpus files for throughput measurement
        var files = Directory.GetFiles(corpusDir, "*.spy");
        var sources = files.Select(File.ReadAllText);
        _combinedSource = string.Join("\n\n", sources);
        _lineCount = _combinedSource.Split('\n').Length;

        Console.WriteLine($"Combined corpus: {_lineCount} lines from {files.Length} files");
    }

    [Benchmark(Description = "Combined Corpus (~160 lines)")]
    public CompilationResult CompileCombinedCorpus()
    {
        var compiler = new Compiler();
        return compiler.Compile(_combinedSource, "combined.spy");
    }

    /// <summary>
    /// Gets the total line count of the combined corpus.
    /// Useful for calculating lines/second throughput.
    /// </summary>
    public int LineCount => _lineCount;
}

/// <summary>
/// Memory allocation benchmarks.
/// </summary>
[MemoryDiagnoser]
public class MemoryBenchmarks
{
    private string _largeFunctionsSource = null!;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
        _largeFunctionsSource = File.ReadAllText(Path.Combine(corpusDir, "large_functions.spy"));
    }

    [Benchmark(Description = "Memory: Large Functions (73 lines)")]
    public CompilationResult CompileLargeFunctions()
    {
        var compiler = new Compiler();
        return compiler.Compile(_largeFunctionsSource, "large_functions.spy");
    }
}
