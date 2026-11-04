using System.Collections.Concurrent;
using System.Collections.Immutable;
using CsCheck;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class ILCompilesPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ILCompilesPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// For every generated program that compiles through the front end, emit the
    /// generated C# all the way to IL (<c>compilation.Emit</c>) and assert Roslyn
    /// reports no errors. This test previously only checked front-end
    /// <c>Compile().Success</c> and never invoked Roslyn Emit, so its name did not
    /// match its behaviour; it now actually validates IL emission.
    ///
    /// <para><b>It then spent its whole life emitting nothing (#1432).</b> Measured at
    /// <c>b66c848af</c>: <c>IL emission: 0/0</c>. Not one of the 50 samples reached
    /// <c>compilation.Emit</c>, because a single-file compile is an ENTRY-POINT compile
    /// (<c>SyntheticProject.BuildConfig</c> sets <c>EntryPoint</c> to the file, so
    /// <c>IsEntryPointFileForTypeCheck</c> is true) and <c>ModuleLevelValidator</c> then requires a
    /// <c>main()</c> — while <c>GenModule</c> draws function names from a ten-name pool that does
    /// not contain "main". Every sample was rejected by the front end and skipped by the guard
    /// below, so the assertion this test exists for never executed. <see cref="WithEntryPoint"/>
    /// closes that, and the sample floor keeps it closed: an empty corpus must fail, not pass.</para>
    /// </summary>
    [Fact]
    public void GeneratedCSharp_CompilesToValidIL()
    {
        var references = IntegrationTestBase.GetSharedReferences();
        int total = 0;
        int emitted = 0;
        int knownRed = 0;
        var rejections = new ConcurrentBag<string>();

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Where(module => !HasStructurallyRefusedShape(module)).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(WithEntryPoint(module));

            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "il_test.spy");

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
            {
                // Reported, never silently dropped: a front-end rejection rate that climbs back to
                // 100% is how this test went vacuous the first time, and the reason is what makes
                // the next occurrence diagnosable rather than invisible.
                var first = result.Diagnostics.GetErrors().FirstOrDefault();
                rejections.Add(first is null ? "(no error diagnostic)" : $"{first.Code}: {first.Message}");
                return;
            }

            Interlocked.Increment(ref total);

            var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
            var compilation = CSharpCompilation.Create(
                "ILCompilesProperty",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            var pinned = HasNoneLiteralReceiver(module);

            if (errors.Count > 0)
            {
                // The one known-red shape (R-KK): tolerated only when every Roslyn error is the pin's
                // measured code, so a second defect in the same draw still fails the run.
                if (pinned && errors.All(d => d.Id == NoneLiteralReceiver.RoslynCode))
                {
                    Interlocked.Increment(ref knownRed);
                    return;
                }

                var detail = string.Join("\n  ", errors.Take(5).Select(d => d.ToString()));
                throw new Xunit.Sdk.XunitException(
                    "Generated C# failed to emit to IL (#1035):\n" +
                    $"=== Sharpy source ===\n{source}\n" +
                    $"=== Generated C# ===\n{result.GeneratedCSharpCode}\n" +
                    $"=== CS diagnostics ===\n  {detail}");
            }

            if (pinned)
            {
                throw new Xunit.Sdk.XunitException(
                    $"The {NoneLiteralReceiver.Issue} pin is stale: a draw with a {NoneLiteralReceiver.Shape} "
                    + "compiled to valid IL. Delete NoneLiteralReceiver and its facts (drain on fix).\n"
                    + $"=== Sharpy source ===\n{source}");
            }

            Interlocked.Increment(ref emitted);
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(WithEntryPoint(m)), iter: SampleCount);

        _output.WriteLine($"IL emission: {emitted}/{total} emitted to valid IL "
            + $"({rejections.Count} rejected by the front end)");
        _output.WriteLine($"  {knownRed} skipped ({NoneLiteralReceiver.Issue}: {NoneLiteralReceiver.Shape} "
            + $"-> {NoneLiteralReceiver.RoslynCode})");
        foreach (var reason in rejections.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(5))
            _output.WriteLine($"  rejected x{reason.Count()}: {reason.Key}");

        // The floor is the whole point of #1432: without it, "every sample was rejected" and
        // "every sample emitted clean IL" are the same green. It is deliberately far below the
        // measured rate rather than next to it — this is a random generator, and a floor that
        // tracks the observed value converts a real signal into a flake.
        // Counted on `emitted`, not `total`: a known-red draw reaches compilation.Emit but witnesses
        // nothing, so it must not hold the floor up. On a run with no pinned draw the two are equal.
        emitted.Should().BeGreaterThanOrEqualTo(MinimumEmittedSamples,
            "a sample that never reaches compilation.Emit cannot witness the property this test "
            + "asserts; #1432 was exactly this suite passing on 0 emitted programs");
    }

    /// <summary>
    /// The one known-red draw shape this lane tolerates (R-KK, #2365, a band chore on P76's gate): a
    /// member access whose receiver is the <c>None</c> literal (<c>raise None.m</c>, <c>x = None.qux</c>,
    /// <c>return None.res</c>). The front end accepts it and the emitter writes <c>null.M</c>, which Roslyn
    /// refuses with CS0023 — the <c>None</c>-literal row of #2365's receiver-kind axis, cured by P17's
    /// not-found arm (a semantic-time SPY0203). Nightly seeds <c>5F-Ai3FcPnEx</c>, <c>8sLgyTw-IOM3</c> and
    /// <c>c4U0RdI-d-Uw</c> drew it.
    ///
    /// <para>Keyed on the SHAPE (R-JE's precedent, 8d6883cd2), never on the code alone: a pinned draw is
    /// skipped only when every Roslyn error is CS0023, and reported "n skipped (#2365)". It drains loudly
    /// two ways: a pinned draw that compiles to valid IL fails the sampled test, and
    /// <see cref="NoneLiteralReceiver_IsStillKnownRed"/> fails once the front end refuses the shape.</para>
    /// </summary>
    private static readonly KnownRedDrawShape NoneLiteralReceiver =
        new KnownRedDrawShape("#2365", "None-literal member-access receiver", "CS0023");

    private sealed record KnownRedDrawShape(string Issue, string Shape, string RoslynCode);

    /// <summary>True when any member access in <paramref name="node"/> has the <c>None</c> literal
    /// (through parentheses) as its receiver.</summary>
    private static bool HasNoneLiteralReceiver(Node node) =>
        (node is MemberAccess access && AstHelper.UnwrapParenthesized(access.Object) is NoneLiteral)
        || node.GetChildNodes().Any(HasNoneLiteralReceiver);

    /// <summary>
    /// The pin's drain signal, deterministic: the reproducer cells of #2365's <c>None</c>-literal row (the
    /// first is nightly #7's shrunk seed <c>c4U0RdI-d-Uw</c>) still pass the front end and still fail Roslyn
    /// with CS0023 only. When P17 lands, the front end refuses them (SPY0203) and this goes red: delete
    /// <see cref="NoneLiteralReceiver"/>, this fact and the skip.
    /// </summary>
    [Theory]
    [InlineData("def run_task(x: int, qux: int) -> int:\n    raise None.m\n\ndef main():\n    pass\n")]
    [InlineData("def main():\n    x = None.qux\n")]
    [InlineData("def main():\n    print(None.qux)\n")]
    [InlineData("def f() -> int:\n    return None.res\n\ndef main():\n    pass\n")]
    public void NoneLiteralReceiver_IsStillKnownRed(string source)
    {
        var module = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll()).ParseModule();
        HasNoneLiteralReceiver(module).Should().BeTrue("the reproducer must carry the pinned shape");

        var result = new Sharpy.Compiler.Compiler().Compile(source, "il_test.spy");
        result.Success.Should().BeTrue(
            $"{NoneLiteralReceiver.Issue}'s None-literal row has changed at the front end; if it now refuses "
            + "the shape (SPY0203, P17), the pin has drained — delete NoneLiteralReceiver, this fact and the skip. "
            + "First error: " + result.Diagnostics.GetErrors().FirstOrDefault()?.ToString());

        var compilation = CSharpCompilation.Create(
            "ILCompilesPin",
            new[] { CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode) },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        using var ms = new MemoryStream();
        var codes = compilation.Emit(ms).Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id)
            .Distinct()
            .ToList();
        codes.Should().Equal(new[] { NoneLiteralReceiver.RoslynCode },
            "the pin tolerates exactly the measured Roslyn code; any other outcome means the row moved");
    }

    /// <summary>
    /// Positive and negative controls for the pin's shape predicate: it reports the <c>None</c> literal as
    /// a receiver (bare or parenthesized) and nothing else — a member access on a name, on a call taking
    /// <c>None</c>, or a bare <c>None</c> is a second shape the skip must never cover.
    /// </summary>
    [Theory]
    [InlineData("x = None.qux\n", true)]
    [InlineData("x = (None).qux\n", true)]
    [InlineData("x = y.qux\n", false)]
    [InlineData("x = f(None).qux\n", false)]
    [InlineData("x = None\n", false)]
    public void NoneLiteralReceiver_ShapePredicate_CoversOnlyThatShape(string source, bool expected)
    {
        var module = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll()).ParseModule();
        HasNoneLiteralReceiver(module).Should().Be(expected);
    }

    /// <summary>
    /// Minimum number of generated programs that must survive the front end and be emitted to IL.
    /// Set well below the observed rate so ordinary generator variance cannot make this flake,
    /// while any collapse toward the vacuous 0 fails loudly.
    /// </summary>
    private const int MinimumEmittedSamples = 5;

    /// <summary>
    /// How many programs to generate. Raised from 50 because the FLOOR IS A PROPERTY OF THE PAIR,
    /// not of the floor alone, and 50 was too few for this generator to witness anything reliably.
    ///
    /// <para>Measured over eight consecutive runs at 50: emitted counts 3, 0, 4, 5, 3, 6, 6, 2 —
    /// mean 3.6 against a floor of 5, so the assertion failed five times in eight, and one run
    /// reached the vacuous <c>0/0</c> that #1432 exists to catch. The generator is shared with the
    /// parser round-trip and metamorphic suites, where its ~90% semantic-rejection rate is
    /// irrelevant because those only need well-formed SYNTAX; here every rejected sample is a
    /// sample that witnesses nothing.</para>
    ///
    /// <para>Fixing that by lowering the floor would make the suite agree with whatever it happens
    /// to produce — the failure mode #1432 was filed about. Drawing more samples instead keeps the
    /// floor meaningful and buys proportionally more bug-finding: at 50 samples this suite surfaced
    /// #1477 and #1485 within five runs.</para>
    /// </summary>
    private const int SampleCount = 250;

    /// <summary>
    /// Makes a generated module a legal Sharpy entry point without changing what it computes:
    /// annotates parameters that have no type, and appends an empty <c>main()</c> if absent.
    ///
    /// <para>Both are mechanical consequences of the language, not of this test. A single-file
    /// compile is an ENTRY-POINT compile, so a module without <c>main</c> never reaches codegen;
    /// and Sharpy requires parameter annotations (SPY0226), which <c>GenModule</c> never emits
    /// because it was built for parser/unparser ROUND-TRIPPING, where untyped parameters are
    /// perfectly good syntax. Feeding a syntax generator to a codegen property test is what made
    /// this suite report <c>IL emission: 0/0</c> and pass (#1432).</para>
    ///
    /// <para>Applied here rather than in <c>GenModule</c> on purpose: that generator is shared with
    /// the parser round-trip, unparser and metamorphic suites, where injecting a <c>main</c> or
    /// annotations would change what those tests exercise.</para>
    /// </summary>
    private static Sharpy.Compiler.Parser.Ast.Module WithEntryPoint(Sharpy.Compiler.Parser.Ast.Module module)
    {
        var body = module.Body.Select(AnnotateParameters).ToImmutableArray();

        if (!body.Any(s => s is FunctionDef { Name: "main" }))
        {
            body = body.Add(new FunctionDef
            {
                Name = "main",
                Body = ImmutableArray.Create<Statement>(new PassStatement()),
            });
        }

        return module with { Body = body };
    }

    /// <summary>
    /// True when <paramref name="node"/> holds a shape the front end refuses whatever surrounds it,
    /// so the sample can never reach <c>compilation.Emit</c>: an import or a property declaration
    /// inside a function (SPY0708, since P21e's declaration-position rule) and a bare <c>super()</c>
    /// that is not the receiver of a member access (SPY0287).
    ///
    /// <para><b>The yield halved without the floor moving (#2387).</b> The floor/sample pair was sized
    /// against 3.6 emitted per 50 (≈7.2%). Measured over 10 runs @ 21dfc83bb: 5, 10, 8, 14, 6, 11, 13,
    /// 7, 13, 8 per 250 (mean 9.5, ≈3.8%), so P(&lt; 5) ≈ 4% per run and CI went red on unrelated
    /// PRs. The top front-end rejections were exactly these three shapes (x46 bare <c>super()</c>, x45
    /// function-scoped import, x20 function-scoped property): refusals that became legal-language
    /// verdicts after the generator was written, which this consumer cannot use. Skipping them at the
    /// draw restores the yield the floor was sized for; lowering the floor would make the suite agree
    /// with whatever it produces (#1432). Filtered here, not in <c>GenModule</c>, for the reason
    /// <see cref="WithEntryPoint"/> gives: the generator's other consumers only need syntax.</para>
    /// <para>Walked through <see cref="Node.GetChildNodes"/>, not the typed generators' <c>AstRewriter</c>:
    /// naming that namespace puts a file in <c>PropertyOracleConformanceTests</c>' universe, and this lane
    /// is not a typed-generator oracle. The parser's walk hides only type annotations, which hold none of
    /// the three shapes.</para>
    /// </summary>
    private static bool HasStructurallyRefusedShape(Node node, Node? parent = null, bool inFunction = false) => node switch
    {
        ImportStatement or FromImportStatement or PropertyDef when inFunction => true,
        SuperExpression when parent is not MemberAccess => true,
        _ => node.GetChildNodes().Any(child => HasStructurallyRefusedShape(child, node, inFunction || node is FunctionDef)),
    };

    /// <summary>Annotates untyped parameters, recursing into class bodies for methods.</summary>
    private static Statement AnnotateParameters(Statement statement) => statement switch
    {
        FunctionDef function => function with
        {
            Parameters = function.Parameters.Select(AnnotateParameter).ToImmutableArray(),
        },
        ClassDef classDef => classDef with
        {
            Body = classDef.Body.Select(AnnotateParameters).ToImmutableArray(),
        },
        _ => statement,
    };

    // `int` is arbitrary but harmless: a body that uses the parameter incompatibly is still
    // rejected, which is a legitimate rejection rather than the structural one #1432 was about.
    // `self`/`cls` are left alone — annotating a receiver would be wrong, not merely arbitrary.
    private static Parameter AnnotateParameter(Parameter parameter)
        => parameter.Type is not null || parameter.Name is "self" or "cls"
            ? parameter
            : parameter with { Type = new TypeAnnotation { Name = "int" } };
}
