using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Classifies every statement written directly in a type body — a class, struct, interface or
/// union — through ONE classifier, <see cref="ClassifyTypeBodyStatement"/>, and refuses the ones
/// that are not allowed there.
///
/// <para>Today the only refused shape is <c>let</c> (<c>let x = e</c>, <c>let a, b = e</c>,
/// <c>let x: T = e</c>): a <c>let</c> binding is local by definition, so in a type body it is
/// SPY0340 with the field steer (#1974, P21a). This is the type-body twin of the module-level
/// <c>let</c> arm in <see cref="ModuleLevelValidator"/>. <c>NameResolver</c>'s class and struct
/// loops skip a <c>let</c> <see cref="VariableDeclaration"/> so a refused <c>let</c> never
/// registers a field.</para>
///
/// <para>Every other statement falls through untouched. This classifier is deliberately the seam
/// where the remaining non-member statements of a type body (an executable statement, a bare
/// assignment, control flow — today an SPY0510 code-generation ICE) will be refused by adding
/// arms here, never at the emitter.</para>
///
/// <para>Nested hosts (a class inside a class, a class inside a method) are reached because each
/// override classifies its own body and then descends through the base traversal. The union host is
/// classified for totality, but today the union grammar admits only <c>case</c>, <c>def</c> and
/// <c>pass</c>, so a union-body <c>let</c> is refused by the parser before this validator runs.</para>
/// </summary>
internal sealed class TypeBodyStatementValidator : ValidatingAstWalker
{
    public override string Name => "TypeBodyStatementValidator";
    public override int Order => 51; // Beside ModuleLevelValidator (50), before CircularImportUsageValidator (52)

    public override void VisitClassDef(ClassDef node)
    {
        ClassifyTypeBody(node.Body, "class");
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        ClassifyTypeBody(node.Body, "struct");
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        ClassifyTypeBody(node.Body, "interface");
        base.VisitInterfaceDef(node);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        ClassifyTypeBody(node.Body, "union");
        base.VisitUnionDef(node);
    }

    private void ClassifyTypeBody(ImmutableArray<Statement> body, string hostKind)
    {
        foreach (var statement in body)
            ClassifyTypeBodyStatement(statement, hostKind);
    }

    /// <summary>
    /// The one decision for a statement written directly in a type body of kind
    /// <paramref name="hostKind"/> (<c>class</c>, <c>struct</c>, <c>interface</c> or <c>union</c>).
    /// A statement-scoped <c>@suppress</c> wrapper is transparent: the inner statement is classified.
    /// </summary>
    private void ClassifyTypeBodyStatement(Statement statement, string hostKind)
    {
        var stmt = statement.UnwrapDecorated();
        switch (stmt)
        {
            case Assignment { IsLet: true }:
            case VariableDeclaration { IsLet: true }:
                AddError(
                    TypeBodyLetMessage(hostKind),
                    line: stmt.LineStart,
                    column: stmt.ColumnStart,
                    code: DiagnosticCodes.Semantic.ModuleLevelExecutableStatement,
                    span: stmt.Span);
                break;

            default:
                // Members, nested declarations, and (for now) every other statement fall through
                // untouched — see the class summary.
                break;
        }
    }

    /// <summary>The SPY0340 text for a <c>let</c> in a type body — the field steer (#1974).</summary>
    internal static string TypeBodyLetMessage(string hostKind)
        => $"`let` is not allowed in {(hostKind == "interface" ? "an" : "a")} {hostKind} body; "
           + "declare a field as 'x: T = ...' "
           + "or a constant as 'const X = ...'";
}
