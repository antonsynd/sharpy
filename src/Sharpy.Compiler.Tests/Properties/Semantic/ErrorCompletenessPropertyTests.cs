using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
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

    [Fact]
    public void MissingAbstractImplementation_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectMissingAbstractImplementation,
            "missing abstract implementation", ProgramWithClassHierarchy());
    }

    /// <summary>A user class that implements nothing passed where <c>T: Ordered[T]</c> is inferred
    /// — SPY0237 (#2288 Phase 4).</summary>
    [Fact]
    public void ConstraintViolationAtInferredCall_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectConstraintViolationAtInferredCall,
            "constraint violation at an inferred call", ProgramWithConstraints());
    }

    /// <summary>The same violation at an explicit instantiation <c>SortedBox[Plain](…)</c>: Sharpy
    /// does not refuse it (Roslyn does, SPY0908 CS0311) — a KnownRed pin on #2212 that fails the
    /// first time the program is refused.</summary>
    [Fact]
    public void ConstraintViolationAtExplicitInstantiation_IsPinned()
    {
        RunInjectionTest(ErrorInjector.InjectConstraintViolationAtExplicitInstantiation,
            "constraint violation at an explicit instantiation", ProgramWithConstraints());
    }

    private static Gen<Module> ProgramWithConstraints() =>
        GenGenerics.GenericConstraintProgram(TypeEnv.WithGenerics, fuel: 1);

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

    /// <summary>
    /// Every applicable sample must draw exactly the injection's code (<see cref="ErrorInjector.Judge"/>):
    /// before #2288 the property counted ANY failed analysis — and any throw — as a catch, so an
    /// unrelated refusal or an internal error passed for the refusal under test.
    /// </summary>
    private void RunInjectionTest(
        Func<Module, ErrorInjector.InjectionResult?> injector,
        string label,
        Gen<Module>? overrideGen = null)
    {
        int tested = 0;
        var failures = new List<string>();
        ErrorInjector.KnownRedRefusal? pin = null;

        var baseGen = overrideGen ?? Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2));

        var wellTyped = SemanticFilter.WellTypedProgram(baseGen);

        wellTyped.Sample(module =>
        {
            var result = injector(module);
            if (result == null)
                return;

            Interlocked.Increment(ref tested);
            pin = result.KnownRed;
            if (ErrorInjector.Judge(result, label) is { } failure)
            {
                lock (failures)
                    failures.Add(failure);
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(pin is null
            ? $"Error completeness ({label}): {tested - failures.Count}/{tested} drew the expected code"
            : $"Error completeness ({label}): KnownRed {pin.Issue} — {tested - failures.Count}/{tested} still unrefused ({pin.Observed})");
        Assert.True(tested > 0, $"No {label} injections were applicable");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} {label} injections misjudged; the first:\n{failures.FirstOrDefault()}");
    }
}
