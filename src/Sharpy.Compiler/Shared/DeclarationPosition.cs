using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Where a declaration is, for a diagnostic to point at. A collision names two declarations and
/// both positions are real source locations; carrying only a line (all these walks tracked
/// before #1388) forced every consumer to render column 0. Shared by the semantic collision walks
/// (<c>CodeGenInfoComputer</c>) and the emitter's one reporting helper
/// (<c>CodeGenContext.ReportAt</c>, #2032), so both point at the same token.
/// </summary>
internal readonly record struct DeclarationPosition(int Line, int Column)
{
    /// <summary>
    /// Prefers the NAME token's position over the statement's, so the caret lands on the
    /// identifier the user has to rename rather than on the <c>def</c>/<c>class</c>/
    /// <c>property</c> keyword in front of it. Falls back to the statement when the name
    /// position is not tracked, and to nothing when neither is.
    /// </summary>
    public static DeclarationPosition? From(int nameLine, int nameColumn, int stmtLine, int stmtColumn)
        => nameLine > 0 ? new DeclarationPosition(nameLine, nameColumn)
            : stmtLine > 0 ? new DeclarationPosition(stmtLine, stmtColumn)
            : null;

    /// <summary>For nodes that carry a single position (type parameters, enum members).</summary>
    public static DeclarationPosition? From(int line, int column)
        => line > 0 ? new DeclarationPosition(line, column) : null;

    /// <summary>
    /// The position a diagnostic about <paramref name="node"/> points at: a declaration's NAME
    /// token (falling back to its statement), any other node's start.
    /// </summary>
    public static DeclarationPosition? Of(Node node) => node switch
    {
        FunctionDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        ClassDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        StructDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        InterfaceDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        EnumDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        UnionDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        DelegateDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        EventDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        PropertyDef n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        TypeAlias n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        VariableDeclaration n => From(n.NameLineStart, n.NameColumnStart, n.LineStart, n.ColumnStart),
        _ => From(node.LineStart, node.ColumnStart)
    };
}
