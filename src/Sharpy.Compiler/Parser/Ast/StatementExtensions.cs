namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Extension methods for <see cref="Statement"/> nodes used by scan sites that walk
/// <c>module.Body</c> and type-test the statements they find.
/// </summary>
public static class StatementExtensions
{
    /// <summary>
    /// Returns the statement carried by a <see cref="DecoratedStatement"/> wrapper, or the
    /// statement itself when it is not decorated. Single-level by construction: the parser never
    /// nests a <see cref="DecoratedStatement"/> inside another, so a single unwrap is exhaustive.
    /// </summary>
    /// <remarks>
    /// Every <c>module.Body</c> scan that type-tests for a concrete statement kind (e.g.
    /// <see cref="ImportStatement"/>, <see cref="FromImportStatement"/>) must call this first so a
    /// suppress-decorated statement is classified by its inner target rather than silently skipped
    /// (#1124). A conformance test in <c>CompilerInvariants</c> guards this contract.
    /// </remarks>
    public static Statement UnwrapDecorated(this Statement stmt)
        => stmt is DecoratedStatement decorated ? decorated.Statement : stmt;
}
