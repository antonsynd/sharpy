using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>InlayHintHandler</c>'s two dispatch switches, run through
/// <see cref="LspDispatchTotality"/>. <c>CollectInlayHints</c> hints declaring bindings and
/// collects call hints from every Expression child of every statement BEFORE its switch; the
/// switch itself recurses into suites and manages binding scope. <c>MarkPatternBound</c>
/// records the names a match pattern captures; kinds that bind no name are skipped, and the
/// binding classification is cross-checked against the CFG builder's own pattern-binding
/// switch. The representative justified-default kinds are probed in
/// <c>Sharpy.Lsp.Tests.InlayHintTests</c> (<c>InterfaceBody_YieldsNoHint_WhileSiblingFunctionDoes</c>,
/// <c>WildcardPattern_BindsNothing_WhileSiblingCaptureDoes</c>), each absence next to a
/// positive control on the same input.
/// Mutation (worktree @ 277f54543 + this change): GuardPattern arm deleted from MarkPatternBound
/// → red (1 failed, 2 passed); restored → green (3 passed).
/// </summary>
public class InlayHintDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public InlayHintDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // ── CollectInlayHints: Statement universe (31) ──

    private static readonly HashSet<string> CollectInlayHintsArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(MatchStatement),
        // Phase 2 remediation: bodied declarations and the deferred suite are recursed like a
        // class/function body (no hint → hint; cells in InlayHintTests).
        nameof(PropertyDef),
        nameof(UnionDef),
        nameof(EventDef),
        nameof(DeferStatement),
    };

    private const string CallHintsFromChildren =
        "PRE-SWITCH: call hints are collected from every Expression child of the statement (the GetChildNodes walk above the switch); no suite to recurse";

    private static readonly Dictionary<string, string> CollectInlayHintsDefault = new()
    {
        [nameof(VariableDeclaration)] = "PRE-SWITCH: the declaring-binding type hint and the initializer's call hints come from the if-chain above the switch",
        [nameof(Assignment)] = "PRE-SWITCH: the declaring-binding type hint and the value's call hints come from the if-chain above the switch",
        [nameof(ExpressionStatement)] = CallHintsFromChildren,
        [nameof(ReturnStatement)] = CallHintsFromChildren,
        [nameof(AssertStatement)] = CallHintsFromChildren,
        [nameof(RaiseStatement)] = CallHintsFromChildren,
        [nameof(YieldStatement)] = CallHintsFromChildren,
        [nameof(DelegateDef)] = "PRE-SWITCH: parameter defaults are its only Expression children (call hints via the walk above the switch); a signature-only declaration has no suite",
        [nameof(DecoratedStatement)] = "UNREACHABLE: unwrapped at the top of the statement loop (InlayHintHandler.CollectInlayHints) before the if-chain and the switch",
        [nameof(InterfaceDef)] = "CONTRACTUAL: an interface body declares signatures — nothing binds a name or calls",
        [nameof(EnumDef)] = "CONTRACTUAL: enum members are constant literals — nothing binds by inference or calls",
        [nameof(TypeAlias)] = "CONTRACTUAL: a type-level declaration with no expression children",
        [nameof(ImportStatement)] = "CONTRACTUAL: no expression children and no inferred binding",
        [nameof(FromImportStatement)] = "CONTRACTUAL: no expression children and no inferred binding",
        [nameof(BreakStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(ContinueStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(PassStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(BreakWithFlagStatement)] = "UNREACHABLE: emitter-synthesized (never parsed)",
    };

    [Fact]
    public void CollectInlayHints_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/InlayHintHandler.cs",
            "CollectInlayHints",
            typeof(Statement),
            CollectInlayHintsArms,
            CollectInlayHintsDefault);
    }

    // ── MarkPatternBound: Pattern universe (16) ──

    private static readonly HashSet<string> MarkPatternBoundArms = new()
    {
        nameof(BindingPattern),
        nameof(StarPattern),
        nameof(TuplePattern),
        nameof(ListPattern),
        nameof(PositionalPattern),
        nameof(PropertyPattern),
        nameof(UnionCasePattern),
        nameof(OrPattern),
        nameof(AndPattern),
        nameof(AsPattern),
        nameof(GuardPattern),
    };

    private const string BindsNoName =
        "CONTRACTUAL: binds no name (the CFG builder's pattern-binding switch agrees — see MarkPatternBound_Arms_EqualTheCfgBindingClassification)";

    private static readonly Dictionary<string, string> MarkPatternBoundDefault = new()
    {
        [nameof(LiteralPattern)] = BindsNoName,
        [nameof(MemberAccessPattern)] = BindsNoName,
        [nameof(RelationalPattern)] = BindsNoName,
        [nameof(TypePattern)] = BindsNoName,
        [nameof(WildcardPattern)] = BindsNoName,
    };

    [Fact]
    public void MarkPatternBound_ArmsPlusJustifiedDefaults_EqualThePatternUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/InlayHintHandler.cs",
            "MarkPatternBound",
            typeof(Pattern),
            MarkPatternBoundArms,
            MarkPatternBoundDefault);
    }

    /// <summary>
    /// Cross-check required by plan-950124 Phase 2: the LSP's "which patterns bind" answer must
    /// be the compiler's. <c>ControlFlowGraphBuilder.CollectPatternBindingKeysInto</c> is the
    /// production classifier that <c>CfgPatternBindingTotalityTests</c> pins to its
    /// <c>Binding</c> set; reading it here (rather than copying the set) keeps one source.
    /// </summary>
    [Fact]
    public void MarkPatternBound_Arms_EqualTheCfgBindingClassification()
    {
        var cfgBinding = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "CollectPatternBindingKeysInto");
        Assert.NotEmpty(cfgBinding);
        _output.WriteLine($"CFG binding kinds ({cfgBinding.Count}): {string.Join(", ", cfgBinding.OrderBy(a => a))}");
        Assert.True(MarkPatternBoundArms.SetEquals(cfgBinding),
            "InlayHintHandler.MarkPatternBound and ControlFlowGraphBuilder.CollectPatternBindingKeysInto disagree on which patterns bind.\n" +
            $"  LSP-only: {string.Join(", ", MarkPatternBoundArms.Except(cfgBinding))}\n" +
            $"  CFG-only: {string.Join(", ", cfgBinding.Except(MarkPatternBoundArms))}");
    }
}
