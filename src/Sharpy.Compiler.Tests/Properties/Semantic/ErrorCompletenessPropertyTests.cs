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

    /// <summary>P63b (#2324): an int/str operand of <c>+</c>/<c>-</c> replaced by a literal its type refuses — SPY0222 (rows: arithmetic, str + str).</summary>
    [Fact]
    public void WrongTypedArithmeticOperand_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedArithmeticOperand, "wrong-typed arithmetic operand", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): an operand of a user type's <c>__add__</c>/<c>__mul__</c>/… replaced by an int — SPY0222.</summary>
    [Fact]
    public void WrongTypedArithmeticOperand_OnAUserType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedArithmeticOperand, "wrong-typed operand of a user operator", MutationBases.Parsed(GenOperators.BinaryOperatorProgram()));

    /// <summary>P63b (#2324): an ordering or equality operand replaced by a literal of another type — SPY0222.</summary>
    [Fact]
    public void WrongTypedComparisonOperand_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedComparisonOperand, "wrong-typed comparison operand", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): an operand of a user comparison dunder replaced by an int — SPY0222.</summary>
    [Fact]
    public void WrongTypedComparisonOperand_OnAUserType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedComparisonOperand, "wrong-typed operand of a user comparison", MutationBases.Parsed(GenOperators.ComparisonOperatorProgram()));

    /// <summary>P63b (#2324): a unary operand replaced by a str — SPY0223.</summary>
    [Fact]
    public void WrongTypedUnaryOperand_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedUnaryOperand, "wrong-typed unary operand", MutationBases.Parsed(GenOperators.UnaryOperatorProgram()));

    /// <summary>P63b (#2324): an <c>and</c>/<c>or</c> operand replaced by a fixed tuple — SPY0220.</summary>
    [Fact]
    public void NoFalsyCaseLogicalOperand_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectNoFalsyCaseLogicalOperand, "and/or operand with no falsy case", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): an <c>if</c> / conditional test replaced by a fixed tuple — SPY0220 (rows: if/else, truthiness).</summary>
    [Fact]
    public void NoFalsyCaseTruthTest_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectNoFalsyCaseTruthTest, "truth test with no falsy case", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a case guard replaced by a fixed tuple — SPY0241.</summary>
    [Fact]
    public void NoFalsyCaseGuard_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectNoFalsyCaseGuard, "case guard with no falsy case", MutationBases.Parsed(GenMatchPatterns.MatchWithGuards()));

    /// <summary>P63b (#2324): one branch of a conditional stored to a typed slot replaced by a literal the slot refuses — SPY0220.</summary>
    [Fact]
    public void MismatchedConditionalBranch_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectMismatchedConditionalBranch, "conditional branch of another type", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a list index replaced by a str — SPY0220.</summary>
    [Fact]
    public void WrongTypedIndex_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedIndex, "wrong-typed list index", MutationBases.ListSubscripts());

    /// <summary>P63b (#2324): a numeric-spec hole wrapped in <c>str(…)</c> — SPY0609.</summary>
    [Fact]
    public void StrHoleUnderNumericSpec_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectStrHoleUnderNumericSpec, "str hole under a numeric format spec", MutationBases.Parsed(GenFStrings.FStringWithFormatSpecs()));

    /// <summary>P63b (#2324): a name read in an f-string hole replaced by an undefined one — SPY0200.</summary>
    [Fact]
    public void UndefinedNameInFStringHole_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedNameInFStringHole, "undefined name in an f-string hole", MutationBases.Parsed(GenFStrings.FStringWithArithmetic()));

    /// <summary>P63b (#2324): the body's read of the <c>with … as</c> target replaced by an undefined name — SPY0200.</summary>
    [Fact]
    public void UndefinedNameAtWithTarget_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedNameAtWithTarget, "undefined name in place of a with-as target", MutationBases.Parsed(GenContextManagers.ContextManagerWithAsBinding()));

    /// <summary>P63b (#2324): a <c>range</c>/<c>abs</c> argument no overload takes — SPY0354 (rows: range(), abs(), for range).</summary>
    [Fact]
    public void OverloadedBuiltinWrongArgument_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectOverloadedBuiltinWrongArgument, "overloaded builtin with a wrong argument", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): <c>int(…)</c> of a list in an f-string hole — SPY0354 (row: str() / int() conversion).</summary>
    [Fact]
    public void OverloadedBuiltinWrongArgument_InAnFString_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectOverloadedBuiltinWrongArgument, "int() conversion of a list", MutationBases.Parsed(GenFStrings.FStringWithNestedCalls()));

    /// <summary>P63b (#2324): <c>identity[int](…)</c> called with a str — SPY0220.</summary>
    [Fact]
    public void WrongTypedExplicitGenericArgument_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedExplicitGenericArgument, "explicit generic instantiation with a wrong argument", MutationBases.GenericFunctions());

    /// <summary>P63b (#2324): <c>super().__init__(…)</c> with one argument more than the base constructor takes — SPY0224.</summary>
    [Fact]
    public void SuperInitArity_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectSuperInitArity, "super().__init__ with an extra argument", MutationBases.Classes());

    /// <summary>P63b (#2324): a user constructor's argument replaced by a literal its parameter refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedConstructorArgument_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedConstructorArgument, "wrong-typed constructor argument", MutationBases.Classes());

    /// <summary>P63b (#2324): <c>Vec("s", …)</c> inside a user operator's body — SPY0220.</summary>
    [Fact]
    public void WrongTypedConstructorArgument_OfAUserOperatorType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedConstructorArgument, "wrong-typed constructor argument of a user operator type", MutationBases.Parsed(GenOperators.BinaryOperatorProgram()));

    /// <summary>P63b (#2324): a <c>super().__init__(…)</c> argument replaced by a literal the base constructor's parameter refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedSuperInitArgument_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedSuperInitArgument, "wrong-typed super().__init__ argument", MutationBases.Classes());

    /// <summary>P63b (#2324): a user constructor called with one argument more than its <c>__init__</c> takes — SPY0224.</summary>
    [Fact]
    public void ConstructorArity_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectConstructorArity, "constructor with an extra argument", MutationBases.Classes());

    /// <summary>P63b (#2324): a user method called with one argument more than it takes — SPY0224.</summary>
    [Fact]
    public void MethodArity_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectMethodArity, "method with an extra argument", MutationBases.Classes());

    /// <summary>P63b (#2324): <c>str(n, 0)</c> in an f-string hole — SPY0224 (row: str() / int() conversion).</summary>
    [Fact]
    public void FixedArityBuiltinArity_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectFixedArityBuiltinArity, "str() with an extra argument", MutationBases.Parsed(GenFStrings.FStringWithNestedCalls()));

    /// <summary>P63b (#2324): a field or method read renamed to a member no class declares — SPY0203.</summary>
    [Fact]
    public void UndefinedMember_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedMember, "undefined member", MutationBases.Classes());

    /// <summary>P63b (#2324): a field store/read renamed to a field the class does not declare — SPY0203.</summary>
    [Fact]
    public void UndefinedField_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedField, "undefined field", MutationBases.Classes());

    /// <summary>P63b (#2324): <c>s.upper()</c> renamed to a method str does not have — SPY0203.</summary>
    [Fact]
    public void UndefinedMethod_OnStr_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedMethod, "undefined str method", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a method call in an f-string hole renamed to a method the class does not have — SPY0203.</summary>
    [Fact]
    public void UndefinedMethod_InAnFString_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedMethod, "undefined method in an f-string hole", MutationBases.Parsed(GenFStrings.FStringWithMethodCalls()));

    /// <summary>P63b (#2324): <c>a + b</c> on a user type rewritten <c>a.__add__(b)</c> — SPY0427.</summary>
    [Fact]
    public void DunderCalledByName_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectDunderCalledByName, "dunder called by name", MutationBases.Parsed(GenOperators.BinaryOperatorProgram()));

    /// <summary>P63b (#2324): a comparison dunder declared <c>-> int</c> — SPY0321 (rows: comparison dunders, user __eq__).</summary>
    [Fact]
    public void DunderWrongReturnType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectDunderWrongReturnType, "comparison dunder returning int", MutationBases.Parsed(GenOperators.ComparisonOperatorProgram()));

    /// <summary>P63b (#2324): a parameter annotation naming an undefined type — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtParameter_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtParameter, "undefined parameter type", MutationBases.Functions());

    /// <summary>P63b (#2324): <c>def identity[T](x: Nope_xyz)</c> — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtParameter_OfAGenericFunction_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtParameter, "undefined parameter type of a generic function", MutationBases.GenericFunctions());

    /// <summary>P63b (#2324): an interface's base naming an undefined interface — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtBase_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtBase, "undefined base interface", GenInterfaces.ModuleWithInterfaceHierarchy());

    /// <summary>P63b (#2324): a generic base's type argument naming an undefined type — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtTypeArgument_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtTypeArgument, "undefined type argument of a generic base", MutationBases.GenericInheritance());

    /// <summary>P63b (#2324): <c>case Dog():</c> renamed to an undefined class — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtPatternHead_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtPatternHead, "undefined class pattern head", MutationBases.Parsed(GenMatchPatterns.MatchWithTypePatterns()));

    /// <summary>P63b (#2324): a generator's element-type annotation naming an undefined type — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtReturn_OfAGenerator_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtReturn, "undefined element-type annotation of a generator", GenIterators.ModuleWithGenerator(valid: true));

    /// <summary>P63b (#2324): a local declaration's annotation naming an undefined type (<c>x: Nope_xyz = 0</c>) — SPY0202.</summary>
    [Fact]
    public void UndefinedTypeAtDeclaration_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectUndefinedTypeAtDeclaration, "undefined declaration type", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): <c>class Factory(IProducer[int, int])</c> — SPY0224.</summary>
    [Fact]
    public void TypeArgumentArity_OfAGenericInterfaceBase_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectTypeArgumentArity, "generic interface base with an extra type argument", MutationBases.Parsed(GenVariance.CovariantInterfaceProgram()));

    /// <summary>P63b (#2324): <c>nums: list[int, int]</c> — SPY0224.</summary>
    [Fact]
    public void TypeArgumentArity_OfAListAnnotation_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectTypeArgumentArity, "list annotation with an extra type argument", MutationBases.TypedCollections());

    /// <summary>P63b (#2324): a reassignment of a declared local storing a literal its type refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedReassignment_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedReassignment, "wrong-typed reassignment", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): an annotated declaration's initializer replaced by a literal its annotation refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedDeclarationInitializer_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedDeclarationInitializer, "wrong-typed declaration initializer", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a <c>list[T]</c> declaration initialized with an element its annotation refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedListInitializer_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedListInitializer, "list initializer of another element type", MutationBases.TypedCollections());

    /// <summary>P63b (#2324): a <c>return</c> value replaced by a literal the declared return type refuses — SPY0260.</summary>
    [Fact]
    public void WrongTypedReturnValue_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedReturnValue, "wrong-typed return value", MutationBases.Functions());

    /// <summary>P63b (#2324): a <c>return value</c> in a def with a non-None return type dropped to a bare <c>return</c> — SPY0260.</summary>
    [Fact]
    public void DroppedReturnValue_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectDroppedReturnValue, "dropped return value", MutationBases.Functions());

    /// <summary>P63b (#2324): <c>main</c> (no return annotation) gains <c>return 1</c> — SPY0260.</summary>
    [Fact]
    public void ReturnValueInImplicitNoneDef_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectReturnValueInImplicitNoneDef, "return value in an implicit-None def", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a <c>yield</c> value replaced by a literal the element type refuses — SPY0220.</summary>
    [Fact]
    public void WrongTypedYield_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectWrongTypedYield, "wrong-typed yield", GenIterators.ModuleWithGenerator(valid: true));

    /// <summary>P63b (#2324): <c>yield from 5</c> — SPY0220.</summary>
    [Fact]
    public void YieldFromNonIterable_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectYieldFromNonIterable, "yield from a non-iterable", GenIterators.ModuleWithYieldFrom());

    /// <summary>P63b (#2324): a literal pattern replaced by one of a type the subject refuses — SPY0220.</summary>
    [Fact]
    public void LiteralPatternOfOtherType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectLiteralPatternOfOtherType, "literal pattern of another type", MutationBases.Parsed(GenMatchPatterns.MatchWithOrPatterns()));

    /// <summary>P63b (#2324): <c>case (1 as bound_xyz) | 2</c> — SPY0359.</summary>
    [Fact]
    public void OrPatternBindingMismatch_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectOrPatternBindingMismatch, "or-pattern alternative that binds", MutationBases.Parsed(GenMatchPatterns.MatchWithOrPatterns()));

    /// <summary>P63b (#2324): <c>case [a, b] and a</c> — SPY0372.</summary>
    [Fact]
    public void AndPatternDuplicateCapture_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectAndPatternDuplicateCapture, "and-pattern binding a capture twice", MutationBases.Parsed(GenMatchPatterns.MatchWithAndPatterns()));

    /// <summary>P63b (#2324): a tuple pattern gains a trailing wildcard in an arm that reads no capture — SPY0257.</summary>
    [Fact]
    public void TuplePatternExtraElement_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectTuplePatternExtraElement, "tuple pattern longer than its subject", MutationBases.Parsed(GenMatchPatterns.MatchWithNestedPatterns()));

    /// <summary>P63b (#2324): the trailing <c>case _:</c> moved first — SPY0700.</summary>
    [Fact]
    public void IrrefutableArmFirst_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectIrrefutableArmFirst, "wildcard arm first", MutationBases.Parsed(GenMatchPatterns.MatchWithOrPatterns()));

    /// <summary>P63b (#2324): a list pattern gains a leading literal its element type refuses — SPY0220.</summary>
    [Fact]
    public void ListPatternElementOfOtherType_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectListPatternElementOfOtherType, "list pattern element of another type", MutationBases.Parsed(GenMatchPatterns.MatchWithListPatterns()));

    /// <summary>P63b (#2324): <c>except int as e0</c> — SPY0399.</summary>
    [Fact]
    public void HandlerTypeNotException_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectHandlerTypeNotException, "handler of a non-exception type", GenExceptions.ModuleWithTryExcept(handlerCount: 1));

    /// <summary>P63b (#2324): <c>raise 42</c> — SPY0242.</summary>
    [Fact]
    public void RaiseNonException_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectRaiseNonException, "raise of a non-exception", GenExceptions.ModuleWithRaiseExpression());

    /// <summary>P63b (#2324): a zero-argument <c>__exit__(self)</c> takes two more parameters — SPY0320.</summary>
    [Fact]
    public void ExitArity_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectExitArity, "__exit__ with extra parameters", MutationBases.Parsed(GenContextManagers.ValidContextManagerProgram()));

    /// <summary>P63b (#2324): the manager's <c>async __aenter__/__aexit__</c> become sync <c>__enter__/__exit__</c> — SPY0324.</summary>
    [Fact]
    public void AsyncWithOverSyncManager_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectAsyncWithOverSyncManager, "async with over a sync manager", MutationBases.Parsed(GenContextManagers.AsyncContextManagerProgram()));

    /// <summary>P63b (#2324): an <c>@override @final</c> method renamed so no base member has its name — SPY0248.</summary>
    [Fact]
    public void OverrideWithoutBaseMember_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectOverrideWithoutBaseMember, "@override without a base member", GenDecorators.ModuleWithDecoratedFunction(validDecorator: true));

    /// <summary>P63b (#2324): the derived class's <c>@override</c> method renamed — SPY0248 (row: class Derived(Base)).</summary>
    [Fact]
    public void OverrideWithoutBaseMember_InAHierarchy_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectOverrideWithoutBaseMember, "@override without a base member in a class hierarchy", MutationBases.Classes());

    /// <summary>P63b (#2324): a local renamed camelCase (declaration and reads) — exactly one SPY0453 warning and no error.</summary>
    [Fact]
    public void CamelCaseLocal_IsWarned() =>
        RunInjectionTest(ErrorInjector.InjectCamelCaseLocal, "camelCase local", MutationBases.Parsed(GenFStrings.FStringWithArithmetic()));

    /// <summary>P63b (#2324): a class implementing two interfaces drops the last member of one — SPY0325.</summary>
    [Fact]
    public void InterfaceMemberRemoved_OfOneOfTwoInterfaces_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectInterfaceMemberRemoved, "interface member removed (two interfaces)", GenInterfaces.ModuleWithMultipleInterfaces());

    /// <summary>P63b (#2324): a call statement of <c>main</c> rewritten <c>call if True else call</c> — a
    /// value-producing expression as a statement, SPY0603 (row: statements.md expression statement).</summary>
    [Fact]
    public void ConditionalExpressionStatement_IsRejected() =>
        RunInjectionTest(ErrorInjector.InjectConditionalExpressionStatement, "conditional expression statement", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a <c>try</c> whose handlers go subclass → base, reordered base first: the
    /// subclass handler is dead, Sharpy accepts it and Roslyn refuses it (SPY0908 CS0160) — a KnownRed pin
    /// on #2363.</summary>
    [Fact]
    public void HandlerOrderBaseFirst_IsPinned() =>
        RunInjectionTest(ErrorInjector.InjectHandlerOrderBaseFirst, "handler order base first", GenExceptions.ModuleWithExceptionHierarchy());

    /// <summary>P63b (#2324): <c>def main(arg: int)</c>: Sharpy accepts it and Roslyn refuses it (SPY0908 CS1558) — a KnownRed pin on #2335 that fails the first time the program is refused.</summary>
    [Fact]
    public void MainParameter_IsPinned() =>
        RunInjectionTest(ErrorInjector.InjectMainParameter, "main with a parameter", MutationBases.TypedStatements());

    /// <summary>P63b (#2324): a <c>@dataclass</c> constructed with an argument its field refuses: SPY0908 CS1503 — a KnownRed pin on #2021.</summary>
    [Fact]
    public void DataclassWrongArgument_IsPinned() =>
        RunInjectionTest(ErrorInjector.InjectDataclassWrongArgument, "@dataclass constructed with a wrong argument", GenDecorators.ModuleWithDataclass());

    /// <summary>P63b (#2324): a <c>@dataclass</c> constructed with one argument fewer than it has fields: SPY0908 CS7036 — a KnownRed pin on #2021.</summary>
    [Fact]
    public void DataclassArity_IsPinned() =>
        RunInjectionTest(ErrorInjector.InjectDataclassArity, "@dataclass constructed with a missing argument", GenDecorators.ModuleWithDataclass());

    /// <summary>P63b (#2324): the library's module-level typed constant stores a literal its annotation
    /// refuses, in a two-file project — SPY0220.</summary>
    [Fact]
    public void WrongTypedLibraryConstant_IsRejected() =>
        RunProjectInjectionTest(ErrorInjector.InjectWrongTypedLibraryConstant, "wrong-typed library constant");

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
    /// unrelated refusal or an internal error passed for the refusal under test. A mutated program whose
    /// source equals its input's is a failure too: the mutation never reached the judged text (#2324 —
    /// every f-string lane judged the valid program until the rewriter dropped stale captured holes).
    /// <see cref="PropertyIterations.Injection"/> samples (100; <c>SHARPY_PROPERTY_ITER</c> overrides).
    /// The injector runs on each raw draw BEFORE the well-typed filter, and only a draw it applies to is
    /// analysed (#2324, R-IX): before, every draw was analysed first and most lanes' injectors then
    /// returned null, so a lane applicable to a tenth of its base paid ten analyses per judged sample.
    /// </summary>
    private void RunInjectionTest(
        Func<Module, ErrorInjector.InjectionResult?> injector,
        string label,
        Gen<Module>? overrideGen = null)
    {
        int tested = 0;
        var failures = new List<string>();
        var sources = new HashSet<string>(StringComparer.Ordinal);
        ErrorInjector.KnownRedRefusal? pin = null;

        var baseGen = overrideGen ?? Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2));

        var sink = new RejectionSink($"ErrorCompleteness base generator ({label})");
        var drawn = baseGen.Select(module => (Module: module, Result: injector(module)));
        var wellTyped = SemanticFilter.WellTypedProgram(drawn, draw => draw.Result is null ? null : draw.Module, sink);

        wellTyped.Sample(draw =>
        {
            var (module, result) = draw;
            if (result == null)
                return;

            Interlocked.Increment(ref tested);
            pin = result.KnownRed;
            var mutatedSource = Sharpy.Compiler.Pretty.Unparser.Unparse(result.Mutated);
            lock (sources)
                sources.Add(mutatedSource);
            if (mutatedSource == Sharpy.Compiler.Pretty.Unparser.Unparse(module))
            {
                lock (failures)
                    failures.Add($"Injected {label} left the source unchanged — the mutation never reached the judged text:\n{mutatedSource}");
            }
            else if (ErrorInjector.Judge(result, label) is { } failure)
            {
                lock (failures)
                    failures.Add(failure);
            }
        }, print: draw => Sharpy.Compiler.Pretty.Unparser.Unparse(draw.Module), iter: PropertyIterations.Injection);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        _output.WriteLine(pin is null
            ? $"Error completeness ({label}): {tested - failures.Count}/{tested} drew the expected code ({sources.Count} distinct mutated sources)"
            : $"Error completeness ({label}): KnownRed {pin.Issue} — {tested - failures.Count}/{tested} still unrefused ({pin.Observed}; {sources.Count} distinct mutated sources)");
        Assert.True(tested > 0, $"No {label} injections were applicable");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} {label} injections misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    /// <summary>The multi-file face of <see cref="RunInjectionTest"/>: every applicable
    /// <c>GenImports.ImportingModule</c> pair that compiles clean as one project, injected, draws exactly
    /// the injection's code through <see cref="ErrorInjector.Judge(ErrorInjector.ProjectInjectionResult, string)"/>.</summary>
    private void RunProjectInjectionTest(
        Func<(string MainSource, string LibSource), ErrorInjector.ProjectInjectionResult?> injector,
        string label)
    {
        int tested = 0;
        var failures = new List<string>();
        var sink = new RejectionSink($"ErrorCompleteness base project ({label})");
        SemanticFilter.CompilableProject(GenImports.ImportingModule(TypeEnv.Default, fuel: 1),
                pair => new[] { ("main.spy", pair.MainSource), ("lib.spy", pair.LibSource) }, sink)
            .Sample(pair =>
            {
                var result = injector(pair);
                if (result == null)
                    return;
                Interlocked.Increment(ref tested);
                if (result.Files.Any(f => f.Path == "lib.spy" && f.Content == Sharpy.Compiler.Pretty.Unparser.Unparse(AstRewriter.Parse(pair.LibSource))))
                {
                    lock (failures)
                        failures.Add($"Injected {label} left the library unchanged:\n{pair.LibSource}");
                }
                else if (ErrorInjector.Judge(result, label) is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, iter: PropertyIterations.Injection);

        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        _output.WriteLine($"Error completeness ({label}): {tested - failures.Count}/{tested} drew the expected code");
        Assert.True(tested > 0, $"No {label} injections were applicable");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} {label} injections misjudged; the first:\n{failures.FirstOrDefault()}");
    }
}
