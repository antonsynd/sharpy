using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>Which arm a D6 use takes (plan-775c50 D6; see <see cref="SemanticInfo.GetAsyncGuardedUse"/>).</summary>
public enum AsyncGuardedUseKind
{
    /// <summary>The value's ensure call is hoisted before the enclosing statement and the use names the
    /// backing field: a <c>ref</c> / <c>in</c> operand naming the value, or the value as the receiver
    /// root of a member or element path that is stored to, passed by modifier (<c>ref</c> / <c>in</c> /
    /// <c>out</c>), or a method call's receiver.</summary>
    Ensure,

    /// <summary>An <c>out</c> operand naming the value itself: its arm (the storage once the value is
    /// Ready, the temporary before; D4) is picked before the enclosing statement.</summary>
    OutOperand,
}

/// <summary>
/// One D6 use (plan-775c50): <paramref name="Node"/>, the Identifier or MemberAccess that names the
/// Guarded value <paramref name="Value"/> in an <c>async def</c> body, and its arm.
/// </summary>
public sealed record AsyncGuardedUse(Expression Node, Symbol Value, AsyncGuardedUseKind Kind);

/// <summary>
/// Records, for each <c>async def</c> body in a module, the uses of Guarded values that are neither a
/// plain read nor a plain store (plan-775c50 D6, P77 T6), on <see cref="SemanticInfo"/>. Under D4 a
/// Guarded variable's or <c>@static</c> field's accessor returns by reference (a computed const's
/// <c>ref readonly</c>); a use whose C# holds that reference while later operands run — and so across
/// an <c>await</c> among them, CS8178 — is recorded, so the emitter names the backing field after an
/// ensure hoisted before the statement. The classification is D4's taxonomy by the use's SHAPE, never
/// by the value's type (D4: an arm is picked by <c>StaticStorage</c>, never by a type).
/// <para><b>Recorded.</b> The node under <c>out</c> (<see cref="AsyncGuardedUseKind.OutOperand"/>) or
/// <c>ref</c> / <c>in</c>; the node as the receiver root of a member / index / slice path
/// (parentheses skipped) that is under any modifier, a store target (an assignment's, augmented or
/// not, a tuple or star leaf of one, a <c>for</c>, <c>with</c> or comprehension target), or the
/// receiver of a method call (<see cref="AsyncGuardedUseKind.Ensure"/>).</para>
/// <para><b>Not recorded.</b> A plain read: the value read by value, a member or element of it read by
/// value, the value invoked or passed. A plain store: every store site on the bare or qualified name
/// itself (D4's "plain stores"). A use outside an <c>async def</c> body — a nested <c>def</c>, lambda
/// or class body is its own scope, recorded only when it is itself an <c>async def</c>. A Guarded
/// property (module or class): its accessor returns a value, never a reference.</para>
/// <para>Runs after <c>CodeGenInfoComputer.ComputeForModule</c>, so every storage fact it reads is
/// final; an imported value's fact is read through <see cref="ImportedStaticFacts.ImportedStorage"/>,
/// the one importer-side reader.</para>
/// </summary>
internal sealed class AsyncGuardedUseAnalyzer
{
    private readonly SemanticInfo _info;
    private readonly SemanticBinding _binding;
    private readonly SemanticBinding _importFacts;
    private readonly List<Node> _ancestors = new();

    private AsyncGuardedUseAnalyzer(SemanticInfo info, SemanticBinding binding, SemanticBinding? importFacts)
    {
        _info = info;
        _binding = binding;
        _importFacts = importFacts ?? binding;
    }

    /// <summary>Records every D6 use in <paramref name="module"/> on <paramref name="info"/>.</summary>
    public static void Analyze(Module module, SemanticInfo info, SemanticBinding binding, SemanticBinding? importFacts)
    {
        var analyzer = new AsyncGuardedUseAnalyzer(info, binding, importFacts);
        foreach (var statement in module.Body)
            analyzer.Walk(statement, inAsync: false);
    }

    private void Walk(Node node, bool inAsync)
    {
        if (inAsync && node is Identifier or MemberAccess)
            Classify((Expression)node);

        // The scope of the children: a function body is async exactly when its def is; a lambda or a
        // type body is never (its methods are their own FunctionDefs). A parameter default is not in
        // the body.
        var childInAsync = node switch
        {
            FunctionDef def => def.IsAsync,
            LambdaExpression => false,
            ClassDef or StructDef or InterfaceDef or EnumDef or UnionDef or PropertyDef or DelegateDef or EventDef => false,
            _ => inAsync,
        };

        _ancestors.Add(node);
        if (node is FunctionDef function)
        {
            foreach (var parameter in function.Parameters)
            {
                if (parameter.DefaultValue != null)
                    Walk(parameter.DefaultValue, inAsync: false);
            }
            foreach (var statement in function.Body)
                Walk(statement, childInAsync);
        }
        else
        {
            foreach (var child in node.GetChildNodes())
            {
                if (child != null)
                    Walk(child, childInAsync);
            }
        }
        _ancestors.RemoveAt(_ancestors.Count - 1);
    }

