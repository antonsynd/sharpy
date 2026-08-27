using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Refuses two distinct local spellings in one function that compile to the same C# local name
/// (SPY0522, #1276).
/// </summary>
/// <remarks>
/// <para>
/// The emitter keys its local-variable slots on the name a binding is EMITTED under, and an
/// unescaped local is emitted camelCased — so bare <c>zed</c> and bare <c>Zed</c> share one slot:
/// the second binding was emitted as a plain assignment into the first's storage and the program
/// printed <c>7 7</c> where CPython prints <c>6 7</c>. Only SPY0453 fired — a style warning standing
/// in for a correctness refusal.
/// </para>
/// <para>
/// This extends to function bodies the rule the module level and the class member space already
/// enforce: distinct source spellings whose emitted names collide in one C# declaration space are
/// refused. Deterministic renaming was rejected for the other spaces because it silently changes
/// interop-visible names; locals are not interop-visible, but consistency of the refusal is worth
/// more than the convenience, and the backtick escape is the sanctioned way to keep both spellings.
/// </para>
/// <para>
/// The escape is a real remedy here since #1357: a backtick-escaped local is emitted VERBATIM, so
/// <c>`Zed`</c> comes out as <c>Zed</c> and no longer collides with <c>zed</c>. That is why this
/// check keys on <see cref="NameCasing.ResolveVariable"/> — the same function the emitter resolves
/// the binding through — rather than on <c>ToCamelCase</c>. Keying on the mangled name regardless
/// would refuse a pair that now compiles; keying on the source spelling would miss the pair that
/// still collides.
/// </para>
/// <para>
/// Same-spelling rebinding is exempt by construction — the check compares distinct spellings only,
/// and the emitter's version machinery (<c>x</c>, <c>x_1</c>) exists for exactly that case.
/// </para>
/// <para>
/// The scope is the whole function body rather than the C# block that contains each binding: the
/// emitter's slot table is flat per function, so two bindings in disjoint blocks collide there just
/// the same. Nested functions get their own scope, as they do in the emitter.
/// </para>
/// </remarks>
internal sealed class LocalNameCollisionValidator : ValidatingAstWalker
{
    public override string Name => "LocalNameCollision";

    // After the naming-advice validators (55, 56, 57), which is where a reader looking at a
    // collision would expect the style warning to have already been said, and before DecoratorValidator (60).
    public override int Order => 58;

    /// <summary>A binding's source spelling and the position to report it at.</summary>
    private readonly record struct Binding(string Spelling, int Line, int Column);

    /// <summary>
    /// Emitted name → the first binding that claimed it, for the function scope being walked.
    /// Null outside any function: module-level collisions are the CodeGenInfoComputer's space.
    /// </summary>
    private Dictionary<string, Binding>? _scope;

    public override void VisitFunctionDef(FunctionDef node)
    {
        // Each function is its own slot table, including a nested one, so save and restore rather
        // than sharing the enclosing scope's map.
        var enclosing = _scope;
        _scope = new Dictionary<string, Binding>(StringComparer.Ordinal);

        foreach (var parameter in node.Parameters)
        {
            Declare(parameter.Name, parameter.IsNameBacktickEscaped, parameter.LineStart, parameter.ColumnStart);
        }

        base.VisitFunctionDef(node);

        _scope = enclosing;
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        Declare(node.Name, node.IsNameBacktickEscaped, node.NameLineStart, node.NameColumnStart);
        base.VisitVariableDeclaration(node);
    }

    public override void VisitAssignment(Assignment node)
    {
        if (node.Operator == AssignmentOperator.Assign)
        {
            // Every shape a target can take, not just a bare name: `Zed, other = (7, 8)` binds
            // `Zed` exactly as `Zed = 7` does, and the emitter gives it the same mangled slot.
            DeclareTarget(node.Target);
        }

        base.VisitAssignment(node);
    }

    public override void VisitForStatement(ForStatement node)
    {
        DeclareTarget(node.Target);
        base.VisitForStatement(node);
    }

    public override void VisitForClause(ForClause node)
    {
        DeclareTarget(node.Target);
        base.VisitForClause(node);
    }

    public override void VisitWithStatement(WithStatement node)
    {
        foreach (var item in node.Items)
        {
            if (item.Name != null)
            {
                Declare(item.Name, item.IsNameBacktickEscaped, item.NameLineStart, item.NameColumnStart);
            }
        }

        base.VisitWithStatement(node);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        foreach (var handler in node.Handlers)
        {
            if (handler.Name != null)
            {
                Declare(handler.Name, handler.IsNameBacktickEscaped, handler.NameLineStart, handler.NameColumnStart);
            }
        }

        base.VisitTryStatement(node);
    }

    public override void VisitWalrusExpression(WalrusExpression node)
    {
        Declare(node.Target, node.IsNameBacktickEscaped, node.LineStart, node.ColumnStart);
        base.VisitWalrusExpression(node);
    }

