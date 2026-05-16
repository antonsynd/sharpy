using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
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
        RunInjectionTest(ErrorInjector.InjectWrongArgumentType, "wrong argument type",
            ProgramWithLenCall());
    }

    [Fact]
    public void MissingArgument_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectMissingArgument, "missing argument",
            ProgramWithLenCall());
    }

    [Fact]
    public void WrongFunctionArgCount_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectWrongFunctionArgCount,
            "wrong function arg count", ProgramWithFunctionCall());
    }

    [Fact]
    public void WrongFunctionArgType_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectWrongFunctionArgType,
            "wrong function arg type", ProgramWithFunctionCall());
    }

    [Fact]
    public void UndefinedFunctionCall_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectUndefinedFunctionCall,
            "undefined function call", ProgramWithFunctionCall());
    }

    [Fact]
    public void OverrideSignatureMismatch_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectOverrideSignatureMismatch,
            "override signature mismatch", ProgramWithClassHierarchy());
    }

    [Fact]
    public void WrongConstructorArgs_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectWrongConstructorArgs,
            "wrong constructor args", ProgramWithClassHierarchy());
    }

    private static Gen<Module> ProgramWithClassHierarchy() =>
        GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 1);

    private static Gen<Module> ProgramWithLenCall() =>
        GenTyped.ExpressionOfType(TypeEnv.Default, "int", fuel: 1).Select(inner =>
        {
            var lenCall = new FunctionCall
            {
                Function = new Identifier { Name = "len" },
                Arguments = ImmutableArray.Create<Expression>(
                    new ListLiteral
                    {
                        Elements = ImmutableArray.Create<Expression>(inner)
                    })
            };

            var body = ImmutableArray.CreateBuilder<Statement>();
            foreach (var binding in TypeEnv.Default.Bindings)
            {
                body.Add(new VariableDeclaration
                {
                    Name = binding.Key,
                    Type = new TypeAnnotation { Name = binding.Value },
                    InitialValue = GenTyped.DefaultValue(binding.Value)
                });
            }
            body.Add(new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(lenCall)
                }
            });

            return new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    new FunctionDef
                    {
                        Name = "main",
                        Body = body.ToImmutable()
                    })
            };
        });

    private static Gen<Module> ProgramWithFunctionCall() =>
        GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 1);

    private void RunInjectionTest(
        Func<Module, ErrorInjector.InjectionResult?> injector,
        string label,
        Gen<Module>? overrideGen = null)
    {
        int tested = 0;
        int caught = 0;
        var falseNegatives = new List<string>();

        var baseGen = overrideGen ?? Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
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
