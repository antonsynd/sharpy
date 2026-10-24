using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Module = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// A generic replace over the immutable AST (#2324, P63b Design Decision 4): <see cref="Replace"/>
/// rebuilds the spine from <c>target</c> up to the root and leaves every other node shared. It reflects
/// over each record's public init properties — a <see cref="Node"/>, an <c>ImmutableArray</c> of anything
/// that may hold one, or a non-node AST record that holds nodes (<see cref="ExceptHandler"/>,
/// <see cref="MatchCase"/>, <see cref="FStringPart"/>, <see cref="Parameter"/> …) — so a node kind the
/// generators start emitting is reached the day it appears, with no hand <c>with { … }</c> per kind (the
/// injector's four-kind <c>ReplaceIdentifier</c> is the under-reach this replaces). Matching is by
/// reference; a target the walk cannot reach THROWS, never drops. Mutable members such as
/// <see cref="ImportStatement"/>'s resolution fields are not init properties of a node-holding type, so
/// they are copied with the clone and never walked.
/// </summary>
internal static class AstRewriter
{
    /// <summary>The walk found no init property that holds <c>target</c>.</summary>
    public sealed class UnreachableNodeException(string message) : Exception(message);

    /// <summary><paramref name="root"/> with the one node <paramref name="target"/> (by reference) replaced by
    /// <paramref name="replacement"/>.</summary>
    public static Module Replace(Module root, Node target, Node replacement)
    {
        var (rewritten, changed) = Rewrite(root, target, replacement);
        if (!changed)
            throw new UnreachableNodeException(
                $"a {target.GetType().Name} at {target.LineStart}:{target.ColumnStart} is not reachable from the module "
                + "through any init property — the rewriter cannot re-parent it");
        return (Module)rewritten!;
    }

    /// <summary>Applies <paramref name="edits"/> in order; each target must still be in the tree.</summary>
    public static Module ReplaceAll(Module root, IEnumerable<(Node Target, Node Replacement)> edits) =>
        edits.Aggregate(root, (module, edit) => Replace(module, edit.Target, edit.Replacement));

    /// <summary>
    /// The one AST shape every mutation reads: <paramref name="module"/> unparsed and re-parsed by the real
    /// parser. A generated AST and a parsed one differ (no <see cref="Parenthesized"/> nodes, a typeless
    /// <see cref="VariableDeclaration"/> for a reassignment, shared constant subtrees); the parsed form has
    /// one fresh instance per node, so a reference match edits exactly one place. A parse error is a
    /// generator or unparser defect and throws.
    /// </summary>
    public static Module Normalize(Module module) => Parse(Sharpy.Compiler.Pretty.Unparser.Unparse(module));