    public override void VisitBindingPattern(BindingPattern node)
    {
        // A bare name in a case label captures the scrutinee into a local — unless the TypeChecker
        // resolved it to a module-level constant (RFC 3535) or a union variant (#1562).
        if (Context.SemanticInfo.GetPatternConstantSymbol(node) == null
            && Context.SemanticInfo.GetPatternUnionCase(node) == null)
        {
            Declare(node.Name.Name, node.Name.IsNameBacktickEscaped, node.Name.LineStart, node.Name.ColumnStart);
        }

        base.VisitBindingPattern(node);
    }

    public override void VisitTypePattern(TypePattern node)
    {
        if (node.BindingName != null)
        {
            Declare(node.BindingName.Name, node.BindingName.IsNameBacktickEscaped,
                node.BindingName.LineStart, node.BindingName.ColumnStart);
        }

        base.VisitTypePattern(node);
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        // A lambda parameter is a local of the enclosing C# body, but a same-spelling collision
        // with another local is VERSIONED by the allocator (#1647), not refused, so it does not
        // enter the collision map. Only the reserved-prefix rule applies to it.
        foreach (var parameter in node.Parameters)
        {
            RefuseReservedEscapedPrefix(parameter.Name, parameter.IsNameBacktickEscaped,
                parameter.LineStart, parameter.ColumnStart);
        }

        base.VisitLambdaExpression(node);
    }

    public override void VisitModifiedArgument(ModifiedArgument node)
    {
        // An inline out declaration (`try_parse("42", out value: int)`) introduces a local at the
        // call site, and the emitter gives it a slot from the same table as every other local.
        if (node.InlineName != null)
        {
            Declare(node.InlineName, node.IsNameBacktickEscaped,
                node.Argument.LineStart, node.Argument.ColumnStart);
        }

        base.VisitModifiedArgument(node);
    }

    /// <summary>Declares whatever names a binding target spells, ignoring non-binding shapes.</summary>
    private void DeclareTarget(Expression? target)
    {
        switch (target)
        {
            case Identifier id:
                Declare(id.Name, id.IsNameBacktickEscaped, id.LineStart, id.ColumnStart);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                {
                    DeclareTarget(element);
                }
                break;
            case StarExpression star:
                // `a, *rest = ...` binds `rest`; the star only says how much it takes.
                DeclareTarget(star.Operand);
                break;
        }
    }

    /// <summary>
    /// Refuses a backtick-escaped local whose spelling starts with <c>__</c> (SPY0522, #1560 D1 §3).
    /// An escaped local is emitted verbatim, and <c>__{prefix}_{n}</c> is the shape of every
    /// temporary the emitter declares in the same C# body (<c>__lambda_0</c>, <c>__loopVar_1</c>,
    /// <c>__spread_2</c>, …), so such a local can collide with a temporary that no source line
    /// names. The unescaped spelling is not affected: <c>__t</c> camelCases to <c>t</c>.
    /// </summary>
    /// <returns>True when the binding was refused.</returns>
    private bool RefuseReservedEscapedPrefix(string spelling, bool isEscaped, int line, int column)
    {
        if (_scope == null || !isEscaped || !spelling.StartsWith("__", StringComparison.Ordinal))
        {
            return false;
        }

        AddError(
            $"Escaped local '{spelling}' is reserved: a name starting with '__' is the compiler's own "
            + "temporary-name space, and an escaped local is emitted verbatim. Drop the backticks or "
            + "rename it.",
            line,
            column,
            code: DiagnosticCodes.CodeGen.MemberNameCollision);
        return true;
    }

    private void Declare(string spelling, bool isEscaped, int line, int column)
    {
        if (_scope == null || spelling.Length == 0)
        {
            return;
        }

        if (RefuseReservedEscapedPrefix(spelling, isEscaped, line, column))
        {
            return;
        }

        // The check is on the EMITTED name, which is what the C# declaration space is keyed on, so
        // it has to be computed exactly as the emitter computes it — through NameCasing, escape and
        // all. A backtick-escaped local is emitted verbatim (owner decision on #1357), so `` `Zed` ``
        // occupies `Zed` and is no longer in the way of `zed`; an unescaped one is camelCased and
        // still is. Anything else here either refuses a legal program or lets a CS0128 through.
        var emitted = NameCasing.ResolveVariable(spelling, isEscaped);

        if (!_scope.TryGetValue(emitted, out var first))
        {
            _scope[emitted] = new Binding(spelling, line, column);
            return;
        }

        if (string.Equals(first.Spelling, spelling, StringComparison.Ordinal))
        {
            return; // Rebinding the same name: the emitter's version machinery handles it.
        }

        AddError(
            $"Name collision: local '{spelling}' and '{first.Spelling}' (line {first.Line}) both "
            + $"compile to '{emitted}', so they would share one variable. Rename one of them, or "
            + $"backtick-escape one to keep its spelling — an escaped local is emitted verbatim.",
            line,
            column,
            code: DiagnosticCodes.CodeGen.MemberNameCollision);
    }
}
