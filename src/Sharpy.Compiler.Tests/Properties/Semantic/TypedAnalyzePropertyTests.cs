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
    /// pinned known red — the #2169 nested-conditional cell). Before #2288 the lane passed with half
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
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "typed_test.spy");

            if (!result.Success)
                throw new Exception(
                    $"Filtered program should analyze clean but got errors:\n{source}");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        _output.WriteLine($"Filtered typed analysis: {count} programs all analyzed clean");
    }

    /// <summary>Every RAW typed program analyses without a throw or an internal compiler error
    /// (<see cref="ErrorInjector.JudgeNoInternalError"/>); a refusal is allowed — the sample is
    /// unfiltered. Before #2288 the lane swallowed every exception but one <c>Analyze</c> never throws.</summary>
    [Fact]
    public void TypedProgram_NeverThrowsInternalError()
    {
        var verdicts = ErrorInjector.JudgeSamples(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type => GenTyped.TypedProgram(new TypeEnv(), type, fuel: 3)),
            source => ErrorInjector.JudgeNoInternalError(source, "typed program"), iter: 100);
        _output.WriteLine($"Typed programs: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed without an internal error");
        Assert.True(verdicts.Tested > 0, "No typed programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} typed programs crashed the compiler; the first:\n{verdicts.First}");
    }
}
