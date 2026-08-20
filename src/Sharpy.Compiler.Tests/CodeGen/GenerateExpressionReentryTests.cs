using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using AstIdentifier = Sharpy.Compiler.Parser.Ast.Identifier;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// No AST expression node is generated twice within one statement scope (#1334).
///
/// <para><b>Defect class.</b> The emitter reaching the same sub-expression twice emits code whose
/// <em>value</em> is right and whose <em>effect count</em> is wrong: <c>obj.prop += 1</c> that
/// calls the getter twice, <c>xs[f()] += 1</c> that evaluates <c>f()</c> twice. The generated C#
/// compiles, the snapshot matches, and the fixture prints the expected number — the divergence is
/// visible only if the duplicated sub-expression has an observable side effect and only if a test
/// exercises exactly that. Both known instances (the augmented-assignment indexer, then
/// <c>abc5bf4b0</c>'s <c>MemberReadIsPlainField</c> property getter) were found by someone writing
/// a single-evaluation test for one shape; <c>AugmentedAssignmentSingleEvaluationTests</c> pins
/// thirteen of them by hand. This is the same property stated once, over the corpus.</para>
///
/// <para><b>Mechanism.</b> <c>GenerateExpression</c> is the single wrapper every expression passes
/// through (it became so for #1251's sequence materialization). A test-side
/// <see cref="ICodeEmitterFactory"/> installs a recorder there; the recorder counts nodes by
/// reference within a scope opened per Sharpy statement. Production emit sees a null field.</para>
///
/// <para><b>Corpus: every executing single-file fixture, not the snapshot subset.</b> The ~130
/// <c>.expected.cs</c> fixtures were the intended scope; the executing corpus is 1,720 and costs
/// ~9s at DOP 8, so scoping down bought nothing and cost coverage.</para>
///
/// <para><b>Two tripwires, one defect class — the split closed by #1351.</b> Double evaluation
/// arrives two ways, and until #1351 this sweep saw only one of them.</para>
///
/// <para><i>Re-generation</i> — the emitter reaching the same node twice — is counted at
/// <c>GenerateExpression</c>. Red-verified: reverting the <c>GenerateBinaryOp</c> pipe-forward fix
/// this sweep motivated turns it red on <c>functions/pipe_basic</c> and
/// <c>functions/pipe_with_partial</c>, naming the duplicated nodes.</para>
///
/// <para><i>Re-splicing</i> — ONE generation inserted into TWO syntax positions — was invisible
/// here, and it is the half both known bugs came from. <c>HoistAugmentedTargetOperand</c> takes
/// ALREADY-GENERATED syntax and the caller splices it into the read and the write;
/// <c>GenerateExpression</c> ran once, so a node-entry counter at the choke point cannot see the
/// duplication by construction. Measured: inverting <c>MemberReadIsPlainField</c> — reintroducing
/// <c>abc5bf4b0</c>, the bug this guard was specified against — left the sweep GREEN.</para>
///
/// <para>#1351 closes that by counting splices too: <c>HoistAugmentedTargetOperand</c> reports
/// every operand it declines to hoist (<c>IExpressionGenerationRecorder.OnSplice</c>, null in
/// production), and an operand that is not repeatable by STRUCTURE alone fails. The exemption is
/// deliberately not the emitter's own verdict — see <c>IsSpliceRepeatable</c> — so the guard
/// cannot be silenced by the decision it exists to check.</para>
///
/// <para><b>Mutation, run 2026-08-14 at HEAD <c>c09a8fd68</c>.</b> Procedure: replace the body of
/// <c>MemberReadIsPlainField</c> (<c>RoslynEmitter.Statements.Assignments.cs</c>) after its
/// null-receiver guard with <c>return true;</c> — the pre-<c>abc5bf4b0</c> behavior, where a
/// property read counts as a plain field — then run this sweep. <b>Observed:</b> FAILED with
/// <c>single_evaluation_property_target_1227: MemberAccess at 43:8</c> and <c>at 48:5</c>, on the
/// counters <c>0 re-entries, 2 re-splices</c>. Both halves of that count matter: the re-generation
/// counter was still blind (0), exactly as this doc recorded before #1351, and the new splice
/// tripwire caught it. Reverted.</para>
///
/// <para>The hand-written backstops stay: <c>single_evaluation_*.spy</c> (3 fixtures) and
/// <c>AugmentedAssignmentSingleEvaluationTests</c> check observable effect COUNTS at runtime,
/// which is a different question from whether a hoist was declined.</para>
///
/// <para><b>Not to be confused with <c>d2903ad9d</c></b>, which cites #1351 but fixed a different
/// defect — the uncompilable-fixture skip-list ratchet below. That commit is not progress on the
/// re-splicing gap; this is.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class GenerateExpressionReentryTests
{
    private readonly ITestOutputHelper _output;

    public GenerateExpressionReentryTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Valid single-file programs — the same eligibility the metamorphic sweep uses. Error
    /// fixtures are excluded because a compile that stops early generates nothing to count.
    /// </summary>
    private static List<TestFixtureInfo> Eligible()
        => FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsMultiFile
                     && f.ExpectedFile is not null
                     && f.ErrorFile is null
                     && f.RuntimeErrorFile is null)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void ExecutingCorpus_GeneratesEachExpressionNodeAtMostOncePerStatement()
    {
        var fixtures = Eligible();

        // A sweep with an empty corpus passes for free — the failure mode a guard must not have.
        fixtures.Should().HaveCountGreaterThan(1_000,
            "the executing fixture corpus is what this property sweeps");

        var stopwatch = Stopwatch.StartNew();
        var offenders = new ConcurrentBag<string>();
        var splicers = new ConcurrentBag<string>();
        var uncompilable = new ConcurrentBag<string>();

        Parallel.ForEach(
            fixtures,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) },
            fixture =>
            {
                var recorder = new ReentryRecorder();
                var options = new CompilerOptions
                {
                    OutputType = "exe",
                    WarningsAsErrors = false,
                    Features = fixture.Features.Count == 0
                        ? FeatureFlags.None
                        : FeatureFlags.None.Enable(fixture.Features),
                };

                CompilationResult result;
                try
                {
                    result = new Compiler(options, NullLogger.Instance, new RecordingEmitterFactory(recorder))
                        .Compile(File.ReadAllText(fixture.SpyFilePath), Path.GetFileName(fixture.SpyFilePath));
                }
                catch (Exception ex)
                {
                    uncompilable.Add($"{fixture.TestName}: threw {ex.GetType().Name}: {ex.Message}");
                    return;
                }

                // Reported, not asserted: this seam compiles without the fixture harness's extra
                // references, so a fixture that needs one has nothing to say about re-entry. The
                // count going UP is what would matter, which is why it is in the output.
                if (!result.Success)
                {
                    uncompilable.Add(fixture.TestName);
                    return;
                }

                foreach (var reentry in recorder.Reentries)
                    offenders.Add($"{fixture.TestName}: {reentry}");
                foreach (var splice in recorder.Splices)
                    splicers.Add($"{fixture.TestName}: {splice}");
            });

        stopwatch.Stop();
        _output.WriteLine(
            $"Swept {fixtures.Count} fixtures in {stopwatch.Elapsed.TotalSeconds:F1}s "
            + $"({uncompilable.Count} did not compile through this seam, {offenders.Count} "
            + $"re-entries, {splicers.Count} re-splices).");
        foreach (var offender in offenders.OrderBy(o => o, StringComparer.Ordinal))
            _output.WriteLine($"  {offender}");
        foreach (var splicer in splicers.OrderBy(s => s, StringComparer.Ordinal))
            _output.WriteLine($"  {splicer}");

        offenders.Should().BeEmpty(
            "an expression generated more than once in a statement emits its side effects more "
            + "than once — correct value, wrong effect count (#1334)");

        splicers.Should().BeEmpty(
            "an operand spliced into both the read and the write of one augmented assignment "
            + "without a hoist is EVALUATED twice — `xs[idx()] += 1` calling idx() twice where "
            + "CPython calls it once (#1227). The emitter declined to hoist it, which is only "
            + "correct when re-evaluating the operand is unobservable; a non-leaf operand reaching "
            + "here means IsRepeatableTargetOperand said yes to something that runs code. This is "
            + "the re-SPLICING half of the single-evaluation class (#1351) — the half both known "
            + "bugs came from, and the half the re-generation counter above cannot see, because "
            + "generation happened exactly once and the duplication is downstream of it.");

        // A fixture that does not compile through this seam is SKIPPED, and a skipped fixture
        // witnesses nothing. That was reported and never asserted, which is the same defect as
        // #1432 (a property suite whose samples were all rejected by the front end reported
        // "0/0 emitted" and passed): the corpus guard above catches an EMPTY corpus, but not one
        // where every fixture was silently dropped. ITestOutputHelper output is invisible on a
        // passing test, so nobody would see the count climb. Ratcheted instead — if a change
        // makes more fixtures uncompilable through this seam, that is either a real regression
        // or a deliberate move that should lower this number in the same commit.
        uncompilable.Count.Should().BeLessThanOrEqualTo(MaxUncompilableFixtures,
            $"fixtures skipped by this sweep are not swept; the baseline is "
            + $"{MaxUncompilableFixtures} and these did not compile:\n  "
            + string.Join("\n  ", uncompilable.OrderBy(u => u, StringComparer.Ordinal).Take(20)));
    }

    /// <summary>
    /// How many fixtures may fail to compile through the recording seam, which lacks the fixture
    /// harness's extra references. Measured at <c>8c09a923f</c>: <b>6 of 1,824</b>, and all six are
    /// the same shape — <c>builtins/qualified_constructor_reference_pinned</c>,
    /// <c>builtins_from_import_resolves</c>, <c>builtins_qualified_call</c>,
    /// <c>builtins_qualified_construction</c>, <c>builtins_qualified_escapes_shadow</c>,
    /// <c>builtins_qualified_type</c>. They reference the <c>builtins</c> module, which this seam
    /// does not register, so they cannot compile here and have nothing to say about re-entry.
    ///
    /// <para>Raised to <b>7</b> for <c>builtin_from_import_alias_1383</c>, which is the seventh of
    /// exactly that shape: it spells <c>from builtins import len as blen</c>, the same import this
    /// seam cannot resolve for <c>builtins_from_import_resolves</c> already in the list. Nothing
    /// about re-entry changed — the sweep still reports 0 re-entries — and the fixture is swept by
    /// the file-based harness, which does register the module.</para>
    ///
    /// <para>Raised to <b>8</b> for <c>builtins_isinstance_qualified_1381</c>, the eighth of the
    /// same shape: it spells <c>import builtins</c> + <c>builtins.isinstance(...)</c>, the module
    /// reference this seam does not register. Nothing about re-entry changed — the sweep still
    /// reports 0 re-entries — and the fixture is swept by the file-based harness plus the
    /// metamorphic and differential sweeps, which do register the module.</para>
    ///
    /// <para>Raised to <b>9</b> for <c>builtins_qualified_value_position_1463</c>, the ninth of the
    /// same shape: it spells <c>import builtins</c> + <c>builtins.int</c> in value position. The
    /// seam does not register the builtins module.</para>
    ///
    /// <para>Raised to <b>11</b> for the #1527 alias transparency fixtures:
    /// <c>builtin_type_from_import_alias_1489</c> and
    /// <c>type_alias_call_transparency_1527</c> — same <c>from builtins import</c> shape.</para>
    ///
    /// <para>Raised to <b>12</b> for <c>builtins_isinstance_tuple_qualified_1532</c>, the twelfth
    /// of the same shape: <c>import builtins</c> + <c>builtins.isinstance(x, (int, str))</c>, the
    /// qualified structural-tuple parity fixture added by the plan-930411 verification round.
    /// Nothing about re-entry changed — the sweep still reports 0 re-entries — and the fixture is
    /// swept by the file-based harness, which does register the module.</para>
    ///
    /// <para>Lower this whenever the real number drops; never raise it without saying why in the
    /// same commit. A rise means either a regression or a deliberate scope change, and both are
    /// things a reader must be told rather than left to infer from a silently larger skip list.</para>
    /// </summary>
    private const int MaxUncompilableFixtures = 12;

    // ----------------------------------------------------------------------------------------- //

    private sealed class RecordingEmitterFactory : ICodeEmitterFactory
    {
        private readonly IExpressionGenerationRecorder _recorder;

        public RecordingEmitterFactory(IExpressionGenerationRecorder recorder) => _recorder = recorder;

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
        {
            var emitter = new RoslynEmitter(context, cancellationToken);
            emitter.SetGenerationRecorder(_recorder);
            return emitter;
        }
    }

    private sealed class ReentryRecorder : IExpressionGenerationRecorder
    {
        private readonly Stack<Dictionary<Expression, int>> _scopes = new();

        /// <summary>
        /// Splice counts, kept in a scope stack of their own rather than shared with the
        /// generation counts above. Sharing one dictionary would cross-contaminate: an operand
        /// generated once and then spliced would reach count 2 and be reported as a
        /// re-GENERATION, which it is not.
        /// </summary>
        private readonly Stack<Dictionary<Expression, int>> _spliceScopes = new();

        public List<string> Reentries { get; } = new();

        public List<string> Splices { get; } = new();

        public void OnGenerate(Expression expression)
        {
            if (_scopes.Count == 0 || IsObservablyRepeatable(expression))
                return;

            var scope = _scopes.Peek();
            var count = scope.TryGetValue(expression, out var n) ? n + 1 : 1;
            scope[expression] = count;

            // Reported once per node, on the second generation: a node reached five times is one
            // finding, not four.
            if (count == 2)
            {
                Reentries.Add(
                    $"{expression.GetType().Name} at {expression.LineStart}:{expression.ColumnStart} "
                    + "generated twice in one statement");
            }
        }

        /// <summary>
        /// Leaf reads with no observable effect. The emitter deliberately re-emits these instead
        /// of hoisting — <c>1 &lt; x &lt; 10</c> lowers to <c>1 &lt; x &amp;&amp; x &lt; 10</c>,
        /// which is byte-identical to what it emitted before hoisting existed and is what the C#
        /// snapshots pin. Generating a local read twice duplicates nothing observable, so counting
        /// it would make the tripwire report the emitter's own repeatability rule
        /// (<c>IsRepeatableTargetOperand</c>) as a defect.
        ///
        /// <para>Only LEAVES are exempt, deliberately: a compound expression built from repeatable
        /// parts still duplicates the operation, and a member read can run a getter — the exact
        /// shape <c>abc5bf4b0</c> got wrong. Verified against the live compiler:
        /// <c>1 &lt; bump() &lt; 10</c> calls <c>bump()</c> once, matching CPython.</para>
        ///
        /// <para><b>Tied to the emitter by construction (#1351).</b> This calls the emitter's own
        /// <c>RoslynEmitter.IsRepeatableLeafOperand</c> rather than restating the list. The two
        /// used to be hand-synced copies, which is the failure mode where a new leaf kind is added
        /// on one side only: the tripwire then either reports the emitter's repeatability rule as
        /// a defect, or goes blind to a shape it should judge. One definition, two consumers.</para>
        /// </summary>
        private static bool IsObservablyRepeatable(Expression expression)
            => RoslynEmitter.IsRepeatableLeafOperand(expression);

        /// <summary>
        /// The re-SPLICE exemption: repeatable by STRUCTURE alone, judged by the emitter's own
        /// recursion with its one type-dependent arm answering "no"
        /// (<c>RoslynEmitter.IsRepeatableWithoutTypeInformation</c>). Leaves and pure compositions
        /// of them qualify — <c>xs[-1] += 1</c>'s negated literal, <c>xs[i + 1] += 1</c>'s
        /// arithmetic index — which is what the corpus measured at HEAD: 2 splices, both
        /// <c>UnaryOp</c>, both in <c>arrays/array_negative_index_compound</c>, neither a defect.
        ///
        /// <para><b>Why not just call <c>IsRepeatableTargetOperand</c>.</b> That would ask the
        /// emitter whether the emitter's own decision was correct, and every decline would be
        /// exempt by definition — a guard that cannot fail for what it guards, which is the exact
        /// defect #1351 is filed under. A member read is therefore never exempt here: whether
        /// repeating it is safe depends on <c>MemberReadIsPlainField</c>, and that is precisely
        /// the answer <c>abc5bf4b0</c> got wrong.</para>
        /// </summary>
        private static bool IsSpliceRepeatable(Expression handle)
            => RoslynEmitter.IsRepeatableWithoutTypeInformation(handle);

        /// <summary>
        /// The re-SPLICE half (#1351). The hoist helper declined, so one generation is spliced
        /// into the read and the write of one augmented assignment — two evaluations. Correct only
        /// when the operand is observably repeatable; anything else is #1227's defect, and the
        /// generation counter above cannot see it because generation happened exactly once.
        /// </summary>
        public void OnSplice(Expression handle)
        {
            if (_spliceScopes.Count == 0 || IsSpliceRepeatable(handle))
                return;

            var scope = _spliceScopes.Peek();
            var count = scope.TryGetValue(handle, out var n) ? n + 1 : 1;
            scope[handle] = count;

            if (count == 1)
            {
                Splices.Add(
                    $"{handle.GetType().Name} at {handle.LineStart}:{handle.ColumnStart} "
                    + "spliced unhoisted into both the read and the write of one augmented "
                    + "assignment");
            }
        }

        public IDisposable BeginStatementScope()
        {
            // Reference identity, not value equality: AST nodes are records, so two structurally
            // identical sub-expressions (`f(x) + f(x)`) are equal by value and must count
            // separately. Only the SAME node reached twice is a re-entry.
            _scopes.Push(new Dictionary<Expression, int>(ReferenceEqualityComparer.Instance));
            _spliceScopes.Push(new Dictionary<Expression, int>(ReferenceEqualityComparer.Instance));
            return new Scope(_scopes, _spliceScopes);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Stack<Dictionary<Expression, int>> _scopes;
            private readonly Stack<Dictionary<Expression, int>> _spliceScopes;
            private bool _disposed;

            public Scope(
                Stack<Dictionary<Expression, int>> scopes,
                Stack<Dictionary<Expression, int>> spliceScopes)
            {
                _scopes = scopes;
                _spliceScopes = spliceScopes;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _scopes.Pop();
                _spliceScopes.Pop();
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<Expression>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public bool Equals(Expression? x, Expression? y) => ReferenceEquals(x, y);

            public int GetHashCode(Expression obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
