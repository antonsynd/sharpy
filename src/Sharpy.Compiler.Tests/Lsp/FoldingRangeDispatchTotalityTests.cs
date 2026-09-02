using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guard for <c>FoldingRangeHandler.CollectStatementRanges</c>, run through
/// <see cref="LspDispatchTotality"/>. The server folds suites (compound statements and bodied
/// declarations); a simple statement spelled over several lines is folded by the client's
/// bracket/indent folding, not by the server. The representative justified-default kind is
/// probed in <c>Sharpy.Lsp.Tests.FoldingRangeTests.MultiLineAssignment_YieldsNoRange_WhileSiblingIfDoes</c>
/// (absence next to a positive control on the same input).
/// Mutations (worktree @ 277f54543 + this change): UnionDef arm deleted from
/// CollectStatementRanges → red (1 failed, 0 passed); a phantom kind ("PhantomStatement") added
/// to the justified-default roster → red ("phantom names … PhantomStatement"); restored → green
/// (1 passed).
/// </summary>
public class FoldingRangeDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public FoldingRangeDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> CollectStatementRangesArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(IfStatement),
        nameof(ForStatement),
        nameof(WhileStatement),
        nameof(TryStatement),
        nameof(MatchStatement),
        nameof(WithStatement),
        nameof(PropertyDef),
        // Phase 2 remediation: the `defer:` block, the union body and the function-style event
        // body are suites and now fold (no range → range; cells in FoldingRangeTests).
        nameof(DeferStatement),
        nameof(UnionDef),
        nameof(EventDef),
    };

    private const string SimpleStatement =
        "CONTRACTUAL: a single statement without a suite — a multi-line spelling is folded by the client's bracket/indent folding, not by the server";

    private static readonly Dictionary<string, string> CollectStatementRangesDefault = new()
    {
        [nameof(AssertStatement)] = SimpleStatement,
        [nameof(Assignment)] = SimpleStatement,
        [nameof(ExpressionStatement)] = SimpleStatement,
        [nameof(RaiseStatement)] = SimpleStatement,
        [nameof(ReturnStatement)] = SimpleStatement,
        [nameof(VariableDeclaration)] = SimpleStatement,
        [nameof(YieldStatement)] = SimpleStatement,
        [nameof(TypeAlias)] = SimpleStatement,
        [nameof(ImportStatement)] = SimpleStatement,
        [nameof(FromImportStatement)] = SimpleStatement,
        [nameof(DelegateDef)] = "CONTRACTUAL: a signature-only declaration with no suite (the parameter list is folded client-side like any bracket)",
        [nameof(BreakStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(ContinueStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(PassStatement)] = "CONTRACTUAL: keyword-only statement",
        [nameof(DecoratedStatement)] = "UNREACHABLE: as a suite — the parser wraps only import / from-import / expression / assignment statements (statement-scoped @suppress, Parser.cs), none of which fold",
        [nameof(BreakWithFlagStatement)] = "UNREACHABLE: emitter-synthesized (never parsed)",
    };

    [Fact]
    public void CollectStatementRanges_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/FoldingRangeHandler.cs",
            "CollectStatementRanges",
            typeof(Statement),
            CollectStatementRangesArms,
            CollectStatementRangesDefault);
    }
}
