using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// A Roslyn index of the property sources the typed-generator roster leans on (#2288, verify finding
/// F3): every method of every scanned file, with the <c>Type.Member</c> references, the string literals
/// and the same-file calls of its body. <see cref="TypedGeneratorRosterTests"/> reads it to tie each
/// roster lane to the property that sustains it, so a roster row cannot drift green from the code.
/// </summary>
internal sealed class PropertySourceIndex
{
    /// <summary>One method: <see cref="References"/> are the <c>X.Y</c> member accesses of its body
    /// (calls and method groups alike), <see cref="Literals"/> its string literals, <see cref="Calls"/>
    /// the simple names it invokes (candidates for a same-file helper).</summary>
    public sealed record Method(
        string File,
        string Name,
        SyntaxNode Body,
        IReadOnlySet<string> References,
        IReadOnlySet<string> Literals,
        IReadOnlySet<string> Calls)
    {
        public override string ToString() => $"{File}:{Name}";
    }

    private readonly Dictionary<string, List<Method>> _byFile;

    private PropertySourceIndex(Dictionary<string, List<Method>> byFile)
    {
        _byFile = byFile;
    }

    public IEnumerable<Method> Methods => _byFile.Values.SelectMany(m => m);

    /// <summary>Indexes every <c>*.cs</c> under <paramref name="root"/> whose root-relative,
    /// '/'-separated path <paramref name="include"/> accepts.</summary>
    public static PropertySourceIndex Load(string root, Func<string, bool> include)
    {
        var byFile = new Dictionary<string, List<Method>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (!include(relative))
                continue;
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            byFile[relative] = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body is not null || m.ExpressionBody is not null)
                .Select(m => Describe(relative, m))
                .ToList();
        }

        return new PropertySourceIndex(byFile);
    }

    private static Method Describe(string file, MethodDeclarationSyntax declaration)
    {
        SyntaxNode body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody!;
        var references = body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
            .Where(m => m.Expression is IdentifierNameSyntax)
            .Select(m => $"{((IdentifierNameSyntax)m.Expression).Identifier.ValueText}.{m.Name.Identifier.ValueText}")
            .ToHashSet(StringComparer.Ordinal);
        var literals = body.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(l => l.Token.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        var calls = body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Select(i => i.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                GenericNameSyntax g => g.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var n } => n.Identifier.ValueText,
                _ => null,
            })
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        return new Method(file, declaration.Identifier.ValueText, body, references, literals, calls);
    }

    /// <summary><paramref name="method"/> and every same-file method it calls, transitively — the code
    /// that runs when the method runs, as far as one file can say.</summary>
    public IReadOnlyList<Method> Closure(Method method)
    {
        var sameFile = _byFile[method.File];
        var seen = new HashSet<Method> { method };
        var queue = new Queue<Method>(seen);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var callee in sameFile.Where(m => current.Calls.Contains(m.Name)))
            {
                if (seen.Add(callee))
                    queue.Enqueue(callee);
            }
        }

        return seen.ToList();
    }

    /// <summary>The invocations in <paramref name="method"/>'s closure, as <c>X.Y</c> (or the simple
    /// name), each with its argument list.</summary>
    public IEnumerable<(string Name, ArgumentListSyntax Arguments)> ClosureInvocations(Method method) =>
        Closure(method).SelectMany(m => m.Body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            .Select(i => (i.Expression switch
            {
                MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: var n } =>
                    $"{receiver.Identifier.ValueText}.{n.Identifier.ValueText}",
                MemberAccessExpressionSyntax { Name: var n } => n.Identifier.ValueText,
                IdentifierNameSyntax id => id.Identifier.ValueText,
                GenericNameSyntax g => g.Identifier.ValueText,
                _ => "",
            }, i.ArgumentList));

    /// <summary>
    /// Every <c>new Y("Label", () =&gt; …)</c> in <paramref name="rosterSource"/>: its label, line, and
    /// the <paramref name="generatorMethods"/> (<c>Type.Method</c>) its lambda calls or passes as a method
    /// group — following calls to the roster's own helper methods (<c>TypedStatements()</c> →
    /// <c>GenTyped.TypedProgram</c>).
    /// </summary>
    public static List<(string Label, int Line, IReadOnlySet<string> Invoked)> YRows(
        string rosterSource, IReadOnlySet<string> generatorMethods)
    {
        var tree = CSharpSyntaxTree.ParseText(rosterSource);
        var root = tree.GetRoot();
        var helpers = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .GroupBy(m => m.Identifier.ValueText)
            .ToDictionary(g => g.Key, g => g.Select(m => (SyntaxNode?)m.Body ?? m.ExpressionBody!).ToList(), StringComparer.Ordinal);

        IReadOnlySet<string> Invoked(SyntaxNode scope)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<SyntaxNode>();
            pending.Push(scope);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                // A generator method called or passed as a method group (`SelectMany(GenX.Member)`).
                foreach (var access in current.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
                {
                    if (access.Expression is IdentifierNameSyntax type
                        && generatorMethods.Contains($"{type.Identifier.ValueText}.{access.Name.Identifier.ValueText}"))
                        found.Add($"{type.Identifier.ValueText}.{access.Name.Identifier.ValueText}");
                }

                // A call to one of the roster's own helpers is followed into its body.
                foreach (var invocation in current.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is IdentifierNameSyntax helper
                        && helpers.TryGetValue(helper.Identifier.ValueText, out var bodies)
                        && visited.Add(helper.Identifier.ValueText))
                    {
                        foreach (var body in bodies)
                            pending.Push(body);
                    }
                }
            }

            return found;
        }

        return root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(c => c.Type is IdentifierNameSyntax { Identifier.ValueText: "Y" }
                && c.ArgumentList is { Arguments.Count: >= 2 } args
                && args.Arguments[0].Expression is LiteralExpressionSyntax { Token.Value: string })
            .Select(c => (
                (string)((LiteralExpressionSyntax)c.ArgumentList!.Arguments[0].Expression).Token.Value!,
                tree.GetLineSpan(c.Span).StartLinePosition.Line + 1,
                Invoked(c.ArgumentList.Arguments[1].Expression)))
            .ToList();
    }
}
