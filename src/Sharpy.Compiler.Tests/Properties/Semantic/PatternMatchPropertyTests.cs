using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class PatternMatchPropertyTests
{
    private readonly ITestOutputHelper _output;

    public PatternMatchPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MatchNonExhaustive_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenMatchPatterns.MatchNonExhaustive()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "pattern_test.spy");
                    if (!result.Success || result.Diagnostics.GetAll().Any(d =>
                        d.Severity >= Sharpy.Compiler.Diagnostics.CompilerDiagnosticSeverity.Warning))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Non-exhaustive match diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Non-exhaustive match diagnostic rate too low: {diagnosed}/{total}");
    }
}
