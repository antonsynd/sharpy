using BenchmarkDotNet.Attributes;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

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

        // Every row below times a full compile, so every input has to survive one (#1224).
        CorpusGuard.AssertCompiles(_helloWorldSource, "hello_world.spy");
        CorpusGuard.AssertCompiles(_fibonacciSource, "fibonacci.spy");
        CorpusGuard.AssertCompiles(_classesSource, "classes.spy");
        CorpusGuard.AssertCompiles(_comprehensionsSource, "comprehensions.spy");
        CorpusGuard.AssertCompiles(_largeFunctionsSource, "large_functions.spy");
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
/// Throughput benchmark measuring compilation across the whole corpus.
/// </summary>
/// <remarks>
/// This row used to concatenate every corpus file into one compilation unit. That input has never
/// compiled (#1224): five of the six members define <c>main</c>, and <c>large_lexer_corpus.spy</c>
/// redefines <c>is_prime</c>, <c>gcd</c>, <c>lcm</c>, <c>factorial</c> and <c>Point</c> — 9 SPY0204
/// redefinition errors plus SPY0220/SPY0203 fallout. Compilation therefore stopped in semantic
/// analysis, so every recorded "Combined Corpus" number timed a partial pipeline, and its
/// "~160 lines" label described a 636-line input.
/// <para>
/// Concatenation cannot be repaired in place — the members are independent programs and collide by
/// construction. The corpus is compiled member by member instead, which is what a throughput row
/// over a corpus means anyway, keeps every input identical to the one its per-file row already
/// uses, and makes the reported line count true.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private string[] _sources = null!;
    private string[] _names = null!;
    private int _lineCount;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

        var files = Directory.GetFiles(corpusDir, "*.spy").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        _names = files.Select(Path.GetFileName).ToArray()!;
        _sources = files.Select(File.ReadAllText).ToArray();
        _lineCount = _sources.Sum(s => s.Split('\n').Length);

        for (var i = 0; i < _sources.Length; i++)
            CorpusGuard.AssertCompiles(_sources[i], _names[i]);

        Console.WriteLine($"Corpus: {_lineCount} lines from {files.Length} files");
    }

    [Benchmark(Description = "Whole Corpus (6 files, 632 lines)")]
    public int CompileWholeCorpus()
    {
        var compiled = 0;
        for (var i = 0; i < _sources.Length; i++)
        {
            var compiler = new Compiler();
            if (compiler.Compile(_sources[i], _names[i]).Success)
                compiled++;
        }

        return compiled;
    }

    /// <summary>
    /// Gets the total line count across the corpus.
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

        CorpusGuard.AssertCompiles(_largeFunctionsSource, "large_functions.spy");
    }

    [Benchmark(Description = "Memory: Large Functions (73 lines)")]
    public CompilationResult CompileLargeFunctions()
    {
        var compiler = new Compiler();
        return compiler.Compile(_largeFunctionsSource, "large_functions.spy");
    }
}

/// <summary>
/// Lexer isolation benchmarks.
/// Measures tokenization performance independently of parsing and semantic analysis.
/// </summary>
[MemoryDiagnoser]
public class LexerBenchmarks
{
    private string _largeCorpusSource = null!;
    private string _combinedSource = null!;
    private int _largeCorpusLineCount;
    private int _combinedLineCount;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

        // Large lexer corpus (~500 lines)
        _largeCorpusSource = File.ReadAllText(Path.Combine(corpusDir, "large_lexer_corpus.spy"));
        _largeCorpusLineCount = _largeCorpusSource.Split('\n').Length;

        // Combined corpus for even larger tokenization
        var files = Directory.GetFiles(corpusDir, "*.spy");
        var sources = files.Select(File.ReadAllText);
        _combinedSource = string.Join("\n\n", sources);
        _combinedLineCount = _combinedSource.Split('\n').Length;

