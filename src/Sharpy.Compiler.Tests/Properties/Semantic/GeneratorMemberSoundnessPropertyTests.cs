using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The generated lane for typed-generator members no other property samples (#2288, P63 Phase 3
/// Task 3): each fact reads its member RAW through <see cref="SemanticFilter"/> and asserts its
/// <see cref="RejectionSink"/> holds no unexplained rejection — every sample analyses clean (or is a
/// pinned known red). These are the facts behind the roster's Y-generated rows for match patterns,
/// nested <c>try</c> and list indexing; before them the members had no caller, so their programs were
/// never once compiled. Two more read the list-literal surface generator defect G1 lived in
/// (<c>ListOfType</c>, and <c>TypedProgram</c> with a list result). A source-text member is analysed
/// as drawn; an expression fragment is wrapped in a <c>main</c> that declares the environment it reads.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class GeneratorMemberSoundnessPropertyTests
{
    private const int Iterations = 20;

    /// <summary>The list-literal facts draw more: with G1 reverted they went red on 1 sample in ~20
    /// (SPY0227), too close to zero for 20 iterations to hold a margin.</summary>
    private const int ListIterations = 60;

    private readonly ITestOutputHelper _output;

    public GeneratorMemberSoundnessPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MatchWithGuards_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithGuards", GenMatchPatterns.MatchWithGuards());

    [Fact]
    public void MatchWithOrPatterns_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithOrPatterns", GenMatchPatterns.MatchWithOrPatterns());

    [Fact]
    public void MatchWithListPatterns_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithListPatterns", GenMatchPatterns.MatchWithListPatterns());

    [Fact]
    public void MatchWithAndPatterns_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithAndPatterns", GenMatchPatterns.MatchWithAndPatterns());

    [Fact]
    public void MatchWithTypePatterns_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithTypePatterns", GenMatchPatterns.MatchWithTypePatterns());

    [Fact]
    public void MatchWithNestedPatterns_AnalyzesClean() =>
        AssertSourcesClean("GenMatchPatterns.MatchWithNestedPatterns", GenMatchPatterns.MatchWithNestedPatterns());

    [Fact]
    public void ModuleWithNestedTry_AnalyzesClean() =>
        AssertModulesClean("GenExceptions.ModuleWithNestedTry", GenExceptions.ModuleWithNestedTry());

    /// <summary><c>GenTyped.ListSubscript</c> is an expression fragment over a <c>list[T]</c>
    /// variable: printed from a <c>main</c> that declares <see cref="TypeEnv.WithCollections"/>.</summary>
    [Fact]
    public void ListSubscript_AnalyzesClean() =>
        AssertModulesClean("GenTyped.ListSubscript",
            Gen.OneOfConst("int", "str").SelectMany(element =>
                InMain(TypeEnv.WithCollections, GenTyped.ListSubscript(TypeEnv.WithCollections, element))));

    /// <summary><c>GenTyped.ListOfType</c> is a list-literal fragment: printed from a <c>main</c>
    /// that declares <see cref="TypeEnv.WithCollections"/> — no annotated slot types it, so an empty
    /// literal would be SPY0227 (#2288 G1: 47/200 raw samples before the generator was repaired).</summary>
    [Fact]
    public void ListOfType_AnalyzesClean() =>
        AssertModulesClean("GenTyped.ListOfType",
            Gen.OneOfConst("int", "str").SelectMany(element =>
                InMain(TypeEnv.WithCollections, GenTyped.ListOfType(TypeEnv.WithCollections, element, fuel: 1))),
            ListIterations);

    /// <summary><c>GenTyped.TypedProgram</c> printing a LIST result — the shape that drew a slot-less
    /// <c>print([])</c> and <c>if []:</c> (SPY0227, #2288 G1); the other soundness lanes draw a list
    /// result for at most a third of their samples.</summary>
    [Fact]
    public void TypedProgram_ListResult_AnalyzesClean() =>
        AssertModulesClean("GenTyped.TypedProgram(list result)",
            Gen.OneOfConst("list[int]", "list[str]").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2, withStatements: true)),
            ListIterations);

    private void AssertSourcesClean(string member, Gen<string> raw)
    {
        var sink = new RejectionSink($"GeneratorMemberSoundness.{member}");
        int admitted = 0;
        SemanticFilter.WellTypedSource(raw, sink).Sample(_ => Interlocked.Increment(ref admitted), iter: Iterations);
        Report(sink, admitted);
    }

    private void AssertModulesClean(string member, Gen<Module> raw, int iterations = Iterations)
    {
        var sink = new RejectionSink($"GeneratorMemberSoundness.{member}");
        int admitted = 0;
        SemanticFilter.WellTypedProgram(raw, sink).Sample(_ => Interlocked.Increment(ref admitted),
            print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: iterations);
        Report(sink, admitted);
    }

    private void Report(RejectionSink sink, int admitted)
    {
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Assert.True(admitted > 0, $"{sink.Label}: no sample was admitted — the lane read nothing");
    }

    /// <summary>A <c>main</c> declaring every binding of <paramref name="env"/> at its default value,
    /// then <c>print(fragment)</c>.</summary>
    private static Gen<Module> InMain(TypeEnv env, Gen<Expression> fragment) =>
        fragment.Select(expr =>
        {
            var body = ImmutableArray.CreateBuilder<Statement>();
            foreach (var (name, type) in env.Bindings)
            {
                body.Add(new VariableDeclaration
                {
                    Name = name,
                    Type = Annotation(type),
                    InitialValue = GenTyped.DefaultValue(type),
                });
            }
            body.Add(new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create(expr),
                },
            });
            return new Module
            {
                Body = ImmutableArray.Create<Statement>(new FunctionDef { Name = "main", Body = body.ToImmutable() }),
            };
        });

    private static TypeAnnotation Annotation(string type) => type switch
    {
        "list[int]" or "list[str]" => new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = type[5..^1] }),
        },
        _ => new TypeAnnotation { Name = type },
    };
}
