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

    /// <summary>An un-narrowed <c>T?</c> stored to a <c>T</c> slot — SPY0220 (#2288 Phase 4).</summary>
    [Fact]
    public void OptionalStoreWithoutNarrowing_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectOptionalStoreWithoutNarrowing,
            "Optional stored to its payload type", ProgramWithOptionals());
    }

    /// <summary>An un-narrowed <c>T?</c> as an arithmetic operand — SPY0222 (#2288 Phase 4).</summary>
    [Fact]
    public void OptionalOperandWithoutNarrowing_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectOptionalOperandWithoutNarrowing,
            "Optional arithmetic operand", ProgramWithOptionals());
    }

    /// <summary><c>len(c)</c> on a class whose <c>__len__</c> was renamed away — SPY0320 (#2288 Phase 4).</summary>
    [Fact]
    public void ProtocolDunderRemoved_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectProtocolDunderRemoved,
            "protocol dunder removed", GenInterfaces.ModuleWithProtocolDunder("__len__"));
    }

    /// <summary>A complete interface implementation that drops its last member — SPY0325 (#2288 Phase 4).</summary>
    [Fact]
    public void InterfaceMemberRemoved_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectInterfaceMemberRemoved,
            "interface member removed", GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true));
    }

    /// <summary>The Optional used as its payload in the <c>is None</c> branch of a
    /// <c>GenTyped.NarrowedBlock</c> — SPY0220 (#2288 Phase 4).</summary>
    [Fact]
    public void UseInFailedNarrowingBranch_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectUseInFailedNarrowingBranch,
            "Optional used in the failed narrowing branch", ProgramWithNarrowing(fuel: 2));
    }

    /// <summary>
    /// <c>GenTyped.NarrowedBlock</c>'s first caller is sound by construction (#2288 Phase 4): every
    /// RAW sample of <c>TypedProgram(WithOptionals, …, withStatements: true)</c> analyses clean —
    /// judged per sample, never filtered — and the share that carries a narrowing block is printed
    /// and must be non-zero, so the soundness claim covers the block. Fuel 2, so the generator nests
    /// conditionals (held at fuel 1 while the Unparser wrote a conditional in a conditional's test
    /// position without parentheses, #2169).
    /// </summary>
    [Fact]
    public void NarrowedProgram_AnalysesClean()
    {
        int narrowedTested = 0;
        var verdicts = ErrorInjector.JudgeSamples(ProgramWithNarrowing(fuel: 2), module =>
        {
            if (HasNarrowedBlock(module))
                Interlocked.Increment(ref narrowedTested);
            return ErrorInjector.JudgeClean(Sharpy.Compiler.Pretty.Unparser.Unparse(module), "narrowed Optional program");
        }, iter: 100, m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));

        _output.WriteLine($"narrowed Optional program: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed clean; "
            + $"{narrowedTested}/{verdicts.Tested} carry a NarrowedBlock");
        Assert.True(verdicts.Tested > 0, "No narrowed Optional programs were generated");
        Assert.True(narrowedTested > 0, $"0/{verdicts.Tested} samples carry a NarrowedBlock — the soundness claim does not reach it");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} narrowed Optional programs refused; the first:\n{verdicts.First}");
    }

    /// <summary>A derived-only member read in the else branch of an <c>isinstance</c> test — SPY0203
    /// (#2288 Phase 4).</summary>
    [Fact]
    public void IsinstanceElseBranchMember_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectIsinstanceElseBranchMember,
            "derived member read in the isinstance else branch", ProgramWithClassHierarchy());
    }

    /// <summary>The positive control of <see cref="IsinstanceElseBranchMember_IsRejected"/>: the same
    /// <c>isinstance</c> block reading a BASE member in its else branch analyses clean, sample for
    /// sample — so SPY0203 is drawn by the else-branch read, not by the narrowing.</summary>
    [Fact]
    public void IsinstanceNarrowing_AnalysesClean()
    {
        var verdicts = ErrorInjector.JudgeSamples(ProgramWithClassHierarchy(), module =>
            ErrorInjector.IsinstanceNarrowingValid(module) is { } valid
                ? ErrorInjector.JudgeClean(Sharpy.Compiler.Pretty.Unparser.Unparse(valid), "isinstance narrowing")
                : "isinstance narrowing is inapplicable to a ModuleWithClasses sample:\n"
                  + Sharpy.Compiler.Pretty.Unparser.Unparse(module),
            iter: 50, m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));

        _output.WriteLine($"isinstance narrowing: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed clean");
        Assert.True(verdicts.Tested > 0, "No class hierarchies were generated");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} isinstance narrowing programs refused; the first:\n{verdicts.First}");
    }

    /// <summary>A <c>const</c> binding reassigned — SPY0225 (#2288 Phase 4).</summary>
    [Fact]
    public void ConstReassignment_IsRejected()
    {
        RunInjectionTest(ErrorInjector.InjectConstReassignment,
            "const reassignment", Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true)));
    }

    private static Gen<Module> ProgramWithNarrowing(int fuel) =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel, withStatements: true));

    private static bool HasNarrowedBlock(Module module) =>
        Sharpy.Compiler.Tests.Infrastructure.AstDescendants.SelfAndDescendants(module).Any(n => n is IfStatement
        {
            Test: ComparisonChain { Operators: [ComparisonOperator.IsNot], Operands: [Identifier, NoneLiteral] },
            ElseBody.Length: 0
        });

    private static Gen<Module> ProgramWithOptionals() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2));

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

        var sink = new RejectionSink($"ErrorCompleteness base generator ({label})");
        var wellTyped = SemanticFilter.WellTypedProgram(baseGen, sink);

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

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        _output.WriteLine(pin is null
            ? $"Error completeness ({label}): {tested - failures.Count}/{tested} drew the expected code"
            : $"Error completeness ({label}): KnownRed {pin.Issue} — {tested - failures.Count}/{tested} still unrefused ({pin.Observed})");
        Assert.True(tested > 0, $"No {label} injections were applicable");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} {label} injections misjudged; the first:\n{failures.FirstOrDefault()}");
    }
}
