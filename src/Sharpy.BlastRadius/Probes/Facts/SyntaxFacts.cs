using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.BlastRadius.Probes;

/// <summary>Walks over the tree that any probe needs.</summary>
internal static class SyntaxFacts
{
    /// <summary>Every node under <paramref name="node"/>, parents before children.</summary>
    public static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var below in Descendants(child))
                yield return below;
        }
    }

    /// <summary>
    /// The node and everything under it that runs as part of the same function body. A nested
    /// function, lambda or type is yielded and not entered: its body runs later, or never.
    /// </summary>
    public static IEnumerable<Node> InSameFunction(Node node)
    {
        yield return node;
        if (node is FunctionDef or LambdaExpression or ClassDef or StructDef)
            yield break;
        foreach (var child in node.GetChildNodes())
        {
            foreach (var below in InSameFunction(child))
                yield return below;
        }
    }

    /// <summary>The members of a class, each without its decorators.</summary>
    public static IEnumerable<Statement> Members(ClassDef classDef)
        => classDef.Body.Select(member => member.UnwrapDecorated());

    /// <summary>
    /// What a store target stores to: itself, or the elements of a tuple target. The parser has
    /// already removed parentheses and turned a list display into a tuple
    /// (<c>AstHelper.CanonicalizeStoreTarget</c>).
    /// </summary>
    public static IEnumerable<Expression> TargetLeaves(Expression target) => target switch
    {
        TupleLiteral tuple => tuple.Elements.SelectMany(TargetLeaves),
        StarExpression star => TargetLeaves(star.Operand),
        _ => new[] { target },
    };

    /// <summary>
    /// Names a function binds as locals: targets of assignments, declarations, loops, walruses and
    /// <c>with</c> items, <c>except ... as</c> names, pattern captures, imports, and the functions
    /// and types it declares.
    /// </summary>
    public static HashSet<string> LocalNames(FunctionDef function)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in function.Body.SelectMany(InSameFunction))
        {
            switch (node)
            {
                case Assignment assignment:
                    AddTargets(assignment.Target);
                    break;
                case VariableDeclaration declaration:
                    names.Add(declaration.Name);
                    break;
                case ForStatement loop:
                    AddTargets(loop.Target);
                    break;
                case WalrusExpression walrus:
                    names.Add(walrus.Target);
                    break;
                case WithStatement with:
                    foreach (var item in with.Items.Where(i => i.Target != null))
                        AddTargets(item.Target!);
                    break;
                case TryStatement attempt:
                    names.UnionWith(attempt.Handlers.Select(h => h.Name).OfType<string>());
                    break;
                case BindingPattern binding:
                    names.Add(binding.Name.Name);
                    break;
                case AsPattern alias:
                    names.Add(alias.Name.Name);
                    break;
                case ImportStatement import:
                    names.UnionWith(import.Names.Select(n => n.AsName ?? n.Name.Split('.')[0]));
                    break;
                case FromImportStatement import:
                    names.UnionWith(import.Names.Select(n => n.AsName ?? n.Name));
                    break;
                case FunctionDef nested:
                    names.Add(nested.Name);
                    break;
                case ClassDef nested:
                    names.Add(nested.Name);
                    break;
                case StructDef nested:
                    names.Add(nested.Name);
                    break;
            }
        }

        return names;

        void AddTargets(Expression target)
            => names.UnionWith(TargetLeaves(target).OfType<Identifier>().Select(i => i.Name));
    }
}