    private void Classify(Expression node)
    {
        if (GuardedValue(node) is not { } value)
            return;
        if (UseKind(node) is { } kind)
            _info.SetAsyncGuardedUse(node, value, kind);
    }

    /// <summary>The Guarded variable value <paramref name="node"/> names (a module variable, a
    /// <c>@static</c> field or a computed const, by any spelling), or null.</summary>
    private Symbol? GuardedValue(Expression node)
    {
        var symbol = node switch
        {
            Identifier id => _info.GetIdentifierSymbol(id),
            MemberAccess ma => _info.GetMemberAccessResolution(ma)?.Member ?? _info.GetModuleMemberResolution(ma)?.Member,
            _ => null,
        };
        if (symbol is not VariableSymbol { IsModuleProperty: false } variable)
            return null;
        var storage = _binding.GetCodeGenInfo(variable)?.StaticStorage
            ?? ImportedStaticFacts.ImportedStorage(_importFacts, variable).Storage;
        return storage == StaticStorage.Guarded ? variable : null;
    }

    /// <summary>D4's taxonomy of the use, read from the node's ancestors: null for a plain read or a
    /// plain store.</summary>
    private AsyncGuardedUseKind? UseKind(Expression node)
    {
        // _ancestors ends with the node's parent (Walk adds a node after classifying it).
        var i = _ancestors.Count - 1;
        Expression current = node;
        SkipParentheses(ref current, ref i);

        if (i >= 0 && _ancestors[i] is ModifiedArgument { Modifier: not ParameterModifier.None } operand
            && ReferenceEquals(operand.Argument, current))
        {
            return operand.Modifier == ParameterModifier.Out ? AsyncGuardedUseKind.OutOperand : AsyncGuardedUseKind.Ensure;
        }

        // Climb the member / element path the value is the receiver root of.
        var onPath = false;
        while (i >= 0 && IsReceiverOf(_ancestors[i], current))
        {
            onPath = true;
            current = (Expression)_ancestors[i];
            i--;
            SkipParentheses(ref current, ref i);
        }
        if (!onPath || i < 0)
            return null;

        var parent = _ancestors[i];
        if (parent is ModifiedArgument { Modifier: not ParameterModifier.None } pathOperand
            && ReferenceEquals(pathOperand.Argument, current))
            return AsyncGuardedUseKind.Ensure;
        // A method call on the value, or on a member of it (D4's "mutating call").
        if (parent is FunctionCall call && ReferenceEquals(call.Function, current) && current is MemberAccess)
            return AsyncGuardedUseKind.Ensure;
        if (IsStoreTarget(current, i))
            return AsyncGuardedUseKind.Ensure;
        return null;
    }

    private static bool IsReceiverOf(Node parent, Expression child) => parent switch
    {
        MemberAccess ma => ReferenceEquals(ma.Object, child),
        IndexAccess ia => ReferenceEquals(ia.Object, child),
        SliceAccess sa => ReferenceEquals(sa.Object, child),
        MultiAxisAccess mx => ReferenceEquals(mx.Object, child),
        _ => false,
    };

    private void SkipParentheses(ref Expression current, ref int i)
    {
        while (i >= 0 && _ancestors[i] is Parenthesized p && ReferenceEquals(p.Expression, current))
        {
            current = p;
            i--;
        }
    }

    /// <summary>Whether <paramref name="target"/> (whose parent is <c>_ancestors[i]</c>) is a store
    /// site's target, or a leaf of one through tuple, list, star and parentheses.</summary>
    private bool IsStoreTarget(Expression target, int i)
    {
        Expression top = target;
        while (i >= 0 && _ancestors[i] is TupleLiteral or ListLiteral or StarExpression or Parenthesized
               && IsElementOf(_ancestors[i], top))
        {
            top = (Expression)_ancestors[i];
            i--;
        }
        if (i < 0)
            return false;
        return _ancestors[i] switch
        {
            Assignment assignment => ReferenceEquals(assignment.Target, top),
            ForStatement loop => ReferenceEquals(loop.Target, top),
            ForClause clause => ReferenceEquals(clause.Target, top),
            WithStatement with => with.Items.Any(item => ReferenceEquals(item.Target, top)),
            _ => false,
        };
    }

    private static bool IsElementOf(Node parent, Expression child) => parent switch
    {
        TupleLiteral tuple => tuple.Elements.Any(e => ReferenceEquals(e, child)),
        ListLiteral list => list.Elements.Any(e => ReferenceEquals(e, child)),
        StarExpression star => ReferenceEquals(star.Operand, child),
        Parenthesized p => ReferenceEquals(p.Expression, child),
        _ => false,
    };
}
