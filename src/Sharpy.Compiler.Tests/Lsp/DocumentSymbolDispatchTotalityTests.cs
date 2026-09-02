using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>DocumentSymbolHandler</c>'s two dispatch switches, run through
/// <see cref="LspDispatchTotality"/>. The outline lists named declarations; a statement that
/// declares no name gets no entry. The representative justified-default kind is probed in
/// <c>Sharpy.Lsp.Tests.DocumentSymbolTests.ExpressionStatement_YieldsNoSymbol_WhileSiblingFunctionDoes</c>
/// (absence next to a positive control on the same input); the declaring-assignment gap is
/// #1734 and is rostered as a MISS rather than papered over.
/// Mutation (worktree @ 277f54543 + this change): DelegateDef arm deleted from ConvertStatement
/// → red (1 failed, 1 passed); restored → green (2 passed).
/// </summary>
public class DocumentSymbolDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public DocumentSymbolDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private const string NoDeclaredName = "CONTRACTUAL: declares no name — nothing for the outline to list";
    private const string KeywordOnly = "CONTRACTUAL: keyword-only statement — declares no name";
    private const string Synthesized = "UNREACHABLE: emitter-synthesized (never parsed)";
    private const string DecoratedWrapper =
        "UNREACHABLE: as a declaration — the parser wraps only import / from-import / expression / assignment statements (statement-scoped @suppress, Parser.cs); the wrapped assignment's entry belongs to the Assignment row (#1734)";
    private const string DeclaringAssignment =
        "MISS #1734: a declaring `x = value` binding gets no outline entry (needs first-binding tracking, not a one-arm delegation)";

    // ── ConvertStatement: module-level Statement universe ──

    private static readonly HashSet<string> ConvertStatementArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(TypeAlias),
        // Phase 2 remediation: union (with its cases and methods as children) and delegate
        // declarations join the outline (no symbol → symbol; cells in DocumentSymbolTests).
        nameof(UnionDef),
        nameof(DelegateDef),
    };

    private static readonly Dictionary<string, string> ConvertStatementDefault = new()
    {
        [nameof(Assignment)] = DeclaringAssignment,
        [nameof(DecoratedStatement)] = DecoratedWrapper,
        [nameof(PropertyDef)] = "CONTRACTUAL: a type member — a module-level spelling is a semantic error; ConvertClassMember lists it inside its type",
        [nameof(EventDef)] = "CONTRACTUAL: a type member — a module-level spelling is a semantic error; ConvertClassMember lists it inside its type",
        [nameof(ImportStatement)] = "CONTRACTUAL: imports are not outline entries (no declaration of the file's own)",
        [nameof(FromImportStatement)] = "CONTRACTUAL: imports are not outline entries (no declaration of the file's own)",
        [nameof(AssertStatement)] = NoDeclaredName,
        [nameof(DeferStatement)] = NoDeclaredName,
        [nameof(ExpressionStatement)] = NoDeclaredName,
        [nameof(ForStatement)] = NoDeclaredName,
        [nameof(IfStatement)] = NoDeclaredName,
        [nameof(MatchStatement)] = NoDeclaredName,
        [nameof(RaiseStatement)] = NoDeclaredName,
        [nameof(ReturnStatement)] = NoDeclaredName,
        [nameof(TryStatement)] = NoDeclaredName,
        [nameof(WhileStatement)] = NoDeclaredName,
        [nameof(WithStatement)] = NoDeclaredName,
        [nameof(YieldStatement)] = NoDeclaredName,
        [nameof(BreakStatement)] = KeywordOnly,
        [nameof(ContinueStatement)] = KeywordOnly,
        [nameof(PassStatement)] = KeywordOnly,
        [nameof(BreakWithFlagStatement)] = Synthesized,
    };

    [Fact]
    public void ConvertStatement_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs",
            "ConvertStatement",
            typeof(Statement),
            ConvertStatementArms,
            ConvertStatementDefault);
    }

    // ── ConvertClassMember: type-body Statement universe ──

    private static readonly HashSet<string> ConvertClassMemberArms = new()
    {
        nameof(FunctionDef),
        nameof(PropertyDef),
        nameof(EventDef),
        nameof(VariableDeclaration),
        // Phase 2 remediation: nested type declarations and aliases delegate to ConvertStatement
        // so the outline nests them (no child → child; cell in DocumentSymbolTests).
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(TypeAlias),
    };

    private static readonly Dictionary<string, string> ConvertClassMemberDefault = new()
    {
        [nameof(Assignment)] = DeclaringAssignment,
        [nameof(DecoratedStatement)] = DecoratedWrapper,
        [nameof(ImportStatement)] = "CONTRACTUAL: not a member declaration",
        [nameof(FromImportStatement)] = "CONTRACTUAL: not a member declaration",
        [nameof(AssertStatement)] = NoDeclaredName,
        [nameof(DeferStatement)] = NoDeclaredName,
        [nameof(ExpressionStatement)] = NoDeclaredName,
        [nameof(ForStatement)] = NoDeclaredName,
        [nameof(IfStatement)] = NoDeclaredName,
        [nameof(MatchStatement)] = NoDeclaredName,
        [nameof(RaiseStatement)] = NoDeclaredName,
        [nameof(ReturnStatement)] = NoDeclaredName,
        [nameof(TryStatement)] = NoDeclaredName,
        [nameof(WhileStatement)] = NoDeclaredName,
        [nameof(WithStatement)] = NoDeclaredName,
        [nameof(YieldStatement)] = NoDeclaredName,
        [nameof(BreakStatement)] = KeywordOnly,
        [nameof(ContinueStatement)] = KeywordOnly,
        [nameof(PassStatement)] = KeywordOnly,
        [nameof(BreakWithFlagStatement)] = Synthesized,
    };

    [Fact]
    public void ConvertClassMember_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs",
            "ConvertClassMember",
            typeof(Statement),
            ConvertClassMemberArms,
            ConvertClassMemberDefault);
    }
}
