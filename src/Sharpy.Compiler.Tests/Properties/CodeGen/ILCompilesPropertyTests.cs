using System.Collections.Concurrent;
using System.Collections.Immutable;
using CsCheck;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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
        var rejections = new ConcurrentBag<string>();

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
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

            if (errors.Count > 0)
            {
                var detail = string.Join("\n  ", errors.Take(5).Select(d => d.ToString()));
                throw new Xunit.Sdk.XunitException(
                    "Generated C# failed to emit to IL (#1035):\n" +
                    $"=== Sharpy source ===\n{source}\n" +
                    $"=== Generated C# ===\n{result.GeneratedCSharpCode}\n" +
                    $"=== CS diagnostics ===\n  {detail}");
            }

            Interlocked.Increment(ref emitted);
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(WithEntryPoint(m)), iter: SampleCount);

        _output.WriteLine($"IL emission: {emitted}/{total} emitted to valid IL "
            + $"({rejections.Count} rejected by the front end)");
        foreach (var reason in rejections.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(5))
            _output.WriteLine($"  rejected x{reason.Count()}: {reason.Key}");

        // The floor is the whole point of #1432: without it, "every sample was rejected" and
        // "every sample emitted clean IL" are the same green. It is deliberately far below the
        // measured rate rather than next to it — this is a random generator, and a floor that
        // tracks the observed value converts a real signal into a flake.
        total.Should().BeGreaterThanOrEqualTo(MinimumEmittedSamples,
            "a sample that never reaches compilation.Emit cannot witness the property this test "
            + "asserts; #1432 was exactly this suite passing on 0 emitted programs");
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
