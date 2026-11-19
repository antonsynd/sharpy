using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

/// <summary>
/// Differential parse of the shared Sharpy/Python subset against CPython's <c>ast</c> (#1037).
///
/// <para>Where <see cref="AmbiguityHotspotPropertyTests"/> checks that Sharpy round-trips its own
/// output, this test uses CPython as an <em>independent oracle</em>. It generates ambiguity-hotspot
/// programs, keeps only those in the subset both languages share (an AST-predicate filter, not string
/// matching — see <see cref="SubsetFilter"/>), unparses each once, and hands the identical text to
/// both parsers. Two contracts are asserted:</para>
/// <list type="number">
///   <item>Biconditional — Sharpy accepts the text iff CPython (<c>ast.parse</c>) accepts it.</item>
///   <item>Skeleton — when both accept, their high-level structure agrees. Structure is a coarse
///     multiset of landmark node kinds (calls, lambdas, comprehensions, conditionals, subscripts,
///     attributes, and the four collection literals) whose Sharpy↔CPython correspondence is
///     unambiguous. Leaf / among-ambiguous kinds are excluded so benign representational differences
///     never register (the same landmark set the python comparer counts).</item>
/// </list>
///
/// <para>CPython side: <c>build_tools/differential_parse/compare_ast.py</c>, invoked once per batch
/// over a JSONL temp file (one <c>python3</c> process serves hundreds of samples). The test is a
/// no-op skip when a suitable <c>python3</c> is absent; CI pins the minor in the repo-root <c>.python-version</c>.</para>
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class CPythonDifferentialParseTests
{
    private readonly ITestOutputHelper _output;

    public CPythonDifferentialParseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The same ambiguity-hotspot module the round-trip fuzzer uses: a single top-level expression
    /// statement whose expression stacks lambda-body / subscript / tuple-call hotspots.
    /// </summary>
    private static Gen<Module> HotspotModule =>
        Gen.Int[1, 5].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenExpressions.AmbiguousHotspot(ctx).Select(e => new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement { Expression = e })
            });
        });

    /// <summary>In-subset samples a run collects at scale 1; the nightly multiplies it by <see cref="DepthScale"/>.</summary>
    private const int BaseTargetCount = 150;

    /// <summary>Candidate budget per in-subset sample wanted: the collection stops at
    /// <c>BaseTargetCount × CandidatesPerTarget × scale</c> draws if the target is not reached first.</summary>
    private const int CandidatesPerTarget = 10;

    /// <summary>The fixed floor on collected samples (strictly more than this).</summary>
    private const int MinSamples = 50;

    /// <summary>
    /// Floor on <c>in-subset samples / candidates drawn</c>: what "the subset filter may be over-broad"
    /// means, and independent of runner throughput. Measured 2026-11-19 (4 cores, scale 1, target 150)
    /// over 30 seeds (7919 × 1..30): min 0.495, median 0.553, max 0.649, mean 0.556, s.d. 0.032; every
    /// run reached 150 samples within 231–303 candidates and dual-parsed ≥ 149. 0.35 sits 0.145 (about
    /// 4.5 s.d.) below the measured min. A filter that rejects 90% of draws yields about 0.055.
    /// </summary>
    private const double MinSubsetRatio = 0.35;

    /// <summary>The largest <see cref="DepthScale"/> the nightly may ask for.</summary>
    private const int MaxDepthScale = 6;

    /// <summary>Replays a run: the seed a run prints, as a decimal <c>ulong</c>.</summary>
    public const string SeedVariable = "SHARPY_DIFFPARSE_SEED";

    /// <summary>
    /// Depth multiplier: <see cref="PropertyIterations.Injection"/> over its in-suite 100, so
    /// <c>SHARPY_PROPERTY_ITER=600</c> (the nightly soak) collects six times the samples. Floored at 1
    /// and capped at <see cref="MaxDepthScale"/>. <c>CsCheck_Time</c> plays no part: the collection is
    /// count-bounded, not time-bounded.
    /// </summary>
    private static int DepthScale() => Math.Clamp(PropertyIterations.Injection / 100, 1, MaxDepthScale);

    [SkippableFact]
    public void SharedSubset_SharpyAndCPythonAgree()
    {
        PythonInterpreter.RequireOrSkip(minMinor: 12);
        var python = PythonOracle.TryLocate()
            ?? throw new FileNotFoundException("python3 >= 3.12 is present but build_tools/differential_parse/compare_ast.py was not found.");

        int scale = DepthScale();
        ulong seed = RunSeed();
        var stopwatch = Stopwatch.StartNew();
        Collection collection = CollectSubsetSamples(
            targetCount: BaseTargetCount * scale, maxCandidates: BaseTargetCount * CandidatesPerTarget * scale, seed);
        List<Sample> samples = collection.Samples;
        double ratio = collection.Candidates == 0 ? 0 : (double)samples.Count / collection.Candidates;

        // Printed before the floors so a red log records the yield and the seed to replay it.
        _output.WriteLine(
            $"Collected {samples.Count} in-subset samples from {collection.Candidates} candidates "
            + $"(ratio {ratio:F3}, scale {scale}) in {stopwatch.Elapsed.TotalSeconds:F1} s; "
            + $"seed {collection.Seed} (replay: {SeedVariable}={collection.Seed}).");

        Assert.True(ratio >= MinSubsetRatio,
            $"In-subset ratio {ratio:F3} ({samples.Count}/{collection.Candidates}) is below {MinSubsetRatio}; "
            + $"the subset filter may be over-broad. Seed {collection.Seed}.");
        Assert.True(samples.Count > MinSamples,
            $"Too few in-subset samples generated ({samples.Count} of {collection.Candidates} candidates); "
            + $"the subset filter may be over-broad. Seed {collection.Seed}.");

        IReadOnlyDictionary<int, PythonVerdict> verdicts = python.ParseBatch(samples);

        var biconditionalMismatches = new List<string>();
        var skeletonMismatches = new List<string>();
        int bothParsed = 0;

        foreach (var s in samples)
        {
            if (!verdicts.TryGetValue(s.Id, out var v))
            {
                biconditionalMismatches.Add($"[no python verdict] {Quote(s.Source)}");
                continue;
            }

            if (s.SharpyParses != v.Ok)
            {
                biconditionalMismatches.Add(
                    $"Sharpy={(s.SharpyParses ? "accept" : "reject")} CPython={(v.Ok ? "accept" : "reject")}"
                    + $" :: {Quote(s.Source)}"
                    + (v.Error is null ? "" : $"  (py: {v.Error})"));
                continue;
            }

            if (s.SharpyParses && v.Ok)
            {
                bothParsed++;
                if (!LandmarksEqual(s.Landmarks!, v.Landmarks!))
                {
                    skeletonMismatches.Add(
                        $"Sharpy={FormatLandmarks(s.Landmarks!)} CPython={FormatLandmarks(v.Landmarks!)}"
                        + $" :: {Quote(s.Source)}");
                }
            }
        }

        _output.WriteLine(
            $"Differential parse: {samples.Count} samples, {bothParsed} dual-parsed, "
            + $"{biconditionalMismatches.Count} biconditional mismatches, "
            + $"{skeletonMismatches.Count} skeleton mismatches.");

        if (biconditionalMismatches.Count > 0 || skeletonMismatches.Count > 0)
        {
            var report = new StringBuilder();
            if (biconditionalMismatches.Count > 0)
            {
                report.AppendLine($"--- {biconditionalMismatches.Count} biconditional mismatch(es) ---");
                foreach (var m in biconditionalMismatches.Distinct().Take(60))
                    report.AppendLine(m);
            }
            if (skeletonMismatches.Count > 0)
            {
                report.AppendLine($"--- {skeletonMismatches.Count} skeleton mismatch(es) ---");
                foreach (var m in skeletonMismatches.Distinct().Take(60))
                    report.AppendLine(m);
            }
            _output.WriteLine(report.ToString());
        }

        Assert.True(biconditionalMismatches.Count == 0,
            $"Sharpy and CPython disagree on parseability for {biconditionalMismatches.Count} in-subset sample(s). "
            + "Each distinct root cause needs a gh issue + a TODO(#N) quarantine filter. See test output.");
        Assert.True(skeletonMismatches.Count == 0,
            $"Sharpy and CPython produced different parse skeletons for {skeletonMismatches.Count} in-subset sample(s). "
            + "See test output.");
        Assert.True(bothParsed > MinSamples,
            $"Only {bothParsed} of {samples.Count} in-subset samples parsed on both sides; the skeleton contract "
            + $"was barely exercised. Seed {collection.Seed}.");
    }

    /// <summary>
    /// Anchored shared-subset snippets: each must parse on both sides with an identical landmark
    /// skeleton. Pins the exact cases the generator covers randomly so a regression fails loudly,
    /// and doubles as a fast oracle smoke test independent of generation randomness.
    /// </summary>
    [SkippableTheory]
    [InlineData("f(lambda x: x[0], (a, b))")]
    [InlineData("first(lambda t: t[0], xs)")]
    [InlineData("x[a, b:c]")]
    [InlineData("x[a, b]")]
    [InlineData("obj.method(y).field")]
    [InlineData("[y for y in z if y]")]
    [InlineData("{k: v for k, v in items}")]
    [InlineData("g((a, b))")]
    [InlineData("h(a if b else c, key=1)")]
    [InlineData("super()")]
    [InlineData("data[lambda: n]")]
    // #1130: bare lambda slice bounds — landmark skeletons must agree, not just acceptance.
    [InlineData("x[lambda qux, m, tmp: t:]")]
    [InlineData("x[:lambda a: t:]")]
    [InlineData("x[lambda a: t : b]")]
    public void AnchoredSharedSubset_DualParsesWithMatchingSkeleton(string source)
    {
        PythonInterpreter.RequireOrSkip(minMinor: 12);
        var python = PythonOracle.TryLocate()
            ?? throw new FileNotFoundException("python3 >= 3.12 is present but build_tools/differential_parse/compare_ast.py was not found.");

        // Sharpy side.
        var tokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var ast = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors, $"Sharpy failed to parse anchored subset source: {source}");
        Assert.True(SubsetFilter.IsInSharedSubset(ast), $"Anchored source is not classified as shared-subset: {source}");
        var sharpyLandmarks = LandmarkCounter.Count(ast);

        // CPython side.
        var sample = new Sample(0, source, SharpyParses: true, sharpyLandmarks);
        var verdicts = python.ParseBatch(new[] { sample });
        Assert.True(verdicts.TryGetValue(0, out var v) && v.Ok,
            $"CPython rejected anchored subset source: {source} ({(v?.Error ?? "no verdict")})");
        Assert.True(LandmarksEqual(sharpyLandmarks, v!.Landmarks!),
            $"Skeleton mismatch for `{source}`: Sharpy={FormatLandmarks(sharpyLandmarks)} CPython={FormatLandmarks(v.Landmarks!)}");
    }

    /// <summary>
    /// Seeded regression for the divergence this differential fuzzer found (#1076, fixed): the
    /// #1011 lambda-param forward scan did not descend into a lambda used as a keyword-argument
    /// value, so the inner lambda's depth-0 ':' was mistaken for the outer lambda's body
    /// separator and the parse failed. CPython accepts every one of these (verified via
    /// ast.parse); Sharpy now does too (SkipLambdaHeader). Cases were minimized from fuzzer-found
    /// samples and reproduce deterministically.
    /// </summary>
    [Theory]
    [InlineData("f(lambda v: v, k=lambda: x)")]
    [InlineData("f(lambda v: v, key=lambda x: x)")]
    [InlineData("f(lambda t: t[0], k=lambda: x)")]
    [InlineData("f(lambda v: v, k=x, j=lambda: y)")]
    [InlineData("y(lambda acc: y, value=lambda x: True, key=items)")]
    [InlineData("n((\"test\",), value=lambda value: items, default=lambda bar, a: b\"\")")]
    public void LambdaKwargValueAfterAmbiguousPositionalLambda_Parses(string source)
    {
        var tokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors,
            $"Sharpy failed to parse CPython-valid source: {source}");
    }

    /// <summary>
    /// Seeded regression for the divergence this differential fuzzer found (#1130, fixed): a bare
    /// lambda as a slice bound whose next slice separator ':' is followed by an empty component
    /// failed with SPY0100 at the closing ']'. CPython accepts both (verified via ast.parse);
    /// Sharpy now reads them as slices too (the subscript-element context that suppresses the
    /// param-annotation heuristic). Minimized from a fuzzer-found sample.
    /// </summary>
    [Theory]
    [InlineData("x[lambda qux, m, tmp: t:]")]
    [InlineData("x[:lambda a: t:]")]
    public void LambdaSliceBoundBeforeEmptyComponent_Parses(string source)
    {
        var tokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors,
            $"Sharpy failed to parse CPython-valid source: {source}");
    }

    // ----------------------------------------------------------------------- //
    // Sample collection
    // ----------------------------------------------------------------------- //

    private static Collection CollectSubsetSamples(int targetCount, int maxCandidates, ulong seed)
    {
        var samples = new List<Sample>(targetCount);
        int candidates = 0;
        int batchSize = Math.Max(16, Environment.ProcessorCount * 8);
        var batch = new Sample?[batchSize];

        while (samples.Count < targetCount && candidates < maxCandidates)
        {
            int first = candidates;
            int count = Math.Min(batchSize, maxCandidates - first);
            Parallel.For(0, count, i => batch[i] = TryDrawSubsetSample(seed, first + i));

            // Walk the batch in candidate order so the result does not depend on the thread count.
            for (int i = 0; i < count && samples.Count < targetCount; i++)
            {
                candidates++;
                if (batch[i] is { } s)
                    samples.Add(s);
            }
        }

        return new Collection(samples, candidates, seed);
    }

    /// <summary>
    /// Candidate <paramref name="index"/> of the run seeded with <paramref name="seed"/>: drawn from
    /// its own <see cref="PCG"/> (seed and index mixed), so a candidate is the same whatever thread
    /// draws it. Null when the draw is outside the shared subset or does not unparse.
    /// </summary>
    private static Sample? TryDrawSubsetSample(ulong seed, int index)
    {
        var module = HotspotModule.Generate(new PCG(1u, CandidateSeed(seed, index)), null, out _);
        if (!SubsetFilter.IsInSharedSubset(module))
            return null;

        string source;
        try
        {
            source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
        }
        catch
        {
            return null;
        }

        bool sharpyParses;
        Module? reparsed = null;
        try
        {
            var tokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            reparsed = parser.ParseModule();
            sharpyParses = !parser.Diagnostics.HasErrors;
        }
        catch
        {
            sharpyParses = false;
        }

        var landmarks = sharpyParses && reparsed is not null
            ? LandmarkCounter.Count(reparsed)
            : null;

        return new Sample(index, source, sharpyParses, landmarks);
    }

    /// <summary>SplitMix64 of the run seed and the candidate index.</summary>
    private static ulong CandidateSeed(ulong seed, int index)
    {
        ulong z = seed + (0x9E3779B97F4A7C15UL * (ulong)(index + 1));
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>The run seed: <see cref="SeedVariable"/> when it parses, else a fresh random one.</summary>
    private static ulong RunSeed() =>
        ulong.TryParse(Environment.GetEnvironmentVariable(SeedVariable), out var seed)
            ? seed
            : (ulong)Random.Shared.NextInt64() ^ ((ulong)Random.Shared.Next() << 32);

    private sealed record Collection(List<Sample> Samples, int Candidates, ulong Seed);

    // ----------------------------------------------------------------------- //
    // Landmark comparison helpers
    // ----------------------------------------------------------------------- //

    private static bool LandmarksEqual(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
    {
        if (a.Count != b.Count)
            return false;
        foreach (var (k, v) in a)
        {
            if (!b.TryGetValue(k, out var bv) || bv != v)
                return false;
        }
        return true;
    }

    private static string FormatLandmarks(IReadOnlyDictionary<string, int> m) =>
        m.Count == 0 ? "{}" : "{" + string.Join(", ", m.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")) + "}";

    private static string Quote(string s) => "`" + s.Replace("\n", "\\n", StringComparison.Ordinal) + "`";

    private sealed record Sample(int Id, string Source, bool SharpyParses, IReadOnlyDictionary<string, int>? Landmarks);

    private sealed record PythonVerdict(bool Ok, string? Error, IReadOnlyDictionary<string, int>? Landmarks);

    // ======================================================================= //
    // Shared-subset AST filter
    // ======================================================================= //

    /// <summary>
    /// Closed-whitelist predicate: a module is in the shared subset only if every node is a shape
    /// CPython 3.12 can parse with the same meaning. Rejects Sharpy-only expression forms
    /// (<c>maybe</c>/<c>try</c>/<c>?</c>/<c>to</c>/<c>is T</c>/<c>??</c>/<c>match</c>-expression,
    /// ref/out/in arguments, template strings, star/spread, walrus), position-sensitive shapes we
    /// exclude wholesale to keep the biconditional clean (f-strings — micro-syntax differs; walrus —
    /// bare form is a Python error), and Sharpy-only flags on otherwise-shared nodes (backtick
    /// identifiers, null-conditional access, arrow lambdas, async comprehension clauses, numeric
    /// suffixes). One parser-bug quarantine remains: #1130 (lambda as slice bound), see
    /// <see cref="VisitSliceAccess"/>. Traverses via the AST visitor; never inspects source text.
    /// </summary>
    private sealed class SubsetFilter : AstVisitor
    {
        private bool _ok = true;

        public static bool IsInSharedSubset(Node node)
        {
            var f = new SubsetFilter();
            f.Visit(node);
            return f._ok;
        }

        private void Reject() => _ok = false;

        // Short-circuit: once out of the subset, stop walking.
        public override void Visit(Node node)
        {
            if (!_ok)
                return;
            base.Visit(node);
        }

        // --- Sharpy-only expression forms (no Python analog) ---
        public override void VisitMaybeExpression(MaybeExpression node) => Reject();
        public override void VisitTryExpression(TryExpression node) => Reject();
        public override void VisitQuestionMarkExpression(QuestionMarkExpression node) => Reject();
        public override void VisitTypeCoercion(TypeCoercion node) => Reject();
        public override void VisitTypeCheck(TypeCheck node) => Reject();
        public override void VisitMatchExpression(MatchExpression node) => Reject();
        public override void VisitAwaitExpression(AwaitExpression node) => Reject();
        public override void VisitModifiedArgument(ModifiedArgument node) => Reject();
        public override void VisitTStringLiteral(TStringLiteral node) => Reject();
        public override void VisitStarExpression(StarExpression node) => Reject();
        public override void VisitSpreadElement(SpreadElement node) => Reject();
        public override void VisitDictSpreadComprehension(DictSpreadComprehension node) => Reject();

        // Walrus: bare `x := y` is a Python syntax error and the position rules are subtle; exclude
        // the whole shape rather than track legality per context.
        public override void VisitWalrusExpression(WalrusExpression node) => Reject();

        // F-strings: Sharpy and CPython disagree on delimiter/escape micro-syntax often enough to be
        // noise for a parse biconditional; the round-trip fuzzer already covers them on the Sharpy
        // side. Bytes/plain strings stay in.
        public override void VisitFStringLiteral(FStringLiteral node) => Reject();

        // --- Shared nodes carrying a Sharpy-only flag ---
        public override void VisitIdentifier(Identifier node)
        {
            if (node.IsNameBacktickEscaped)
                Reject();
        }

        public override void VisitMemberAccess(MemberAccess node)
        {
            if (node.IsNullConditional)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitBinaryOp(BinaryOp node)
        {
            if (node.Operator is BinaryOperator.NullCoalesce or BinaryOperator.PipeForward)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitForClause(ForClause node)
        {
            if (node.IsAsync)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitLambdaExpression(LambdaExpression node)
        {
            if (node.IsArrowSyntax)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitIntegerLiteral(IntegerLiteral node)
        {
            if (node.Suffix is not null)
                Reject();
        }

        public override void VisitFloatLiteral(FloatLiteral node)
        {
            if (node.Suffix is not null)
                Reject();
        }
    }

    // ======================================================================= //
    // Landmark skeleton counter (Sharpy AST -> the python comparer's tokens)
    // ======================================================================= //

    /// <summary>
    /// Counts the same landmark multiset that <c>compare_ast.py</c> derives from CPython's AST.
    /// Node→token table (leaf / among-ambiguous kinds are intentionally uncounted):
    /// <list type="bullet">
    ///   <item>FunctionCall, SuperExpression (CPython reads <c>super()</c> as a Call) → Call</item>
    ///   <item>LambdaExpression → Lambda; ConditionalExpression → IfExp</item>
    ///   <item>List/Set/Dict comprehensions → ListComp/SetComp/DictComp</item>
    ///   <item>IndexAccess, SliceAccess, MultiAxisAccess → Subscript (a multi-axis subscript also
    ///     yields a Tuple, matching how CPython models <c>x[a, b]</c>)</item>
    ///   <item>MemberAccess → Attribute; List/Dict/Set/Tuple literals → List/Dict/Set/Tuple</item>
    /// </list>
    /// </summary>
    private sealed class LandmarkCounter : AstVisitor
    {
        private readonly Dictionary<string, int> _counts = new();

        public static Dictionary<string, int> Count(Node node)
        {
            var c = new LandmarkCounter();
            c.Visit(node);
            return c._counts;
        }

        private void Bump(string token) =>
            _counts[token] = _counts.TryGetValue(token, out var v) ? v + 1 : 1;

        public override void VisitFunctionCall(FunctionCall node) { Bump("Call"); DefaultVisit(node); }
        public override void VisitSuperExpression(SuperExpression node) { Bump("Call"); DefaultVisit(node); }
        public override void VisitLambdaExpression(LambdaExpression node) { Bump("Lambda"); DefaultVisit(node); }
        public override void VisitConditionalExpression(ConditionalExpression node) { Bump("IfExp"); DefaultVisit(node); }
        public override void VisitListComprehension(ListComprehension node) { Bump("ListComp"); DefaultVisit(node); }
        public override void VisitSetComprehension(SetComprehension node) { Bump("SetComp"); DefaultVisit(node); }
        public override void VisitDictComprehension(DictComprehension node) { Bump("DictComp"); DefaultVisit(node); }
        public override void VisitIndexAccess(IndexAccess node) { Bump("Subscript"); DefaultVisit(node); }
        public override void VisitSliceAccess(SliceAccess node) { Bump("Subscript"); DefaultVisit(node); }

        public override void VisitMultiAxisAccess(MultiAxisAccess node)
        {
            Bump("Subscript");
            // CPython models a comma-separated (>= 2 axis) subscript index as a Tuple; a single
            // axis (should not occur for MultiAxisAccess) would not.
            if (node.Dimensions.Length >= 2)
                Bump("Tuple");
            DefaultVisit(node);
        }

        public override void VisitMemberAccess(MemberAccess node) { Bump("Attribute"); DefaultVisit(node); }
        public override void VisitListLiteral(ListLiteral node) { Bump("List"); DefaultVisit(node); }
        public override void VisitDictLiteral(DictLiteral node) { Bump("Dict"); DefaultVisit(node); }
        public override void VisitSetLiteral(SetLiteral node) { Bump("Set"); DefaultVisit(node); }
        public override void VisitTupleLiteral(TupleLiteral node) { Bump("Tuple"); DefaultVisit(node); }
    }

    // ======================================================================= //
    // CPython oracle process wrapper
    // ======================================================================= //

    private sealed class PythonOracle
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        private PythonOracle(string pythonExe, string scriptPath)
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public static PythonOracle? TryLocate()
        {
            string? script = FindComparerScript();
            if (script is null)
                return null;

            foreach (var candidate in new[] { "python3", "python" })
            {
                if (HasSupportedVersion(candidate))
                    return new PythonOracle(candidate, script);
            }
            return null;
        }

        private static bool HasSupportedVersion(string exe)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                if (proc is null)
                    return false;
                string outText = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(10_000))
                {
                    try
                    { proc.Kill(entireProcessTree: true); }
                    catch { }
                    return false;
                }
                // Expect "Python 3.MINOR.patch"; require >= 3.12.
                var match = System.Text.RegularExpressions.Regex.Match(outText, @"Python (\d+)\.(\d+)");
                if (!match.Success)
                    return false;
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                return major > 3 || (major == 3 && minor >= 12);
            }
            catch
            {
                return false;
            }
        }

        private static string? FindComparerScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
                {
                    var candidate = Path.Combine(
                        dir.FullName, "build_tools", "differential_parse", "compare_ast.py");
                    return File.Exists(candidate) ? candidate : null;
                }
                dir = dir.Parent;
            }
            return null;
        }

        public IReadOnlyDictionary<int, PythonVerdict> ParseBatch(IEnumerable<Sample> samples)
        {
            string batchPath = Path.Combine(
                Path.GetTempPath(), $"sharpy-diffparse-{Guid.NewGuid():N}.jsonl");
            try
            {
                using (var writer = new StreamWriter(batchPath, append: false, new UTF8Encoding(false)))
                {
                    foreach (var s in samples)
                    {
                        // System.Text.Json handles all escaping of the source text.
                        var obj = JsonSerializer.Serialize(new { id = s.Id, source = s.Source });
                        writer.WriteLine(obj);
                    }
                }

                string stdout = RunComparer(batchPath);
                return ParseVerdicts(stdout);
            }
            finally
            {
                try
                { File.Delete(batchPath); }
                catch { }
            }
        }

        private string RunComparer(string batchPath)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.StartInfo.ArgumentList.Add(_scriptPath);
            proc.StartInfo.ArgumentList.Add("--batch");
            proc.StartInfo.ArgumentList.Add(batchPath);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(120_000))
            {
                try
                { proc.Kill(entireProcessTree: true); }
                catch { }
                throw new InvalidOperationException("compare_ast.py timed out.");
            }
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException(
                    $"compare_ast.py exited {proc.ExitCode}: {stderr}");

            return stdout.ToString();
        }

        private static IReadOnlyDictionary<int, PythonVerdict> ParseVerdicts(string stdout)
        {
            var result = new Dictionary<int, PythonVerdict>();
            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                int id = root.GetProperty("id").GetInt32();
                bool ok = root.GetProperty("ok").GetBoolean();

                string? error = null;
                Dictionary<string, int>? landmarks = null;
                if (ok)
                {
                    landmarks = new Dictionary<string, int>();
                    if (root.TryGetProperty("landmarks", out var lm))
                    {
                        foreach (var prop in lm.EnumerateObject())
                            landmarks[prop.Name] = prop.Value.GetInt32();
                    }
                }
                else if (root.TryGetProperty("error", out var err))
                {
                    error = err.GetString();
                }

                result[id] = new PythonVerdict(ok, error, landmarks);
            }
            return result;
        }
    }
}
