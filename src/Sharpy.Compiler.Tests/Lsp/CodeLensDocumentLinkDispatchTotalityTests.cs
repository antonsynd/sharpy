using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>CodeLensHandler.Handle</c> and <c>DocumentLinkHandler.CollectLinks</c>,
/// run through <see cref="LspDispatchTotality"/>. Code lenses count references to module-level
/// type and function declarations; document links exist only for import statements. The
/// representative justified-default kinds are probed in <c>Sharpy.Lsp.Tests.CodeLensTests</c>
/// (<c>ModuleVariable_YieldsNoLens_WhileSiblingFunctionDoes</c>) and
/// <c>Sharpy.Lsp.Tests.DocumentLinkTests</c> (<c>FunctionDef_YieldsNoLink_WhileSiblingImportDoes</c>),
/// each absence next to a positive control on the same input.
/// Mutation (worktree @ 277f54543 + this change): EnumDef arm deleted from CodeLensHandler.Handle
/// → red (1 failed, 1 passed); restored → green (2 passed).
/// </summary>
public class CodeLensDocumentLinkDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public CodeLensDocumentLinkDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // ── CodeLensHandler.Handle: module-level Statement universe ──

    private static readonly HashSet<string> CodeLensHandleArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        // Phase 2 remediation: the remaining type declarations count references like a class
        // (no lens → lens; cells in CodeLensTests).
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(TypeAlias),
    };

    private const string NoDeclaration = "CONTRACTUAL: not a declaration — nothing whose references a lens could count";

    private static readonly Dictionary<string, string> CodeLensHandleDefault = new()
    {
        [nameof(VariableDeclaration)] = "CONTRACTUAL: the lens scope is type and function declarations (handler doc); a lens per module-level variable is noise the handler declines by contract",
        [nameof(PropertyDef)] = "CONTRACTUAL: a type member — Handle walks module.Body only, and a module-level spelling is a semantic error",
        [nameof(EventDef)] = "CONTRACTUAL: a type member — Handle walks module.Body only, and a module-level spelling is a semantic error",
        [nameof(DecoratedStatement)] = "UNREACHABLE: as a declaration — the parser wraps only import / from-import / expression / assignment statements (statement-scoped @suppress, Parser.cs); a decorated definition is a FunctionDef/ClassDef/… carrying its own Decorators",
        [nameof(BreakWithFlagStatement)] = "UNREACHABLE: emitter-synthesized (never parsed)",
        [nameof(AssertStatement)] = NoDeclaration,
        [nameof(Assignment)] = NoDeclaration,
        [nameof(BreakStatement)] = NoDeclaration,
        [nameof(ContinueStatement)] = NoDeclaration,
        [nameof(DeferStatement)] = NoDeclaration,
        [nameof(ExpressionStatement)] = NoDeclaration,
        [nameof(ForStatement)] = NoDeclaration,
        [nameof(FromImportStatement)] = NoDeclaration,
        [nameof(IfStatement)] = NoDeclaration,
        [nameof(ImportStatement)] = NoDeclaration,
        [nameof(MatchStatement)] = NoDeclaration,
        [nameof(PassStatement)] = NoDeclaration,
        [nameof(RaiseStatement)] = NoDeclaration,
        [nameof(ReturnStatement)] = NoDeclaration,
        [nameof(TryStatement)] = NoDeclaration,
        [nameof(WhileStatement)] = NoDeclaration,
        [nameof(WithStatement)] = NoDeclaration,
        [nameof(YieldStatement)] = NoDeclaration,
    };

    [Fact]
    public void CodeLensHandle_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/CodeLensHandler.cs",
            "Handle",
            typeof(Statement),
            CodeLensHandleArms,
            CodeLensHandleDefault);
    }

    // ── DocumentLinkHandler.CollectLinks: Statement universe ──

    private static readonly HashSet<string> CollectLinksArms = new()
    {
        nameof(ImportStatement),
        nameof(FromImportStatement),
    };

    private static Dictionary<string, string> CollectLinksDefault()
    {
        var roster = LspDispatchTotality.UniformDefault(
            LspDispatchTotality.Universe(typeof(Statement)),
            CollectLinksArms,
            "CONTRACTUAL: document links exist only for import statements (handler doc) — no other kind names a navigable module");
        roster[nameof(DecoratedStatement)] =
            "UNREACHABLE: unwrapped through Statement.UnwrapDecorated before the switch (DocumentLinkHandler.CollectLinks), so a suppress-decorated import still links";
        roster[nameof(BreakWithFlagStatement)] = "UNREACHABLE: emitter-synthesized (never parsed)";
        return roster;
    }

    [Fact]
    public void CollectLinks_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/DocumentLinkHandler.cs",
            "CollectLinks",
            typeof(Statement),
            CollectLinksArms,
            CollectLinksDefault());
    }
}
