using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The single authority on unittest's <c>assert_raises</c> special form (#1283).
///
/// <para>
/// <c>assert_raises</c> is a MARKER, not a context manager: <c>Unittest.AssertRaises</c> throws
/// <c>NotSupportedException</c> and its <c>Dispose</c> is empty by design. The only thing that
/// makes it work is the emitter's rewrite of <c>with assert_raises(E): body</c> into
/// <c>Xunit.Assert.Throws&lt;E&gt;(() =&gt; { body })</c>, and that rewrite fires only inside a
/// <c>@test</c> function.
/// </para>
///
/// <para>
/// Three layers used to answer "is this the special form?" separately, and the three answers
/// diverged: the emitter tested name AND <c>@test</c> AND arity AND the absence of a
/// <c>match=</c> argument; the CFG builder tested the name only, so
/// <c>ControlFlowValidator</c> reasoned about a lowering that would not be emitted; and the
/// TypeChecker's <c>as</c>-capture arm tested the name only, defining the captured variable in
/// the ENCLOSING scope on the assumption that the test-only rewrite would happen. Outside a
/// <c>@test</c> function all three assumptions were false and the marker's bare name reached
/// codegen (CS0119 behind SPY0908). Every layer now asks here.
/// </para>
/// </summary>
internal static class AssertRaisesForm
{
    /// <summary>The Sharpy spelling of the marker.</summary>
    internal const string Name = "assert_raises";

    /// <summary>Whether an expression is a call to <c>assert_raises</c>, bare or qualified.</summary>
    internal static bool IsCall(Expression? expression)
        => expression is FunctionCall call && IsCall(call);

    /// <summary>Whether a call targets <c>assert_raises</c>, bare or qualified.</summary>
    internal static bool IsCall(FunctionCall call)
        => call.Function switch
        {
            Parenthesized paren => IsCall(paren.Expression),
            Identifier { Name: Name } => true,
            MemberAccess { Member: Name } => true,
            _ => false
        };

    /// <summary>
    /// Whether a <c>with</c> statement's single context manager is an <c>assert_raises</c> call.
    /// This is the SPELLING only — see <see cref="IsRewritten"/> for whether the emitter will
    /// actually lower it.
    /// </summary>
    internal static bool IsSpelling(WithStatement statement)
        => statement.Items.Length == 1 && IsCall(statement.Items[0].ContextExpression);

    /// <summary>
    /// Whether the emitter will rewrite this <c>with</c> into <c>Xunit.Assert.Throws</c>. The
    /// arity/match conditions are deliberately absent: the <c>match=</c> form is rewritten too,
    /// by a different arm that emits two flat statements. What both arms share — and what the
    /// CFG and the <c>as</c>-capture actually depend on — is the spelling plus being inside a
    /// <c>@test</c> function.
    /// </summary>
    internal static bool IsRewritten(WithStatement statement, bool inTestFunction)
        => inTestFunction && IsSpelling(statement);

    /// <summary>Whether a function's decorators register it as a test entry point.</summary>
    internal static bool IsTestFunction(IEnumerable<Decorator> decorators)
        => decorators.Any(DecoratorNames.IsTestDecorator);
}