        // Lexer rows need a complete token stream, nothing more: the concatenated corpus does not
        // type-check (see ThroughputBenchmarks), but that is invisible to tokenization, so these
        // two rows measure exactly what they claim (#1224).
        CorpusGuard.AssertTokenizes(_largeCorpusSource, "large_lexer_corpus.spy");
        CorpusGuard.AssertTokenizes(_combinedSource, "combined corpus");

        Console.WriteLine($"Large corpus: {_largeCorpusLineCount} lines");
        Console.WriteLine($"Combined corpus: {_combinedLineCount} lines");
    }

    [Benchmark(Description = "Lexer: Large Corpus (~500 lines)")]
    public IReadOnlyList<Token> Tokenize_LargeCorpus()
    {
        var lexer = new SharpyLexer(_largeCorpusSource);
        return lexer.TokenizeAll();
    }

    [Benchmark(Description = "Lexer: Combined Corpus (~700 lines)")]
    public IReadOnlyList<Token> Tokenize_CombinedCorpus()
    {
        var lexer = new SharpyLexer(_combinedSource);
        return lexer.TokenizeAll();
    }

    /// <summary>
    /// Gets the line count for throughput calculations.
    /// </summary>
    public int LargeCorpusLineCount => _largeCorpusLineCount;
    public int CombinedLineCount => _combinedLineCount;
}

/// <summary>
/// Parser isolation benchmarks.
/// Measures parsing performance using pre-tokenized input.
/// </summary>
[MemoryDiagnoser]
public class ParserBenchmarks
{
    private List<Token> _largeCorpusTokens = null!;
    private List<Token> _fibonacciTokens = null!;
    private List<Token> _classesTokens = null!;
    private int _largeCorpusLineCount;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

        // Pre-tokenize files for pure parsing measurement
        var largeSource = File.ReadAllText(Path.Combine(corpusDir, "large_lexer_corpus.spy"));
        var lexer1 = new SharpyLexer(largeSource);
        _largeCorpusTokens = lexer1.TokenizeAll().ToList();
        _largeCorpusLineCount = largeSource.Split('\n').Length;

        var fibSource = File.ReadAllText(Path.Combine(corpusDir, "fibonacci.spy"));
        var lexer2 = new SharpyLexer(fibSource);
        _fibonacciTokens = lexer2.TokenizeAll().ToList();

        var classesSource = File.ReadAllText(Path.Combine(corpusDir, "classes.spy"));
        var lexer3 = new SharpyLexer(classesSource);
        _classesTokens = lexer3.TokenizeAll().ToList();

        // These rows time a parse, so each token stream has to produce one (#1224).
        CorpusGuard.AssertParses(_largeCorpusTokens, "large_lexer_corpus.spy");
        CorpusGuard.AssertParses(_fibonacciTokens, "fibonacci.spy");
        CorpusGuard.AssertParses(_classesTokens, "classes.spy");

        Console.WriteLine($"Large corpus tokens: {_largeCorpusTokens.Count}");
        Console.WriteLine($"Fibonacci tokens: {_fibonacciTokens.Count}");
        Console.WriteLine($"Classes tokens: {_classesTokens.Count}");
    }

    [Benchmark(Description = "Parser: Large Corpus (~500 lines, pre-tokenized)")]
    public Module Parse_LargeCorpus()
    {
        var parser = new SharpyParser(_largeCorpusTokens);
        return parser.ParseModule();
    }

    [Benchmark(Description = "Parser: Fibonacci (22 lines, pre-tokenized)")]
    public Module Parse_Fibonacci()
    {
        var parser = new SharpyParser(_fibonacciTokens);
        return parser.ParseModule();
    }

    [Benchmark(Description = "Parser: Classes (35 lines, pre-tokenized)")]
    public Module Parse_Classes()
    {
        var parser = new SharpyParser(_classesTokens);
        return parser.ParseModule();
    }

    /// <summary>
    /// Gets the line count for throughput calculations.
    /// </summary>
    public int LargeCorpusLineCount => _largeCorpusLineCount;
}
