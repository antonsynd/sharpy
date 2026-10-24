using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The mutation-generator lanes (#2324, P63b Phase 2): each member wraps one <see cref="MutationLanes"/>
/// break with the ONE code its programs must draw (measured by <see cref="MutationCensus"/> at 600 samples
/// before the member was written) — the break carries no code, the member carries exactly one, so the
/// roster's link reads the member's single literal. A pin names the issue that owns the missing refusal
/// and what happens instead.
/// </summary>
internal static partial class ErrorInjector
{
    private static InjectionResult? Lane(Module? mutated, string code) =>
        mutated is null ? null : new InjectionResult(mutated, code);

    // ---- operand positions -------------------------------------------------------------------------

    /// <summary>An arithmetic operand (builtin or user type) replaced by a literal its type refuses.</summary>
    public static InjectionResult? InjectWrongTypedArithmeticOperand(Module module) =>
        Lane(MutationLanes.WrongTypedArithmeticOperand(module), "SPY0222");

    /// <summary>A comparison operand (ordering or equality) replaced by a literal of another type.</summary>
    public static InjectionResult? InjectWrongTypedComparisonOperand(Module module) =>
        Lane(MutationLanes.WrongTypedComparisonOperand(module), "SPY0222");

    public static InjectionResult? InjectWrongTypedUnaryOperand(Module module) =>
        Lane(MutationLanes.WrongTypedUnaryOperand(module), "SPY0223");

    /// <summary>An <c>and</c>/<c>or</c> operand replaced by a fixed tuple, which has no falsy case.</summary>
    public static InjectionResult? InjectNoFalsyCaseLogicalOperand(Module module) =>
        Lane(MutationLanes.NoFalsyCaseLogicalOperand(module), "SPY0220");

    /// <summary>An <c>if</c>/<c>while</c>/conditional test replaced by a fixed tuple.</summary>
    public static InjectionResult? InjectNoFalsyCaseTruthTest(Module module) =>
        Lane(MutationLanes.NoFalsyCaseTruthTest(module), "SPY0220");

    /// <summary>A case guard replaced by a fixed tuple (a guard has its own code, measured: not SPY0220).</summary>
    public static InjectionResult? InjectNoFalsyCaseGuard(Module module) =>
        Lane(MutationLanes.NoFalsyCaseGuard(module), "SPY0241");

    /// <summary>One branch of a conditional in a typed slot replaced by a literal the slot refuses.</summary>
    public static InjectionResult? InjectMismatchedConditionalBranch(Module module) =>
        Lane(MutationLanes.MismatchedConditionalBranch(module), "SPY0220");

    public static InjectionResult? InjectWrongTypedIndex(Module module) =>
        Lane(MutationLanes.WrongTypedIndex(module), "SPY0220");

    /// <summary>A numeric format spec over a hole that is a str.</summary>
    public static InjectionResult? InjectStrHoleUnderNumericSpec(Module module) =>
        Lane(MutationLanes.StrHoleUnderNumericSpec(module), "SPY0609");

    // ---- names ------------------------------------------------------------------------------------

    public static InjectionResult? InjectUndefinedNameInFStringHole(Module module) =>
        Lane(MutationLanes.UndefinedNameInFStringHole(module), "SPY0200");

    public static InjectionResult? InjectUndefinedNameAtWithTarget(Module module) =>
        Lane(MutationLanes.UndefinedNameAtWithTarget(module), "SPY0200");

    // ---- arguments and calls ----------------------------------------------------------------------

    /// <summary>An argument of an overloaded builtin (<c>range</c>, <c>abs</c>, <c>int</c>) no overload takes.</summary>
    public static InjectionResult? InjectOverloadedBuiltinWrongArgument(Module module) =>
        Lane(MutationLanes.WrongTypedOverloadedBuiltinArgument(module), "SPY0354");

    public static InjectionResult? InjectWrongTypedExplicitGenericArgument(Module module) =>
        Lane(MutationLanes.WrongTypedExplicitGenericArgument(module), "SPY0220");

    /// <summary><c>super().__init__(…)</c> with one argument more than the base constructor takes.</summary>
    public static InjectionResult? InjectSuperInitArity(Module module) =>
        Lane(MutationLanes.SuperInitArityPlusOne(module), "SPY0224");

    // ---- member names -----------------------------------------------------------------------------

    public static InjectionResult? InjectUndefinedMember(Module module) =>
        Lane(MutationLanes.UndefinedMember(module), "SPY0203");

    public static InjectionResult? InjectUndefinedMethod(Module module) =>
        Lane(MutationLanes.UndefinedMethod(module), "SPY0203");

    public static InjectionResult? InjectUndefinedField(Module module) =>
        Lane(MutationLanes.UndefinedField(module), "SPY0203");

    /// <summary>An operator a user type implements called by its dunder name.</summary>
    public static InjectionResult? InjectDunderCalledByName(Module module) =>
        Lane(MutationLanes.DunderCalledByName(module), "SPY0427");

    /// <summary>A comparison dunder declared <c>-> int</c>.</summary>
    public static InjectionResult? InjectDunderWrongReturnType(Module module) =>
        Lane(MutationLanes.DunderWrongReturnType(module), "SPY0321");

    // ---- annotations ------------------------------------------------------------------------------

    public static InjectionResult? InjectUndefinedTypeAtParameter(Module module) =>
        Lane(MutationLanes.UndefinedTypeAtParameter(module), "SPY0202");

    public static InjectionResult? InjectUndefinedTypeAtBase(Module module) =>
        Lane(MutationLanes.UndefinedTypeAtBase(module), "SPY0202");

    public static InjectionResult? InjectUndefinedTypeAtTypeArgument(Module module) =>
        Lane(MutationLanes.UndefinedTypeAtTypeArgument(module), "SPY0202");

    public static InjectionResult? InjectUndefinedTypeAtPatternHead(Module module) =>
        Lane(MutationLanes.UndefinedTypeAtPatternHead(module), "SPY0202");

    public static InjectionResult? InjectUndefinedTypeAtReturn(Module module) =>
        Lane(MutationLanes.UndefinedTypeAtReturn(module), "SPY0202");

    /// <summary>A generic annotation (base, declaration) with one type argument too many.</summary>
    public static InjectionResult? InjectTypeArgumentArity(Module module) =>
        Lane(MutationLanes.TypeArgumentArity(module), "SPY0224");

    // ---- stores -----------------------------------------------------------------------------------

    public static InjectionResult? InjectWrongTypedReassignment(Module module) =>
        Lane(MutationLanes.WrongTypedReassignment(module), "SPY0220");

    /// <summary>A <c>list[T]</c> declaration initialized with a list of an element its annotation refuses.</summary>
    public static InjectionResult? InjectWrongTypedListInitializer(Module module) =>
        Lane(MutationLanes.WrongTypedListInitializer(module), "SPY0220");

    // ---- return values ----------------------------------------------------------------------------

    public static InjectionResult? InjectWrongTypedReturnValue(Module module) =>
        Lane(MutationLanes.WrongTypedReturn(module), "SPY0260");

    public static InjectionResult? InjectReturnValueInImplicitNoneDef(Module module) =>
        Lane(MutationLanes.ReturnValueInImplicitNoneDef(module), "SPY0260");

    public static InjectionResult? InjectWrongTypedYield(Module module) =>
        Lane(MutationLanes.WrongTypedYieldValue(module), "SPY0220");

    public static InjectionResult? InjectYieldFromNonIterable(Module module) =>
        Lane(MutationLanes.YieldFromNonIterableValue(module), "SPY0220");

    // ---- patterns ---------------------------------------------------------------------------------

    public static InjectionResult? InjectLiteralPatternOfOtherType(Module module) =>
        Lane(MutationLanes.LiteralPatternOfOtherTypeLane(module), "SPY0220");

    public static InjectionResult? InjectOrPatternBindingMismatch(Module module) =>
        Lane(MutationLanes.OrPatternBindingMismatchLane(module), "SPY0359");

    public static InjectionResult? InjectAndPatternDuplicateCapture(Module module) =>
        Lane(MutationLanes.AndPatternDuplicateCaptureLane(module), "SPY0372");

    public static InjectionResult? InjectTuplePatternExtraElement(Module module) =>
        Lane(MutationLanes.TuplePatternExtraElement(module), "SPY0257");

    public static InjectionResult? InjectIrrefutableArmFirst(Module module) =>
        Lane(MutationLanes.IrrefutableArmFirstLane(module), "SPY0700");

    public static InjectionResult? InjectListPatternElementOfOtherType(Module module) =>
        Lane(MutationLanes.ListPatternElementOfOtherTypeLane(module), "SPY0220");

    // ---- declaration shapes -----------------------------------------------------------------------

    public static InjectionResult? InjectHandlerTypeNotException(Module module) =>
        Lane(MutationLanes.HandlerTypeNotException(module), "SPY0399");

    public static InjectionResult? InjectRaiseNonException(Module module) =>
        Lane(MutationLanes.RaiseNonException(module), "SPY0242");

    public static InjectionResult? InjectExitArity(Module module) =>
        Lane(MutationLanes.ExitArity(module), "SPY0320");

    public static InjectionResult? InjectAsyncWithOverSyncManager(Module module) =>
        Lane(MutationLanes.AsyncWithOverSyncManager(module), "SPY0324");

    public static InjectionResult? InjectOverrideWithoutBaseMember(Module module) =>
        Lane(MutationLanes.OverrideWithoutBaseMember(module), "SPY0248");

    /// <summary>A local renamed camelCase — the one WARNING lane (SPY0453 at warning severity).</summary>
    public static InjectionResult? InjectCamelCaseLocal(Module module) =>
        MutationLanes.CamelCaseLocal(module) is { } mutated
            ? new InjectionResult(mutated, "SPY0453", Severity: CompilerDiagnosticSeverity.Warning)
            : null;

    // ---- pins: refusals the compiler does not make --------------------------------------------------

    /// <summary><c>def main(arg: int)</c>: Sharpy accepts it and Roslyn refuses the emitted entry point.</summary>
    public static InjectionResult? InjectMainParameter(Module module) =>
        MutationLanes.MainParameter(module) is { } mutated
            ? new InjectionResult(mutated, null, new KnownRedRefusal("#2335",
                "analyses clean; run is SPY0908 CS1558 — main's parameter list is left to Roslyn"))
            : null;

    /// <summary>A <c>try</c> whose handlers go subclass → base reordered base → subclass: the subclass
    /// handler is unreachable, Sharpy accepts it and Roslyn refuses it.</summary>
    public static InjectionResult? InjectHandlerOrderBaseFirst(Module module) =>
        MutationLanes.HandlerOrderBaseFirst(module) is { } mutated
            ? new InjectionResult(mutated, null, new KnownRedRefusal("#2363",
                "analyses clean; run is SPY0908 CS0160 — handler reachability is left to Roslyn"))
            : null;

    /// <summary>A <c>@dataclass</c> constructed with an argument its field type refuses: the synthesized
    /// constructor has no semantic signature.</summary>
    public static InjectionResult? InjectDataclassWrongArgument(Module module) =>
        MutationLanes.WrongTypedDataclassArgument(module) is { } mutated
            ? new InjectionResult(mutated, null, new KnownRedRefusal("#2021",
                "analyses clean; run is SPY0908 CS1503 — the @dataclass constructor has no semantic signature"))
            : null;

    // ---- the multi-file lane ------------------------------------------------------------------------

    /// <summary>A multi-file injection: the files and the ONE code the project must draw.</summary>
    public sealed record ProjectInjectionResult(IReadOnlyList<(string Path, string Content)> Files, string ExpectedDiagnosticCode);

    /// <summary>The library's module-level typed constant stores a literal its annotation refuses.</summary>
    public static ProjectInjectionResult? InjectWrongTypedLibraryConstant((string MainSource, string LibSource) project) =>
        MutationLanes.WrongTypedLibraryConstant(project) is { } mutated
            ? new ProjectInjectionResult(new[] { ("main.spy", mutated.MainSource), ("lib.spy", mutated.LibSource) }, "SPY0220")
            : null;

    /// <summary>The multi-file face of <see cref="Judge(InjectionResult, string)"/>: judges the project
    /// through <see cref="JudgeProject"/> at error severity.</summary>
    public static string? Judge(ProjectInjectionResult injection, string label) =>
        JudgeProject(injection.Files, injection.ExpectedDiagnosticCode, CompilerDiagnosticSeverity.Error, null, label);
}
