using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Benchmarks;

/// <summary>
/// Isolated ParseText-vs-tree-rewrite micro-benchmark for the EmitLineDirectives-on codegen seam
/// (#1126). Reproduces exactly the two competing shapes of ProjectCompiler.CodeGen.cs:
/// <list type="bullet">
/// <item><description><b>Old on-branch</b> (pre-#1108, at 29892b541): serialize the emitter unit,
/// splice #line fixes as text (<see cref="LineDirectivePostProcessor"/>), reparse the processed text
/// (<c>CSharpSyntaxTree.ParseText</c>).</description></item>
/// <item><description><b>New on-branch</b> (#1108): serialize the emitter unit (planner snapshot),
/// plan (<see cref="LineDirectiveEditPlanner"/>), apply as a tree rewrite
/// (<see cref="LineDirectiveTreeRewriter"/>), serialize the rewritten unit (the csharpCode the
/// pipeline stores), and wrap via <c>CSharpSyntaxTree.Create</c> (zero-parse).</description></item>
/// </list>
/// Stage_* benchmarks break the new path into its components so an optimize-vs-revert decision can
/// see where the time goes. The emitter unit is produced once in GlobalSetup through the real
/// RoslynEmitter (captured at the ICodeEmitterFactory seam), so only the seam itself is measured.
/// </summary>
[MemoryDiagnoser]
[InProcess] // BDN's csproj toolchain cannot uniquely resolve this project when agent worktrees
            // shadow the repo; the seam is pure managed code, so in-process measurement is sound.
public class LineDirectiveSeamBenchmarks
{
    [Params("classes.spy", "large_functions.spy", "large_lexer_corpus.spy")]
    public string CorpusFile { get; set; } = null!;

    private CompilationUnitSyntax _root = null!;
    private string _emitted = null!;
    private string _processed = null!;
    private LineDirectiveEditPlan _plan = null!;
    private CompilationUnitSyntax _rewritten = null!;

    [GlobalSetup]
    public void Setup()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
        var source = File.ReadAllText(Path.Combine(corpusDir, CorpusFile));

        var factory = new CapturingEmitterFactory();
        var options = new CompilerOptions
        {
            References = new[] { Path.Combine(AppContext.BaseDirectory, "Sharpy.Core.dll") },
            OutputType = "exe",
            WarningsAsErrors = false
        };
        var result = new Compiler(options, NullLogger.Instance, factory).Compile(source, CorpusFile);
        if (!result.Success || factory.Captured.Count == 0)
            throw new InvalidOperationException($"corpus member {CorpusFile} must compile cleanly to be benchmarked");

        // A single-file compile normally captures one unit; if an auxiliary unit ever appears, the
        // module unit (the largest) is the representative input.
        _root = factory.Captured.OrderByDescending(u => u.FullSpan.Length).First();

        // Precomputed inputs for the Stage_* benchmarks (the full-path benchmarks recompute these,
        // mirroring production).
        _emitted = _root.ToFullString();
        _processed = LineDirectivePostProcessor.Process(_emitted);
        _plan = LineDirectiveEditPlanner.Plan(_emitted);
        _rewritten = LineDirectiveTreeRewriter.Apply(_root, _plan);

        if (_rewritten.ToFullString() != _processed)
            throw new InvalidOperationException("rewriter output must be byte-identical to the oracle (#1108)");

        Console.WriteLine(
            $"{CorpusFile}: {_emitted.Length:N0} chars emitted, {_plan.Edits.Count} line-directive edit(s)");
    }

    // ------------------------------------------------------------------------------------------
    // The two full seam shapes (what production ran before/after #1108). Both start from the
    // emitter unit and include the initial ToFullString, as ProjectCompiler.CodeGen.cs does.
    // ------------------------------------------------------------------------------------------

    [Benchmark(Baseline = true, Description = "old on-branch: serialize + Process + ParseText")]
    public SyntaxTree OldOnBranch_ProcessThenParseText()
    {
        var emitted = _root.ToFullString();
        var processed = LineDirectivePostProcessor.Process(emitted);
        return CSharpSyntaxTree.ParseText(
            processed, options: CSharpParseOptions.Default, path: "bench.cs", encoding: Encoding.UTF8);
    }

    [Benchmark(Description = "new on-branch: serialize + Plan + Apply + serialize + Create")]
    public SyntaxTree NewOnBranch_PlanApplySerializeCreate()
    {
        var emitted = _root.ToFullString();
        var plan = LineDirectiveEditPlanner.Plan(emitted);
        var rewritten = LineDirectiveTreeRewriter.Apply(_root, plan);
        CsharpCodeSink = rewritten.ToFullString();
        return CSharpSyntaxTree.Create(
            rewritten, options: CSharpParseOptions.Default, path: "bench.cs", encoding: Encoding.UTF8);
    }

    /// <summary>Consumes the csharpCode string the pipeline stores, so it cannot be elided.</summary>
    public string? CsharpCodeSink;

    // ------------------------------------------------------------------------------------------
    // Stage breakdown of the new path (inputs precomputed in GlobalSetup).
    // ------------------------------------------------------------------------------------------

    [Benchmark(Description = "stage: LineDirectiveEditPlanner.Plan")]
    public object Stage_Plan() => LineDirectiveEditPlanner.Plan(_emitted);

    [Benchmark(Description = "stage: LineDirectiveTreeRewriter.Apply")]
    public CompilationUnitSyntax Stage_Apply() => LineDirectiveTreeRewriter.Apply(_root, _plan);

    [Benchmark(Description = "stage: rewritten.ToFullString")]
    public string Stage_SerializeRewritten() => _rewritten.ToFullString();

    [Benchmark(Description = "stage: ParseText of processed text only")]
    public SyntaxTree Stage_ParseTextOnly() =>
        CSharpSyntaxTree.ParseText(
            _processed, options: CSharpParseOptions.Default, path: "bench.cs", encoding: Encoding.UTF8);

    // ------------------------------------------------------------------------------------------
    // Capturing emitter factory: records each emitter-built unit before the pipeline rewrites it
    // (same seam ReparseEquivalenceConformanceTests uses).
    // ------------------------------------------------------------------------------------------

    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<CompilationUnitSyntax> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), Captured);

        private sealed class CapturingEmitter : ICodeEmitter
        {
            private readonly ICodeEmitter _inner;
            private readonly List<CompilationUnitSyntax> _sink;

            public CapturingEmitter(ICodeEmitter inner, List<CompilationUnitSyntax> sink)
            {
                _inner = inner;
                _sink = sink;
            }

            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                var unit = _inner.GenerateCompilationUnit(module);
                _sink.Add(unit);
                return unit;
            }
        }
    }
}
