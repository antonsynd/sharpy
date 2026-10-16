using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class IteratorPropertyTests
{
    private readonly ITestOutputHelper _output;

    public IteratorPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// <c>GenIterators.ModuleWithGenerator(valid: true)</c>: every raw sample analyses clean (or is a pinned known red) — the sink counts what the filter discards. Before #2288 the lane passed with three quarters of its samples refused or throwing.
    /// </summary>
    [Fact]
    public void GeneratorFunction_CompilesWithYield()
    {
        var sink = new RejectionSink("Iterator.GeneratorFunction_CompilesWithYield");

        SemanticFilter.WellTypedProgram(GenIterators.ModuleWithGenerator(valid: true), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// <c>GenIterators.ModuleWithGeneratorValidator</c>: every raw sample analyses clean (or is a pinned known red) — the sink counts what the filter discards. Before #2288 the lane passed with half of its samples refused or throwing. It asserted only that analysis did not throw an internal error; a refusal counted as "analyzed".
    /// </summary>
    [Fact]
    public void GeneratorFunction_ValidatorChecks()
    {
        var sink = new RejectionSink("Iterator.GeneratorFunction_ValidatorChecks");

        SemanticFilter.WellTypedProgram(GenIterators.ModuleWithGeneratorValidator(), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// <c>GenIterators.ModuleWithYieldFrom</c>: every raw sample analyses clean (or is a pinned known red) — the sink counts what the filter discards. Before #2288 the lane passed with three quarters of its samples refused or throwing.
    /// </summary>
    [Fact]
    public void YieldFrom_CompilesClean()
    {
        var sink = new RejectionSink("Iterator.YieldFrom_CompilesClean");

        SemanticFilter.WellTypedProgram(GenIterators.ModuleWithYieldFrom(), sink)
            .Sample(_ => { }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
    }

    /// <summary>
    /// The two halves are judged separately, every sample (#2288): the complete class
    /// (<c>__next__</c> + <c>__iter__ -> Self</c>, iterated by <c>for v in c</c>) analyses clean,
    /// and the same class without <c>__iter__</c> draws exactly SPY0320 (not iterable). Before
    /// #2288 one accepted-or-refused sample of either half passed the property, and the complete
    /// half had never once analysed clean (an undeclared field, an unannotated <c>__iter__</c>).
    /// </summary>
    [Fact]
    public void IterProtocol_RequiresBothMethods()
    {
        var complete = ErrorInjector.JudgeSamples(GenIterators.ModuleWithIterProtocol(complete: true),
            source => ErrorInjector.JudgeClean(source, "complete iterator class"), iter: 25);
        _output.WriteLine($"Iter protocol (complete): {complete.Tested - complete.Failures}/{complete.Tested} analysed clean");

        var incomplete = ErrorInjector.JudgeSamples(GenIterators.ModuleWithIterProtocol(complete: false),
            source => ErrorInjector.JudgeSource(source, "SPY0320", CompilerDiagnosticSeverity.Error, null,
                "iterated class without __iter__"), iter: 25);
        _output.WriteLine($"Iter protocol (no __iter__): {incomplete.Tested - incomplete.Failures}/{incomplete.Tested} drew SPY0320");

        Assert.True(complete.Tested > 0, "No complete iterator programs were sampled");
        Assert.True(incomplete.Tested > 0, "No incomplete iterator programs were sampled");
        Assert.True(complete.First is null,
            $"{complete.Failures}/{complete.Tested} complete iterator programs refused; the first:\n{complete.First}");
        Assert.True(incomplete.First is null,
            $"{incomplete.Failures}/{incomplete.Tested} incomplete iterator programs misjudged; the first:\n{incomplete.First}");
    }
}
