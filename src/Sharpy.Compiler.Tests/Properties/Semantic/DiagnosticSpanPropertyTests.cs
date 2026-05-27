using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class DiagnosticSpanPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticSpanPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllDiagnosticSpans_AreWithinSourceBounds()
    {
        var errors = new List<string>();

        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "span_test.spy");

                var sourceLineCount = source.Split('\n').Length;

                foreach (var diag in result.Diagnostics.GetAll())
                {
                    if (diag.Span.HasValue)
                    {
                        var span = diag.Span.Value;
                        if (span.Start < 0 || span.End > source.Length || span.Start > span.End)
                        {
                            lock (errors)
                                errors.Add(
                                    $"Span out of bounds: {span} for source length {source.Length}, " +
                                    $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                        }
                    }

                    if (diag.Line.HasValue)
                    {
                        if (diag.Line.Value < 1)
                        {
                            lock (errors)
                                errors.Add(
                                    $"Line {diag.Line.Value} is less than 1 (1-based), " +
                                    $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                        }
                        else if (diag.Line.Value > sourceLineCount)
                        {
                            lock (errors)
                                errors.Add(
                                    $"Line {diag.Line.Value} exceeds source line count {sourceLineCount}, " +
                                    $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                        }
                    }

                    if (diag.Column.HasValue && diag.Column.Value < 0)
                    {
                        lock (errors)
                            errors.Add(
                                $"Column {diag.Column.Value} is negative (0-based), " +
                                $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                    }
                }
            }
            catch
            {
                // Swallow internal errors — focus on span validity, not crashes
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);

        _output.WriteLine($"Diagnostic span bounds: {errors.Count} violations found");
        Assert.Empty(errors);
    }

    [Fact]
    public void ErrorDiagnosticSpans_AreNonEmpty()
    {
        var errors = new List<string>();

        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "span_test.spy");

                foreach (var diag in result.Diagnostics.GetAll())
                {
                    if (diag.Severity == CompilerDiagnosticSeverity.Error && diag.Span.HasValue)
                    {
                        var span = diag.Span.Value;
                        if (span.Length <= 0)
                        {
                            lock (errors)
                                errors.Add(
                                    $"Error diagnostic has empty span: {span}, " +
                                    $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                        }
                    }
                }
            }
            catch
            {
                // Swallow internal errors — focus on span validity, not crashes
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 100);

        _output.WriteLine($"Error diagnostic non-empty spans: {errors.Count} violations found");
        Assert.Empty(errors);
    }

    [Fact]
    public void TypedPrograms_DiagnosticSpans_AreWithinSourceBounds()
    {
        var errors = new List<string>();
        int injected = 0;

        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2));

        var wellTyped = SemanticFilter.WellTypedProgram(baseGen);

        var injectors = new Func<Module, ErrorInjector.InjectionResult?>[]
        {
            ErrorInjector.InjectTypeMismatchAssignment,
            ErrorInjector.InjectUndefinedVariable,
        };

        wellTyped.Sample(module =>
        {
            foreach (var injector in injectors)
            {
                var injection = injector(module);
                if (injection == null)
                    continue;

                Interlocked.Increment(ref injected);
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(injection.Mutated);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "span_test.spy");

                    var sourceLineCount = source.Split('\n').Length;

                    foreach (var diag in result.Diagnostics.GetAll())
                    {
                        if (diag.Span.HasValue)
                        {
                            var span = diag.Span.Value;
                            if (span.Start < 0 || span.End > source.Length || span.Start > span.End)
                            {
                                lock (errors)
                                    errors.Add(
                                        $"Span out of bounds: {span} for source length {source.Length}, " +
                                        $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                            }
                        }

                        if (diag.Line.HasValue)
                        {
                            if (diag.Line.Value < 1)
                            {
                                lock (errors)
                                    errors.Add(
                                        $"Line {diag.Line.Value} is less than 1 (1-based), " +
                                        $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                            }
                            else if (diag.Line.Value > sourceLineCount)
                            {
                                lock (errors)
                                    errors.Add(
                                        $"Line {diag.Line.Value} exceeds source line count {sourceLineCount}, " +
                                        $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                            }
                        }

                        if (diag.Column.HasValue && diag.Column.Value < 0)
                        {
                            lock (errors)
                                errors.Add(
                                    $"Column {diag.Column.Value} is negative (0-based), " +
                                    $"diagnostic {diag.Code}: {diag.Message}\nSource:\n{source}");
                        }
                    }
                }
                catch
                {
                    // Swallow internal errors — focus on span validity, not crashes
                }
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 30);

        _output.WriteLine($"Typed program span bounds: {errors.Count} violations found, {injected} injections tested");
        Assert.True(injected > 0, "No error injections were applicable");
        Assert.Empty(errors);
    }
}