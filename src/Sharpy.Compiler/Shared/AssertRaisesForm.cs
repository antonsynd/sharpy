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
/// makes it work is the emitter's rewrite of <c>with assert_raises(E): body</c> into a flag, a
/// try/catch and a <c>Sharpy.AssertionError</c>.
/// </para>
///
/// <para>
/// That rewrite used to name <c>Xunit.Assert.Throws</c>, so it fired only inside a <c>@test</c>
/// function and SPY0494 refused the form anywhere else. The lowering names no test framework as of
/// #1413, so the <c>@test</c> condition is gone from every layer — leaving the SPELLING as the
/// whole predicate, which is why there is now one method here instead of two.
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
    internal static bool IsCall(FunctionCall call) => NamesTheMarker(call.Function);

    /// <summary>
    /// Whether a callee expression names the marker, through any number of redundant parentheses.
    ///
    /// <para>
    /// Separate from <see cref="IsCall(Expression?)"/> on purpose. The unwrap used to recurse into
    /// that overload, which asks whether the expression is a <em>call</em> — so
    /// <c>with (assert_raises)(ValueError):</c> answered "no", every layer skipped the rewrite, and
    /// the marker's bare name reached codegen as CS0119: the precise failure #1283 set out to make
    /// impossible, reintroduced through the parenthesized spelling. The metamorphic sweep's
    /// ParensWrapCallee transform found it once the stdlib corpus entered its scope (#1338).
    /// </para>
    /// </summary>
    private static bool NamesTheMarker(Expression? callee)
        => callee switch
        {
            Parenthesized paren => NamesTheMarker(paren.Expression),
            Identifier { Name: Name } => true,
            MemberAccess { Member: Name } => true,
            _ => false
        };

    /// <summary>
    /// Whether the emitter will rewrite this <c>with</c> away. The arity/match conditions are
    /// deliberately absent: the <c>match=</c> form is rewritten too, by a different arm that emits
    /// two flat statements. What both arms share — and what the CFG and the <c>as</c>-capture
    /// actually depend on — is the spelling, and nothing else.
    /// </summary>
    internal static bool IsRewritten(WithStatement statement)
        => statement.Items.Length == 1 && IsCall(statement.Items[0].ContextExpression);
}
