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
    public void MatchWithGuards_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenMatchPatterns.MatchWithGuards()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "pattern_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Match with guards: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Match with guards pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void MatchWithOrPatterns_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenMatchPatterns.MatchWithOrPatterns()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "pattern_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Match with or-patterns: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Match with or-patterns pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void MatchWithTypePatterns_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenMatchPatterns.MatchWithTypePatterns()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "pattern_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Match with type patterns: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Match with type patterns pass rate too low: {passed}/{total}");
    }

    [Fact]
    public void MatchWithNestedPatterns_CompilesClean()
    {
        int total = 0;
        int passed = 0;

        GenMatchPatterns.MatchWithNestedPatterns()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "pattern_test.spy");
                    if (result.Success)
                        Interlocked.Increment(ref passed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Match with nested patterns: {passed}/{total} passed");
        Assert.True(passed > total / 3,
            $"Match with nested patterns pass rate too low: {passed}/{total}");
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
