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
/// <para><b>What the red-verification established — including a limit.</b> Reverting the
/// <c>GenerateBinaryOp</c> pipe-forward fix this sweep motivated turns it red on
/// <c>functions/pipe_basic</c> and <c>functions/pipe_with_partial</c>, naming the duplicated
/// nodes: the tripwire is connected to real double generation.
///
/// <para>But inverting <c>MemberReadIsPlainField</c> — reintroducing <c>abc5bf4b0</c>, the bug
/// this guard was specified against — leaves it GREEN, and that is not a corpus gap:
/// <c>HoistAugmentedTargetOperand</c> takes ALREADY-GENERATED syntax and the caller splices the
/// same <c>ExpressionSyntax</c> into the read and the write. <c>GenerateExpression</c> runs once;
/// the duplication is at the splice. A node-entry counter at the choke point cannot see that by
/// construction. So: this guards <b>re-generation</b>, and <c>single_evaluation_*.spy</c> plus
/// <c>AugmentedAssignmentSingleEvaluationTests</c> still guard <b>re-splicing</b> — two
/// mechanisms, one defect class, and neither subsumes the other. Filed as #1351.</para>
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
            });

        stopwatch.Stop();
        _output.WriteLine(
            $"Swept {fixtures.Count} fixtures in {stopwatch.Elapsed.TotalSeconds:F1}s "
            + $"({uncompilable.Count} did not compile through this seam, {offenders.Count} re-entries).");
        foreach (var offender in offenders.OrderBy(o => o, StringComparer.Ordinal))
            _output.WriteLine($"  {offender}");

        offenders.Should().BeEmpty(
            "an expression generated more than once in a statement emits its side effects more "
            + "than once — correct value, wrong effect count (#1334)");

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
    /// <para>Lower this whenever the real number drops; never raise it without saying why in the
    /// same commit. A rise means either a regression or a deliberate scope change, and both are
    /// things a reader must be told rather than left to infer from a silently larger skip list.</para>
    /// </summary>
    private const int MaxUncompilableFixtures = 7;

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

        public List<string> Reentries { get; } = new();

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
        /// </summary>
        private static bool IsObservablyRepeatable(Expression expression)
            => expression is AstIdentifier or SuperExpression or NoneLiteral
                or BooleanLiteral or IntegerLiteral or FloatLiteral or StringLiteral;

        public IDisposable BeginStatementScope()
        {
            // Reference identity, not value equality: AST nodes are records, so two structurally
            // identical sub-expressions (`f(x) + f(x)`) are equal by value and must count
            // separately. Only the SAME node reached twice is a re-entry.
            _scopes.Push(new Dictionary<Expression, int>(ReferenceEqualityComparer.Instance));
            return new Scope(_scopes);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Stack<Dictionary<Expression, int>> _scopes;
            private bool _disposed;

            public Scope(Stack<Dictionary<Expression, int>> scopes) => _scopes = scopes;

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _scopes.Pop();
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
