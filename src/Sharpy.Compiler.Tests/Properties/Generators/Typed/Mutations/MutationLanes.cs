using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Properties.Generators.Typed.PositionMutations;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The named mutation lanes (#2324, P63b): each is a position, a family filter and a one-fact break —
/// a pure <c>Module → Module?</c> with no diagnostic code, so the census can measure what a lane draws
/// before <c>ErrorInjector.Mutations.cs</c> wraps it with the ONE code it must draw. <see cref="Census"/>
/// is the lane × base matrix the census samples.
/// </summary>
internal static class MutationLanes
{
    // ---- operands -----------------------------------------------------------------------------
    /// <summary>An arithmetic operand replaced by a literal its type refuses — on <c>+</c>/<c>-</c> for builtin
    /// operands (<c>"s" * 3</c> and <c>x * "s"</c> are str repetition, measured CLEAN / SPY0220), on any operator
    /// a user type implements.</summary>
    public static Module? WrongTypedArithmeticOperand(Module m) => Mutate(m, Position.Operand, AdditiveOrUserOperator, WrongTypedLiteral);

    private static bool AdditiveOrUserOperator(ProgramFacts facts, Site site) =>
        site.Family == "Arithmetic" && site.Parent is BinaryOp b
        && (b.Operator is BinaryOperator.Add or BinaryOperator.Subtract
            || facts.TypeOf(b.Left) is { } left && facts.Types.ContainsKey(left));
    public static Module? WrongTypedComparisonOperand(Module m) => Mutate(m, Position.Operand, Families("Comparison", "Equality"), WrongTypedLiteral);
    public static Module? WrongTypedEqualityOperand(Module m) => Mutate(m, Position.Operand, Families("Equality"), WrongTypedLiteral);
    /// <summary>A unary operand replaced by a str literal (<c>-42</c> is a valid negation, so a user-type operand
    /// replaced by <c>42</c> drew nothing at the operator, measured).</summary>
    public static Module? WrongTypedUnaryOperand(Module m) => Mutate(m, Position.Operand, Families("Unary"), StrOperand);
    public static Module? NoFalsyCaseLogicalOperand(Module m) => Mutate(m, Position.Operand, Families("Logical"), NoFalsyCaseLiteral);
    public static Module? NoFalsyCaseTruthTest(Module m) => Mutate(m, Position.Operand, Families("TruthTest"), NoFalsyCaseLiteral);
    public static Module? NoFalsyCaseGuard(Module m) => Mutate(m, Position.Operand, Families("Guard"), NoFalsyCaseLiteral);
    public static Module? MismatchedConditionalBranch(Module m) => Mutate(m, Position.Operand, Families("ConditionalBranch"), WrongTypedLiteral);
    public static Module? WrongTypedIndex(Module m) => Mutate(m, Position.Operand, Families("Index"), WrongTypedLiteral);
    public static Module? StrHoleUnderNumericSpec(Module m) => Mutate(m, Position.Operand, Families("FStringNumericSpec"), PositionMutations.StrHoleUnderNumericSpec);
    public static Module? NonOptionalIdentityOperand(Module m) => Mutate(m, Position.Operand, Families("Identity"), PositionMutations.NonOptionalIdentityOperand);

    // ---- names --------------------------------------------------------------------------------
    public static Module? UndefinedNameInFStringHole(Module m) => Mutate(m, Position.Name, Families("FStringHole"), UndefinedNameAtUse);
    public static Module? UndefinedNameAtWithTarget(Module m) => Mutate(m, Position.Name, Families("WithTarget"), UndefinedNameAtUse);

    // ---- arguments and calls ------------------------------------------------------------------
    public static Module? WrongTypedOverloadedBuiltinArgument(Module m) => Mutate(m, Position.Argument, Families("OverloadedBuiltin"), WrongTypedLiteral);
    public static Module? WrongTypedConstructorArgument(Module m) => Mutate(m, Position.Argument, Families("Constructor"), WrongTypedLiteral);
    public static Module? WrongTypedSuperInitArgument(Module m) => Mutate(m, Position.Argument, Families("SuperInit"), WrongTypedLiteral);
    public static Module? WrongTypedExplicitGenericArgument(Module m) => Mutate(m, Position.Argument, Families("ExplicitGeneric"), WrongTypedLiteral);
    public static Module? WrongTypedDataclassArgument(Module m) => Mutate(m, Position.Argument, Families("Dataclass"), WrongTypedLiteral);
    public static Module? StrBuiltinArityPlusOne(Module m) => Mutate(m, Position.Call, Families("StrBuiltin"), ArityPlusOne);
    public static Module? MethodArityPlusOne(Module m) => Mutate(m, Position.Call, Families("Method", "StrMethod"), ArityPlusOne);
    public static Module? SuperInitArityPlusOne(Module m) => Mutate(m, Position.Call, Families("SuperInit"), ArityPlusOne);
    public static Module? ConstructorArityPlusOne(Module m) => Mutate(m, Position.Call, Families("Constructor"), ArityPlusOne);
    public static Module? DataclassArityMinusOne(Module m) => Mutate(m, Position.Call, Families("Dataclass"), ArityMinusOne);

    // ---- member names -------------------------------------------------------------------------
    public static Module? UndefinedMethod(Module m) => Mutate(m, Position.MemberName, Families("Method", "StrMethod", "FStringMethod"), PositionMutations.UndefinedMember);
    public static Module? UndefinedField(Module m) => Mutate(m, Position.MemberName, Families("Field", "FieldStore"), PositionMutations.UndefinedMember);
    public static Module? UndefinedMember(Module m) => Mutate(m, Position.MemberName, Families("Method", "StrMethod", "FStringMethod", "Field", "FieldStore"), PositionMutations.UndefinedMember);
    public static Module? DunderCalledByName(Module m) => Mutate(m, Position.MemberName, Families("OperatorDunder"), DunderByName);

    // ---- annotations --------------------------------------------------------------------------
    public static Module? UndefinedTypeAtParameter(Module m) => Mutate(m, Position.Annotation, Families("Parameter"), UndefinedTypeAtAnnotation);
    public static Module? UndefinedTypeAtBase(Module m) => Mutate(m, Position.Annotation, Families("BaseClass", "BaseInterface"), UndefinedTypeAtAnnotation);
    public static Module? UndefinedTypeAtTypeArgument(Module m) => Mutate(m, Position.Annotation, Families("TypeArgument"), UndefinedTypeAtAnnotation);
    public static Module? UndefinedTypeAtPatternHead(Module m) => Mutate(m, Position.Annotation, Families("PatternHead"), UndefinedTypeAtAnnotation);
    public static Module? UndefinedTypeAtDeclaration(Module m) => Mutate(m, Position.Annotation, Families("Declaration"), UndefinedTypeAtAnnotation);
    public static Module? UndefinedTypeAtReturn(Module m) => Mutate(m, Position.Annotation, Families("Return"), UndefinedTypeAtAnnotation);
    public static Module? TypeArgumentArity(Module m) => Mutate(m, Position.Annotation, Families("BaseClass", "BaseInterface", "Declaration"), TypeArgumentArityPlusOne);
    public static Module? ChangedDeclarationAnnotation(Module m) => Mutate(m, Position.Annotation, Families("Declaration"), ChangedScalarAnnotation);

    // ---- stores -------------------------------------------------------------------------------
    public static Module? WrongTypedDeclarationInitializer(Module m) => Mutate(m, Position.Store, Families("AnnotatedDeclaration"), WrongTypedLiteral);
    public static Module? WrongTypedReassignment(Module m) => Mutate(m, Position.Store, Families("Reassignment"), WrongTypedLiteral);
    public static Module? WrongTypedListInitializer(Module m) => Mutate(m, Position.Store, Families("ListInitializer"), WrongTypedListElement);
    public static Module? WrongTypedModuleConstant(Module m) => Mutate(m, Position.Store, Families("ModuleConstant"), WrongTypedLiteral);

    // ---- return values ------------------------------------------------------------------------
    public static Module? WrongTypedReturn(Module m) => Mutate(m, Position.ReturnValue, Families("Return", "MethodReturn"), WrongTypedReturnValue);
    public static Module? DroppedReturn(Module m) => Mutate(m, Position.ReturnValue, Families("Return", "MethodReturn"), DroppedReturnValue);
    public static Module? ReturnValueInImplicitNoneDef(Module m) => Mutate(m, Position.ReturnValue, Families("ImplicitNoneDef"), AddedReturnValue);
    public static Module? WrongTypedYieldValue(Module m) => Mutate(m, Position.ReturnValue, Families("Yield"), WrongTypedYield);
    public static Module? YieldFromNonIterableValue(Module m) => Mutate(m, Position.ReturnValue, Families("YieldFrom"), YieldFromNonIterable);

    // ---- patterns -----------------------------------------------------------------------------
    public static Module? LiteralPatternOfOtherTypeLane(Module m) => Mutate(m, Position.Pattern, Families("Literal"), LiteralPatternOfOtherType);
    public static Module? OrPatternBindingMismatchLane(Module m) => Mutate(m, Position.Pattern, Families("Or"), OrPatternBindingMismatch);
    public static Module? AndPatternDuplicateCaptureLane(Module m) => Mutate(m, Position.Pattern, Families("And"), AndPatternDuplicateCapture);
    public static Module? TuplePatternExtraElement(Module m) => Mutate(m, Position.Pattern, Families("Tuple"), TuplePatternExtraWildcard);
    public static Module? IrrefutableArmFirstLane(Module m) => Mutate(m, Position.Pattern, Families("WildcardLast"), IrrefutableArmFirst);
    public static Module? ListPatternElementOfOtherTypeLane(Module m) => Mutate(m, Position.Pattern, Families("List"), ListPatternElementOfOtherType);

    // ---- declaration shapes -------------------------------------------------------------------
    public static Module? HandlerTypeNotException(Module m) => MutateModule(m, DeclarationShapes.HandlerTypeNotException);
    public static Module? RaiseNonException(Module m) => MutateModule(m, DeclarationShapes.RaiseNonException);
    public static Module? HandlerOrderBaseFirst(Module m) => MutateModule(m, DeclarationShapes.HandlerOrderBaseFirst);
    public static Module? ExitArity(Module m) => MutateModule(m, DeclarationShapes.ExitArity);
    public static Module? AsyncWithOverSyncManager(Module m) => MutateModule(m, DeclarationShapes.AsyncWithOverSyncManager);
    public static Module? OverrideWithoutBaseMember(Module m) => MutateModule(m, DeclarationShapes.OverrideWithoutBaseMember);
    public static Module? CamelCaseLocal(Module m) => MutateModule(m, DeclarationShapes.CamelCaseLocal);
    public static Module? MainParameter(Module m) => MutateModule(m, DeclarationShapes.MainParameter);
    public static Module? DunderWrongReturnType(Module m) => MutateModule(m, DeclarationShapes.DunderWrongReturnType);
    public static Module? ExpressionStatementOfAnyValue(Module m) => MutateModule(m, DeclarationShapes.ExpressionStatementOfAnyValue);
    public static Module? ConditionalExpressionStatement(Module m) => MutateModule(m, DeclarationShapes.ConditionalExpressionStatement);

    // ---- the multi-file lane ------------------------------------------------------------------

    /// <summary>The library's module-level typed constant stores a literal its annotation refuses
    /// (<c>GenImports.ImportingModule</c>; the importing file is untouched).</summary>
    public static (string MainSource, string LibSource)? WrongTypedLibraryConstant((string MainSource, string LibSource) project) =>
        WrongTypedModuleConstant(AstRewriter.Parse(project.LibSource)) is { } lib
            ? (project.MainSource, Sharpy.Compiler.Pretty.Unparser.Unparse(lib))
            : null;

    // ---- the census matrix --------------------------------------------------------------------

    /// <summary>One census row: a lane over a base.</summary>
    public sealed record CensusRow(string Lane, Func<Module, Module?> Mutate, string Base, Func<Gen<Module>> Generator);

    private static CensusRow Row(string lane, Func<Module, Module?> mutate, string @base, Func<Gen<Module>> generator) =>
        new(lane, mutate, @base, generator);

    private static IEnumerable<CensusRow> Over(string @base, Func<Gen<Module>> generator, params (string Lane, Func<Module, Module?> Mutate)[] lanes) =>
        lanes.Select(l => Row(l.Lane, l.Mutate, @base, generator));

    /// <summary>Every (lane, base) pair the census samples — the matrix's position × break × family cells
    /// over the generators that emit them.</summary>
    public static IReadOnlyList<CensusRow> Census { get; } = new[]
    {
        Over("TypedStatements", MutationBases.TypedStatements,
            (nameof(WrongTypedArithmeticOperand), WrongTypedArithmeticOperand),
            (nameof(WrongTypedComparisonOperand), WrongTypedComparisonOperand),
            (nameof(WrongTypedEqualityOperand), WrongTypedEqualityOperand),
            (nameof(NoFalsyCaseLogicalOperand), NoFalsyCaseLogicalOperand),
            (nameof(NoFalsyCaseTruthTest), NoFalsyCaseTruthTest),
            (nameof(MismatchedConditionalBranch), MismatchedConditionalBranch),
            (nameof(WrongTypedOverloadedBuiltinArgument), WrongTypedOverloadedBuiltinArgument),
            (nameof(UndefinedMethod), UndefinedMethod),
            (nameof(UndefinedTypeAtDeclaration), UndefinedTypeAtDeclaration),
            (nameof(ChangedDeclarationAnnotation), ChangedDeclarationAnnotation),
            (nameof(WrongTypedDeclarationInitializer), WrongTypedDeclarationInitializer),
            (nameof(WrongTypedReassignment), WrongTypedReassignment),
            (nameof(ReturnValueInImplicitNoneDef), ReturnValueInImplicitNoneDef),
            (nameof(CamelCaseLocal), CamelCaseLocal),
            (nameof(MainParameter), MainParameter),
            (nameof(ExpressionStatementOfAnyValue), ExpressionStatementOfAnyValue),
            (nameof(ConditionalExpressionStatement), ConditionalExpressionStatement)),
        Over("TypedCollections", MutationBases.TypedCollections,
            (nameof(WrongTypedListInitializer), WrongTypedListInitializer),
            (nameof(TypeArgumentArity), TypeArgumentArity),
            (nameof(WrongTypedOverloadedBuiltinArgument), WrongTypedOverloadedBuiltinArgument)),
        Over("TypedOptionals", MutationBases.TypedOptionals,
            (nameof(NonOptionalIdentityOperand), NonOptionalIdentityOperand)),
        Over("ListSubscripts", MutationBases.ListSubscripts,
            (nameof(WrongTypedIndex), WrongTypedIndex)),
        Over("Functions", MutationBases.Functions,
            (nameof(WrongTypedArithmeticOperand), WrongTypedArithmeticOperand),
            (nameof(UndefinedTypeAtParameter), UndefinedTypeAtParameter),
            (nameof(UndefinedTypeAtReturn), UndefinedTypeAtReturn),
            (nameof(WrongTypedReturn), WrongTypedReturn),
            (nameof(DroppedReturn), DroppedReturn)),
        Over("Classes", MutationBases.Classes,
            (nameof(WrongTypedConstructorArgument), WrongTypedConstructorArgument),
            (nameof(WrongTypedSuperInitArgument), WrongTypedSuperInitArgument),
            (nameof(SuperInitArityPlusOne), SuperInitArityPlusOne),
            (nameof(ConstructorArityPlusOne), ConstructorArityPlusOne),
            (nameof(MethodArityPlusOne), MethodArityPlusOne),
            (nameof(UndefinedMethod), UndefinedMethod),
            (nameof(UndefinedField), UndefinedField),
            (nameof(UndefinedMember), UndefinedMember),
            (nameof(UndefinedTypeAtBase), UndefinedTypeAtBase),
            (nameof(OverrideWithoutBaseMember), OverrideWithoutBaseMember)),
        Over("BinaryOperatorProgram", () => MutationBases.Parsed(GenOperators.BinaryOperatorProgram()),
            (nameof(WrongTypedArithmeticOperand), WrongTypedArithmeticOperand),
            (nameof(DunderCalledByName), DunderCalledByName),
            (nameof(WrongTypedConstructorArgument), WrongTypedConstructorArgument)),
        Over("UnaryOperatorProgram", () => MutationBases.Parsed(GenOperators.UnaryOperatorProgram()),
            (nameof(WrongTypedUnaryOperand), WrongTypedUnaryOperand),
            (nameof(DunderCalledByName), DunderCalledByName)),
        Over("ComparisonOperatorProgram", () => MutationBases.Parsed(GenOperators.ComparisonOperatorProgram()),
            (nameof(WrongTypedComparisonOperand), WrongTypedComparisonOperand),
            (nameof(WrongTypedEqualityOperand), WrongTypedEqualityOperand),
            (nameof(DunderCalledByName), DunderCalledByName),
            (nameof(DunderWrongReturnType), DunderWrongReturnType)),
        Over("FStringWithArithmetic", () => MutationBases.Parsed(GenFStrings.FStringWithArithmetic()),
            (nameof(UndefinedNameInFStringHole), UndefinedNameInFStringHole),
            (nameof(WrongTypedArithmeticOperand), WrongTypedArithmeticOperand),
            (nameof(CamelCaseLocal), CamelCaseLocal)),
        Over("FStringWithFormatSpecs", () => MutationBases.Parsed(GenFStrings.FStringWithFormatSpecs()),
            (nameof(StrHoleUnderNumericSpec), StrHoleUnderNumericSpec),
            (nameof(CamelCaseLocal), CamelCaseLocal)),
        Over("FStringWithMethodCalls", () => MutationBases.Parsed(GenFStrings.FStringWithMethodCalls()),
            (nameof(UndefinedMethod), UndefinedMethod)),
        Over("FStringWithNestedCalls", () => MutationBases.Parsed(GenFStrings.FStringWithNestedCalls()),
            (nameof(WrongTypedOverloadedBuiltinArgument), WrongTypedOverloadedBuiltinArgument),
            (nameof(StrBuiltinArityPlusOne), StrBuiltinArityPlusOne)),
        Over("ContextManagerWithAsBinding", () => MutationBases.Parsed(GenContextManagers.ContextManagerWithAsBinding()),
            (nameof(UndefinedNameAtWithTarget), UndefinedNameAtWithTarget),
            (nameof(UndefinedMethod), UndefinedMethod)),
        Over("ValidContextManagerProgram", () => MutationBases.Parsed(GenContextManagers.ValidContextManagerProgram()),
            (nameof(ExitArity), ExitArity)),
        Over("AsyncContextManagerProgram", () => MutationBases.Parsed(GenContextManagers.AsyncContextManagerProgram()),
            (nameof(AsyncWithOverSyncManager), AsyncWithOverSyncManager)),
        Over("MatchWithGuards", () => MutationBases.Parsed(GenMatchPatterns.MatchWithGuards()),
            (nameof(NoFalsyCaseGuard), NoFalsyCaseGuard),
            (nameof(IrrefutableArmFirstLane), IrrefutableArmFirstLane)),
        Over("MatchWithOrPatterns", () => MutationBases.Parsed(GenMatchPatterns.MatchWithOrPatterns()),
            (nameof(LiteralPatternOfOtherTypeLane), LiteralPatternOfOtherTypeLane),
            (nameof(OrPatternBindingMismatchLane), OrPatternBindingMismatchLane),
            (nameof(IrrefutableArmFirstLane), IrrefutableArmFirstLane)),
        Over("MatchWithListPatterns", () => MutationBases.Parsed(GenMatchPatterns.MatchWithListPatterns()),
            (nameof(ListPatternElementOfOtherTypeLane), ListPatternElementOfOtherTypeLane)),
        Over("MatchWithAndPatterns", () => MutationBases.Parsed(GenMatchPatterns.MatchWithAndPatterns()),
            (nameof(AndPatternDuplicateCaptureLane), AndPatternDuplicateCaptureLane)),
        Over("MatchWithTypePatterns", () => MutationBases.Parsed(GenMatchPatterns.MatchWithTypePatterns()),
            (nameof(UndefinedTypeAtPatternHead), UndefinedTypeAtPatternHead),
            (nameof(IrrefutableArmFirstLane), IrrefutableArmFirstLane)),
        Over("MatchWithNestedPatterns", () => MutationBases.Parsed(GenMatchPatterns.MatchWithNestedPatterns()),
            (nameof(TuplePatternExtraElement), TuplePatternExtraElement),
            (nameof(LiteralPatternOfOtherTypeLane), LiteralPatternOfOtherTypeLane)),
        Over("CovariantInterfaceProgram", () => MutationBases.Parsed(GenVariance.CovariantInterfaceProgram()),
            (nameof(UndefinedTypeAtTypeArgument), UndefinedTypeAtTypeArgument),
            (nameof(TypeArgumentArity), TypeArgumentArity)),
        Over("GenericFunctionProgram", MutationBases.GenericFunctions,
            (nameof(WrongTypedExplicitGenericArgument), WrongTypedExplicitGenericArgument),
            (nameof(UndefinedTypeAtParameter), UndefinedTypeAtParameter)),
        Over("GenericWithInheritanceProgram", MutationBases.GenericInheritance,
            (nameof(UndefinedTypeAtTypeArgument), UndefinedTypeAtTypeArgument),
            (nameof(TypeArgumentArity), TypeArgumentArity),
            (nameof(UndefinedTypeAtBase), UndefinedTypeAtBase)),
        Over("ModuleWithInterfaceHierarchy", GenInterfaces.ModuleWithInterfaceHierarchy,
            (nameof(UndefinedTypeAtBase), UndefinedTypeAtBase)),
        Over("ModuleWithGenerator", () => GenIterators.ModuleWithGenerator(valid: true),
            (nameof(WrongTypedYieldValue), WrongTypedYieldValue),
            (nameof(UndefinedTypeAtReturn), UndefinedTypeAtReturn)),
        Over("ModuleWithYieldFrom", GenIterators.ModuleWithYieldFrom,
            (nameof(YieldFromNonIterableValue), YieldFromNonIterableValue)),
        Over("ModuleWithTryExcept", () => GenExceptions.ModuleWithTryExcept(handlerCount: 1),
            (nameof(HandlerTypeNotException), HandlerTypeNotException)),
        Over("ModuleWithRaiseExpression", GenExceptions.ModuleWithRaiseExpression,
            (nameof(RaiseNonException), RaiseNonException)),
        Over("ModuleWithExceptionHierarchy", GenExceptions.ModuleWithExceptionHierarchy,
            (nameof(HandlerOrderBaseFirst), HandlerOrderBaseFirst)),
        Over("ModuleWithDecoratedFunction", () => GenDecorators.ModuleWithDecoratedFunction(validDecorator: true),
            (nameof(OverrideWithoutBaseMember), OverrideWithoutBaseMember)),
        Over("ModuleWithDataclass", GenDecorators.ModuleWithDataclass,
            (nameof(WrongTypedDataclassArgument), WrongTypedDataclassArgument),
            (nameof(DataclassArityMinusOne), DataclassArityMinusOne)),
    }.SelectMany(rows => rows).ToList();
}
