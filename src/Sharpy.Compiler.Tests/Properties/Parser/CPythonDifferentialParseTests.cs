using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
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
/// no-op skip when a suitable <c>python3</c> is absent; CI pins 3.12.</para>
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

    [Fact]
    public void SharedSubset_SharpyAndCPythonAgree()
    {
        var python = PythonOracle.TryLocate();
        if (python is null)
        {
            _output.WriteLine("SKIP: python3 >= 3.12 and compare_ast.py not both available.");
            return;
        }

        List<Sample> samples = CollectSubsetSamples(targetCount: 150, maxCandidates: 1500);
        Assert.True(samples.Count > 50,
            $"Too few in-subset samples generated ({samples.Count}); the subset filter may be over-broad.");

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
    }

    /// <summary>
    /// Anchored shared-subset snippets: each must parse on both sides with an identical landmark
    /// skeleton. Pins the exact cases the generator covers randomly so a regression fails loudly,
    /// and doubles as a fast oracle smoke test independent of generation randomness.
    /// </summary>
    [Theory]
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
    public void AnchoredSharedSubset_DualParsesWithMatchingSkeleton(string source)
    {
        var python = PythonOracle.TryLocate();
        if (python is null)
        {
            _output.WriteLine("SKIP: python3 >= 3.12 and compare_ast.py not both available.");
            return;
        }

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
    /// QUARANTINED divergence this differential fuzzer found (#1076): the #1011 lambda-param
    /// forward scan does not descend into a lambda used as a keyword-argument value, so the
    /// inner lambda's depth-0 ':' is mistaken for the outer lambda's body separator and the
    /// parse fails. CPython accepts every one of these (verified via ast.parse). Cases were
    /// minimized from fuzzer-found samples and reproduce deterministically. Un-skip (and
    /// remove the SubsetFilter.VisitFunctionCall quarantine) when #1076 is fixed.
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

    // ----------------------------------------------------------------------- //
    // Sample collection
    // ----------------------------------------------------------------------- //

    private static List<Sample> CollectSubsetSamples(int targetCount, int maxCandidates)
    {
        var collected = new ConcurrentQueue<Sample>();
        int nextId = -1;

        HotspotModule.Sample(module =>
        {
            if (collected.Count >= targetCount)
                return;
            if (!SubsetFilter.IsInSharedSubset(module))
                return;

            string source;
            try
            {
                source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            }
            catch
            {
                return;
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

            int id = Interlocked.Increment(ref nextId);
            collected.Enqueue(new Sample(id, source, sharpyParses, landmarks));
        }, iter: maxCandidates);

        return collected.OrderBy(s => s.Id).ToList();
    }

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
    /// suffixes). Also quarantines the known #1064, #1076, and #1078 shapes. Traverses via the
    /// AST visitor; never inspects source text.
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
            // TODO(#1078): CPython forbids a bare lambda as a binary operator's right operand
            // (`a or lambda: b`); Sharpy accepts it and the unparser emits it unparenthesized.
            if (node.Right is LambdaExpression)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        // TODO(#1078): CPython forbids a bare lambda as a unary operand (`not lambda: x`) and as
        // any non-first comparison-chain operand (`a < lambda: b`); Sharpy accepts both.
        public override void VisitUnaryOp(UnaryOp node)
        {
            if (node.Operand is LambdaExpression)
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitComparisonChain(ComparisonChain node)
        {
            if (node.Operands.Skip(1).Any(o => o is LambdaExpression))
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
            // TODO(#1078): CPython forbids a bare lambda as a comprehension iterator
            // (`[x for y in lambda: z]`); Sharpy accepts it.
            if (node.Iterator is LambdaExpression)
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
            // TODO(#1064): a comparison chain of 3+ operands used directly as a lambda body is
            // truncated on reparse; quarantine the shape until the parser fix lands.
            if (IsMultiComparisonChain(node.Body))
            {
                Reject();
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitConditionalExpression(ConditionalExpression node)
        {
            // TODO(#1064): the same truncation applies to a 3+-operand comparison chain used as the
            // final (else) branch of a conditional.
            if (IsMultiComparisonChain(node.ElseValue))
            {
                Reject();
                return;
            }
            // TODO(#1078): CPython forbids a bare lambda as the conditional's test
            // (`x if lambda: y else z`); Sharpy accepts it and the unparser emits it
            // unparenthesized. (A lambda then-value is fine: both sides read
            // `lambda: a if t else e` as a lambda whose body is the conditional, and the
            // landmark multiset is identical either way.)
            if (node.Test is LambdaExpression)
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

        private static bool IsMultiComparisonChain(Expression e) =>
            e is ComparisonChain { Operators.Length: >= 2 };
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
