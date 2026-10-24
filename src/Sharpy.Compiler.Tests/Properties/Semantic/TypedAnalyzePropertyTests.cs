using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class TypedAnalyzePropertyTests
{
    private readonly ITestOutputHelper _output;

    public TypedAnalyzePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// <c>GenTyped.TypedProgram</c> over three bindings: every raw sample analyses clean (or is a
    /// pinned known red in <c>KnownGeneratorPins</c>). Before #2288 the lane passed with half
    /// its samples refused, and a throw was swallowed.
    /// </summary>
    [Fact]
    public void TypedProgram_AnalyzesClean()
    {
        var sink = new RejectionSink("TypedAnalyze.TypedProgram_AnalyzesClean");

        SemanticFilter.WellTypedProgram(
                Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                {
                    var env = new TypeEnv()
                        .WithBinding("x", "int")
                        .WithBinding("s", "str")
                        .WithBinding("flag", "bool");
                    return GenTyped.TypedProgram(env, type, fuel: 2);
                }), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    [Fact]
    public void FilteredTypedProgram_AnalyzesClean()
    {
        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
        {
            var env = new TypeEnv()
                .WithBinding("x", "int")
                .WithBinding("s", "str")
                .WithBinding("flag", "bool");
            return GenTyped.TypedProgram(env, type, fuel: 2);
        });

        var sink = new RejectionSink("TypedAnalyze.FilteredTypedProgram_AnalyzesClean");
        var filtered = SemanticFilter.WellTypedProgram(baseGen, sink);

        int count = 0;
        filtered.Sample(module =>
        {
            Interlocked.Increment(ref count);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            // The filter admitted it: a second analysis must agree (a refusal throws with its codes).
            PropertyAnalysis.AnalyseClean(source, "a filtered typed program's re-analysis");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        _output.WriteLine($"Filtered typed analysis: {count} programs all analyzed clean");
    }

    /// <summary>Every RAW typed program analyses without a throw or an internal compiler error
    /// (<see cref="ErrorInjector.JudgeNoInternalError"/>); a refusal is allowed — the sample is
    /// unfiltered. Before #2288 the lane swallowed every exception but one <c>Analyze</c> never throws.
    /// It samples the SAME configurations the acceptance lanes do — <see cref="TypeEnv.Default"/>,
    /// <see cref="TypeEnv.WithOptionals"/>, <see cref="TypeEnv.WithCollections"/> and
    /// <see cref="TypeEnv.WithGenerics"/>, each at fuel 2 with statements — beside the bare
    /// <c>new TypeEnv()</c> fuel-3 expression config it always had: before #2324 it sampled only the
    /// bare config, so a crash on a statement or an optional/collection binding was out of its reach.
    /// Each config is judged and reported on its own, so one config's floor cannot hide behind another's.</summary>
    [Theory]
    [InlineData("bare-fuel3")]
    [InlineData("Default")]
    [InlineData("WithOptionals")]
    [InlineData("WithCollections")]
    [InlineData("WithGenerics")]
    public void TypedProgram_NeverThrowsInternalError(string config)
    {
        var verdicts = ErrorInjector.JudgeSamples(NeverThrowsConfig(config),
            source => ErrorInjector.JudgeNoInternalError(source, $"typed program ({config})"), iter: 100);
        _output.WriteLine($"Typed programs ({config}): {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed without an internal error");
        Assert.True(verdicts.Tested > 0, $"No typed programs were sampled ({config})");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} typed programs ({config}) crashed the compiler; the first:\n{verdicts.First}");
    }

    /// <summary>The configurations <see cref="TypedProgram_NeverThrowsInternalError"/> samples; the
    /// result types are the ones the acceptance lanes over the same env draw.</summary>
    private static Gen<Sharpy.Compiler.Parser.Ast.Module> NeverThrowsConfig(string config) => config switch
    {
        "bare-fuel3" => Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(new TypeEnv(), type, fuel: 3)),
        "Default" => Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true)),
        "WithOptionals" => Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2, withStatements: true)),
        "WithCollections" => Gen.OneOfConst("int", "str", "list[int]").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2, withStatements: true)),
        "WithGenerics" => Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithGenerics, type, fuel: 2, withStatements: true)),
        _ => throw new ArgumentOutOfRangeException(nameof(config), config, "unknown NeverThrows config"),
    };
}
