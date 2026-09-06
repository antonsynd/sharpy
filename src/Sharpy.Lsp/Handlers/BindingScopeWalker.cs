using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// The names already bound on one control-flow path of a lexical scope. A binding gets an
/// inferred-type hint only the first time its name is bound on its path: later assignments
/// to the same name are rebindings, and a hint on each of them is noise. A function, class
/// or struct body starts a fresh scope (so a name shadowed in a nested scope hints again at
/// its own binding); while/for/with bodies share the enclosing scope; mutually-exclusive
/// branches (if/elif/else, except handlers, match cases) each work on a <see cref="Fork"/>
/// and are merged back with <see cref="MergeFrom"/> — each branch's first binding hints,
/// and code after the construct treats a name bound in any branch as already bound.
/// </summary>
/// <remarks>
/// Shared by <see cref="SharpyInlayHintHandler"/> (inferred-type hints) and
/// <see cref="SharpyDocumentSymbolHandler"/> (outline entries for declaring assignments).
/// </remarks>
internal sealed class BindingScope
{
    private readonly HashSet<string> _bound;

    public BindingScope() => _bound = new(StringComparer.Ordinal);

    private BindingScope(HashSet<string> bound) => _bound = bound;

    /// <summary>Records <paramref name="name"/> as bound; true if this is its first binding.</summary>
    public bool TryDeclare(string name) => _bound.Add(name);

    /// <summary>
    /// Records a binding that never carries a hint itself (parameter, loop target,
    /// <c>with … as</c>, <c>except … as</c>, match-pattern capture) so a later assignment
    /// to the same name is correctly treated as a rebinding.
    /// </summary>
    public void MarkBound(string name) => _bound.Add(name);

    /// <summary>A copy for one branch of a mutually-exclusive construct.</summary>
    public BindingScope Fork() => new(new HashSet<string>(_bound, StringComparer.Ordinal));

    /// <summary>Folds a branch's bindings back in: bound in any branch is bound after it.</summary>
    public void MergeFrom(BindingScope branch) => _bound.UnionWith(branch._bound);
}

/// <summary>
/// Static helpers that mark names bound by loop targets and match patterns. Used by the
/// inlay-hint and document-symbol handlers to keep the binding scope consistent across all
/// binding forms that carry no hint or outline entry themselves.
/// </summary>
internal static class BindingScopeWalker
{
    /// <summary>
    /// Records the names a loop target binds. Tuple targets bind each element; anything else
    /// (attribute or index targets) binds no name.
    /// </summary>
    internal static void MarkTargetBound(Expression target, BindingScope scope)
    {
        switch (target)
        {
            case Identifier id:
                scope.MarkBound(id.Name);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    MarkTargetBound(element, scope);
                break;
            case StarExpression star:
                MarkTargetBound(star.Operand, scope);
                break;
        }
    }

    /// <summary>
    /// Records the names a match pattern captures (<c>case x:</c>, <c>case Point(x=px):</c>,
    /// <c>case [head, *rest]:</c>, …). Like loop targets, captures produce no hint themselves,
    /// but a later assignment to a captured name is a rebinding.
    /// </summary>
    internal static void MarkPatternBound(Pattern pattern, BindingScope scope,
        SemanticInfo? semanticInfo = null)
    {
        switch (pattern)
        {
            case BindingPattern binding:
                if (semanticInfo?.GetPatternUnionCase(binding) == null)
                    scope.MarkBound(binding.Name.Name);
                break;
            case StarPattern { Capture: { } capture }:
                MarkPatternBound(capture, scope, semanticInfo);
                break;
            case TuplePattern tuple:
                foreach (var element in tuple.Elements)
                    MarkPatternBound(element, scope, semanticInfo);
                break;
            case ListPattern list:
                foreach (var element in list.Elements)
                    MarkPatternBound(element, scope, semanticInfo);
                break;
            case PositionalPattern positional:
                foreach (var element in positional.Elements)
                    MarkPatternBound(element, scope, semanticInfo);
                break;
            case PropertyPattern property:
                foreach (var field in property.Fields)
                    MarkPatternBound(field.Pattern, scope, semanticInfo);
                break;
            case OrPattern orPattern:
                foreach (var alternative in orPattern.Alternatives)
                    MarkPatternBound(alternative, scope, semanticInfo);
                break;
            case AndPattern andPattern:
                MarkPatternBound(andPattern.Left, scope, semanticInfo);
                MarkPatternBound(andPattern.Right, scope, semanticInfo);
                break;
            case AsPattern asPattern:
                scope.MarkBound(asPattern.Name.Name);
                MarkPatternBound(asPattern.Inner, scope, semanticInfo);
                break;
            case GuardPattern guard:
                MarkPatternBound(guard.Inner, scope, semanticInfo);
                break;
        }
    }
}
