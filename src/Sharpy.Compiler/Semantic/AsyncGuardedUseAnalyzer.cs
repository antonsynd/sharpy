using Sharpy.Compiler.Diagnostics;
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
    private readonly bool _requireBound;
    private readonly List<Node> _ancestors = new();

    private AsyncGuardedUseAnalyzer(
        SemanticInfo info, SemanticBinding binding, SemanticBinding? importFacts, bool requireBound)
    {
        _info = info;
        _binding = binding;
        _importFacts = importFacts ?? binding;
        _requireBound = requireBound;
    }

    /// <summary>Records every D6 use in <paramref name="module"/> on <paramref name="info"/>.
    /// <paramref name="requireBound"/> arms the totality guard (<see cref="RequireBound"/>); a module
    /// the checker refused is not held to it, so a refusal is never masked by an internal error.</summary>
    public static void Analyze(
        Module module, SemanticInfo info, SemanticBinding binding, SemanticBinding? importFacts, bool requireBound = true)
    {
        var analyzer = new AsyncGuardedUseAnalyzer(info, binding, importFacts, requireBound);
        foreach (var statement in module.Body)
            analyzer.Walk(statement, inAsync: false);
    }

    private void Walk(Node node, bool inAsync)
    {
        // Only a FunctionDef opens an async scope, and no expression holds a statement, so nothing under
        // an expression outside an async def body is in one.
        if (!inAsync && node is Expression)
            return;
        if (inAsync && node is Identifier or MemberAccess)
            Classify((Expression)node);

        // The scope of the children: a function body is async exactly when its def is; a lambda or a
        // type body is never (its methods are their own FunctionDefs). A parameter default is not in
        // the body, and is not walked.
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
        {
            RequireBound(node);
            return;
        }
        if (UseKind(node) is { } kind)
            _info.SetAsyncGuardedUse(node, value, kind);
    }

    /// <summary>The Guarded variable value <paramref name="node"/> names (a module variable, a
    /// <c>@static</c> field or a computed const, by any spelling), or null.</summary>
    private Symbol? GuardedValue(Expression node)
    {
        if (_info.GetBoundValueSymbol(node) is not VariableSymbol { IsModuleProperty: false } variable)
            return null;
        return IsGuarded(variable) ? variable : null;
    }

    private bool IsGuarded(VariableSymbol variable)
        => (_binding.GetCodeGenInfo(variable)?.StaticStorage
            ?? ImportedStaticFacts.ImportedStorage(_importFacts, variable).Storage) == StaticStorage.Guarded;

    /// <summary>
    /// The totality guard (plan-775c50 D6): a member access in a D6 position or at a store site (a
    /// plain or augmented assignment's target) that names a Guarded static through a type-denoting
    /// receiver must carry the checker's binding to that Guarded declaration, or the emitter would
    /// pass the value's by-reference accessor across an <c>await</c> (CS8178 behind SPY0908). The
    /// receiver is read STRUCTURALLY here — a type name, a module's exported type, a nested type by
    /// name, a constructed generic's definition, a recorded type reference — never through the
    /// checker's resolver, so a spelling the resolver misses is an internal error at its node instead.
    /// </summary>
    private void RequireBound(Expression node)
    {
        if (!_requireBound || node is not MemberAccess access
            || StructuralType(access.Object) is not { } type
            || TypeHierarchyService.FindStaticValueField(type, access.Member, _binding) is not ({ } field, { } owner)
            || !IsGuarded(DeclarationField(owner, field))
            || (UseKind(access) == null && !IsStoreSite(access)))
            return;

        var bound = _info.GetBoundValueSymbol(access);
        throw new InternalCompilerErrorException("AsyncGuardedUseAnalyzer",
            $"'{access.Member}' names the Guarded value '{field.Name}' through '{type.Name}' in an async def "
            + (bound == null
                ? "body, but the checker bound no value symbol to the access (plan-775c50 D6)"
                : $"body, but the checker bound '{bound.Name}', which is not the Guarded declaration (plan-775c50 D6)"),
            access);
    }

    /// <summary>The field of <paramref name="owner"/>'s declaration (its origin chain's root) named as
    /// <paramref name="field"/>: an import alias's clone (<c>from lib import LC as CC</c>) holds the
    /// loader's fields, not the declared ones whose storage is Guarded.</summary>
    private static VariableSymbol DeclarationField(TypeSymbol owner, VariableSymbol field)
        => owner.DeclarationRoot() is TypeSymbol root
            && root.Fields.FirstOrDefault(f => f.Name == field.Name) is { } declared
            ? declared
            : field;

    /// <summary>Whether <paramref name="node"/> (whose parent ends <c>_ancestors</c>) is itself a store
    /// site's target — a plain or augmented assignment's, a tuple leaf of one, a <c>for</c> /
    /// <c>with</c> / comprehension target: the emitter routes a bound Guarded target through its
    /// setter, and an unbound one keeps the by-reference accessor across the statement.</summary>
    private bool IsStoreSite(Expression node)
    {
        var i = _ancestors.Count - 1;
        var current = node;
        SkipParentheses(ref current, ref i);
        return i >= 0 && IsStoreTarget(current, i);
    }

    /// <summary>The type a receiver spells, read from its shape and the symbols its names bind.</summary>
    private TypeSymbol? StructuralType(Expression receiver) => receiver switch
    {
        Identifier id when _info.GetIdentifierSymbol(id) is TypeSymbol type => type,
        MemberAccess { Object: Identifier moduleName } qualified
            when _info.GetIdentifierSymbol(moduleName) is ModuleSymbol module
            => module.Exports.TryGetValue(qualified.Member, out var exported) ? exported as TypeSymbol : null,
        MemberAccess nested when StructuralType(nested.Object) is { } outer
            => TypeHierarchyService.FindNestedType(outer, nested.Member, _binding),
        IndexAccess constructed => StructuralType(constructed.Object),
        _ when _info.IsTypeReference(receiver) => _info.GetExpressionType(receiver) switch
        {
            UserDefinedType { Symbol: { } symbol } => symbol,
            GenericType { GenericDefinition: { } definition } => definition,
            _ => null,
        },
        _ => null,
    };

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
