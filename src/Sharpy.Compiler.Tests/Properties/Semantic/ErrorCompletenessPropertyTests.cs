using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class ErrorCompletenessPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ErrorCompletenessPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TypeMismatch_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectTypeMismatchAssignment, "type mismatch");
    }

    [Fact]
    public void UndefinedVariable_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectUndefinedVariable, "undefined variable");
    }

    [Fact]
    public void WrongArgumentType_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectWrongArgumentType, "wrong argument type");
    }

    [Fact]
    public void MissingArgument_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectMissingArgument, "missing argument");
    }

    private void RunInjectionTest(
        Func<Sharpy.Compiler.Parser.Ast.Module, ErrorInjector.InjectionResult?> injector,
        string label)
    {
        int tested = 0;
        int caught = 0;
        var falseNegatives = new List<string>();

        var baseGen = Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2));

        var wellTyped = SemanticFilter.WellTypedProgram(baseGen);

        wellTyped.Sample(module =>
        {
            var result = injector(module);
            if (result == null)
                return;

            Interlocked.Increment(ref tested);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(result.Mutated);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var compResult = compiler.Analyze(source, "error_test.spy");

                if (!compResult.Success)
                {
                    Interlocked.Increment(ref caught);
                }
                else
                {
                    lock (falseNegatives)
                        falseNegatives.Add(
                            $"Injected {label} was not caught:\n{source}");
                }
            }
            catch
            {
                Interlocked.Increment(ref caught);
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Error completeness ({label}): {caught}/{tested} caught");
        Assert.True(tested > 0, $"No {label} injections were applicable");
        Assert.Empty(falseNegatives);
    }
}