    /// <summary>Parses <paramref name="source"/> with the real lexer and parser; throws on a parse error.</summary>
    public static Module Parse(string source)
    {
        var parser = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        if (parser.Diagnostics.HasErrors)
            throw new InvalidOperationException("a mutation base does not parse ("
                + string.Join("; ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code}: {d.Message}"))
                + "):\n" + source);
        return module;
    }

    /// <summary>
    /// <paramref name="root"/> and every node reachable from it through the same init properties
    /// <see cref="Replace"/> rebuilds, pre-order. Unlike <see cref="Node.GetChildNodes"/> this reaches the
    /// type annotations (declarations, parameters, returns, bases, handlers, type arguments) the parser's
    /// traversal deliberately hides — an annotation is a mutation site — and it reaches exactly what the
    /// rewriter can re-parent, so every enumerated site is replaceable.
    /// </summary>
    public static IEnumerable<Node> Descendants(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            var children = ChildNodes(node).ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }

    /// <summary>Parses <paramref name="source"/>; null when it has a parse error (a parser THROW propagates —
    /// it is a defect, not a refusal).</summary>
    public static Module? ParseOrNull(string source)
    {
        var parser = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        return parser.Diagnostics.HasErrors ? null : module;
    }

    /// <summary>Every node's nearest node ancestor (by reference), through <see cref="Descendants"/>' walk.</summary>
    public static Dictionary<Node, Node> Parents(Node root)
    {
        var parents = new Dictionary<Node, Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in Descendants(root))
        {
            foreach (var child in ChildNodes(node))
                parents.TryAdd(child, node);
        }
        return parents;
    }

    /// <summary>The nodes directly held by <paramref name="value"/>'s init properties, looking through
    /// arrays and non-node AST records (an <see cref="ExceptHandler"/>'s annotation belongs to its
    /// <see cref="TryStatement"/>).</summary>
    private static IEnumerable<Node> ChildNodes(object value)
    {
        foreach (var property in NodeHoldingProperties(value.GetType()))
        {
            foreach (var node in NodesIn(property.GetValue(value)))
                yield return node;
        }
    }

    private static IEnumerable<Node> NodesIn(object? value)
    {
        switch (value)
        {
            case null:
                yield break;
            case Node node:
                yield return node;
                yield break;
        }
        if (ElementTypeOfImmutableArray(value.GetType()) is not null)
        {
            foreach (var item in (IEnumerable)value)
            {
                foreach (var node in NodesIn(item))
                    yield return node;
            }
        }
        else if (IsAstRecord(value.GetType()))
        {
            foreach (var node in ChildNodes(value))
                yield return node;
        }
    }

    private static (object? Value, bool Changed) Rewrite(object? value, Node target, Node replacement)
    {
        if (value is null)
            return (null, false);
        if (ReferenceEquals(value, target))
            return (replacement, true);

        var type = value.GetType();
        if (ElementTypeOfImmutableArray(type) is { } elementType)
            return RewriteArray(value, elementType, target, replacement);
        if (!IsAstRecord(type))
            return (value, false);

        foreach (var property in NodeHoldingProperties(type))
        {
            var (next, changed) = Rewrite(property.GetValue(value), target, replacement);
            if (!changed)
                continue;
            var clone = CloneMethod.Invoke(value, null)!;
            property.SetValue(clone, next);
            ClearCapturedSource(clone);
            return (clone, true);
        }
        return (value, false);
    }

    private static (object? Value, bool Changed) RewriteArray(object array, Type elementType, Node target, Node replacement)
    {
        var items = ((IEnumerable)array).Cast<object?>().ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var (next, changed) = Rewrite(items[i], target, replacement);
            if (!changed)
                continue;
            items[i] = next;
            var typed = Array.CreateInstance(elementType, items.Count);
            for (int j = 0; j < items.Count; j++)
                typed.SetValue(items[j], j);
            return (CreateImmutableArray(elementType).Invoke(null, new object[] { typed }), true);
        }
        return (array, false);
    }

    /// <summary>
    /// The verbatim source a parsed record captured (an f-string hole's <c>RawText</c>, its
    /// <c>ExpressionText</c> and self-documenting <c>SourceText</c>) describes the subtree as parsed; once a
    /// node under it is replaced the capture is stale, and the Unparser writes a captured hole verbatim —
    /// so a hole mutation was invisible in the source (measured: every f-string lane drew nothing before
    /// this). A rebuilt record drops its captures and is re-spelled from its nodes.
    /// </summary>
    private static void ClearCapturedSource(object clone)
    {
        foreach (var property in CapturedSourceProperties(clone.GetType()))
            property.SetValue(clone, null);
    }

    private static readonly string[] CapturedSourceNames = { "RawText", "ExpressionText", "SourceText" };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> CapturedSourceCache = new();

    private static PropertyInfo[] CapturedSourceProperties(Type type) =>
        CapturedSourceCache.GetOrAdd(type, t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.SetMethod is { IsPublic: true } && CapturedSourceNames.Contains(p.Name))
            .ToArray());

    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private const string AstNamespace = "Sharpy.Compiler.Parser.Ast";

    private static bool IsAstRecord(Type type) => type.IsClass && type.Namespace == AstNamespace;

    private static Type? ElementTypeOfImmutableArray(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
            ? type.GetGenericArguments()[0]
            : null;

    /// <summary>A property type that can hold a node: a node, a non-node AST record, or an
    /// <c>ImmutableArray</c> (or nullable one) of either.</summary>
    private static bool MayHoldNodes(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (ElementTypeOfImmutableArray(underlying) is { } element)
            return MayHoldNodes(element);
        return typeof(Node).IsAssignableFrom(underlying) || IsAstRecord(underlying);
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    private static PropertyInfo[] NodeHoldingProperties(Type type) =>
        PropertyCache.GetOrAdd(type, t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.SetMethod is { IsPublic: true } && MayHoldNodes(p.PropertyType))
            .ToArray());

    private static readonly ConcurrentDictionary<Type, MethodInfo> ArrayFactoryCache = new();

    private static MethodInfo CreateImmutableArray(Type elementType) =>
        ArrayFactoryCache.GetOrAdd(elementType, t => typeof(ImmutableArray)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(ImmutableArray.Create)
                && m.GetParameters() is [{ ParameterType.IsArray: true }])
            .MakeGenericMethod(t));
}
