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
    private readonly Dictionary<string, List<MethodDeclarationSyntax>> _declarations;

    private PropertySourceIndex(Dictionary<string, List<Method>> byFile, Dictionary<string, List<MethodDeclarationSyntax>> declarations)
    {
        _byFile = byFile;
        _declarations = declarations;
    }

    public IEnumerable<Method> Methods => _byFile.Values.SelectMany(m => m);

    /// <summary>Indexes every <c>*.cs</c> under <paramref name="root"/> whose root-relative,
    /// '/'-separated path <paramref name="include"/> accepts.</summary>
    public static PropertySourceIndex Load(string root, Func<string, bool> include)
    {
        var byFile = new Dictionary<string, List<Method>>(StringComparer.Ordinal);
        var declarations = new Dictionary<string, List<MethodDeclarationSyntax>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (!include(relative))
                continue;
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            declarations[relative] = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body is not null || m.ExpressionBody is not null)
                .ToList();
            byFile[relative] = declarations[relative].Select(m => Describe(relative, m)).ToList();
        }

        return new PropertySourceIndex(byFile, declarations);
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


    // ---- generator calls by ARGUMENT, and consumed verdicts (#2324, P63b Decision 8) ----------------

    /// <summary>A call of a typed generator member with its arguments normalized: positional in parameter
    /// order (a named argument is placed at its parameter's position), whitespace-collapsed, a lambda's own
    /// parameter spelled <c>_</c>, and a same-file helper's parameter replaced by the argument its caller
    /// passed (or its default). Two properties that sample the same generator configuration produce the
    /// same <see cref="Arguments"/> however their helpers are written.</summary>
    public sealed record GeneratorCall(string Member, string Arguments)
    {
        public override string ToString() => $"{Member}({Arguments})";
    }

    /// <summary>The typed generator calls reachable from <paramref name="scope"/> (a method body or a lambda
    /// inside <paramref name="file"/>), following same-file helper calls with their arguments bound.
    /// <paramref name="generatorMembers"/> maps <c>Type.Member</c> to its overloads (reflection).</summary>
    public IReadOnlyList<GeneratorCall> GeneratorCalls(string file, SyntaxNode scope,
        IReadOnlyDictionary<string, System.Reflection.MethodInfo[]> generatorMembers)
    {
        var found = new List<GeneratorCall>();
        var declarations = _declarations.GetValueOrDefault(file) ?? new List<MethodDeclarationSyntax>();
        Walk(scope, new Dictionary<string, string>(StringComparer.Ordinal), 0);
        return found.Distinct().ToList();

        void Walk(SyntaxNode node, IReadOnlyDictionary<string, string> bound, int depth)
        {
            if (depth > 6)
                return;
            foreach (var access in node.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Expression is not IdentifierNameSyntax type)
                    continue;
                var name = $"{type.Identifier.ValueText}.{access.Name.Identifier.ValueText}";
                if (!generatorMembers.TryGetValue(name, out var overloads))
                    continue;
                found.Add(access.Parent is InvocationExpressionSyntax call && ReferenceEquals(call.Expression, access)
                    ? new GeneratorCall(name, Normalize(call.ArgumentList, overloads, bound))
                    : new GeneratorCall(name, "<method group>"));
            }
            foreach (var invocation in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax helperName)
                    continue;
                foreach (var helper in declarations.Where(d => d.Identifier.ValueText == helperName.Identifier.ValueText
                             && d.ParameterList.Parameters.Count >= invocation.ArgumentList.Arguments.Count))
                {
                    var map = Bind(helper.ParameterList, invocation.ArgumentList, bound);
                    if (map is not null && ((SyntaxNode?)helper.Body ?? helper.ExpressionBody) is { } body)
                        Walk(body, map, depth + 1);
                }
            }
        }
    }

    private static Dictionary<string, string>? Bind(ParameterListSyntax parameters, ArgumentListSyntax arguments,
        IReadOnlyDictionary<string, string> outer)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var list = parameters.Parameters;
        for (int i = 0; i < arguments.Arguments.Count; i++)
        {
            var argument = arguments.Arguments[i];
            var target = argument.NameColon is { } named
                ? list.FirstOrDefault(p => p.Identifier.ValueText == named.Name.Identifier.ValueText)
                : i < list.Count ? list[i] : null;
            if (target is null)
                return null;
            map[target.Identifier.ValueText] = Text(argument.Expression, outer);
        }
        foreach (var parameter in list)
        {
            if (!map.ContainsKey(parameter.Identifier.ValueText) && parameter.Default is { } d)
                map[parameter.Identifier.ValueText] = Text(d.Value, outer);
        }
        return map;
    }

    private static string Normalize(ArgumentListSyntax arguments, System.Reflection.MethodInfo[] overloads,
        IReadOnlyDictionary<string, string> bound)
    {
        var args = arguments.Arguments;
        var overload = overloads.FirstOrDefault(o => o.GetParameters().Length == args.Count
            && args.All(a => a.NameColon is null || o.GetParameters().Any(p => p.Name == a.NameColon.Name.Identifier.ValueText)));
        if (overload is null)
        {
            return string.Join(", ", args.Where(a => a.NameColon is null).Select(a => Text(a.Expression, bound))
                .Concat(args.Where(a => a.NameColon is not null).OrderBy(a => a.NameColon!.Name.Identifier.ValueText, StringComparer.Ordinal)
                    .Select(a => $"{a.NameColon!.Name.Identifier.ValueText}: {Text(a.Expression, bound)}")));
        }
        var parameters = overload.GetParameters();
        var slots = new string[parameters.Length];
        for (int i = 0; i < args.Count; i++)
        {
            var index = args[i].NameColon is { } named
                ? Array.FindIndex(parameters, p => p.Name == named.Name.Identifier.ValueText)
                : i;
            slots[index] = Text(args[i].Expression, bound);
        }
        return string.Join(", ", slots);
    }

    /// <summary>An argument's text: tokens joined by one space, a bound helper parameter replaced by its
    /// argument, an enclosing lambda's parameter spelled <c>_</c>.</summary>
    private static string Text(SyntaxNode expression, IReadOnlyDictionary<string, string> bound)
    {
        var lambdaParameters = expression.AncestorsAndSelf().Concat(expression.DescendantNodes())
            .SelectMany(n => n switch
            {
                SimpleLambdaExpressionSyntax l => new[] { l.Parameter.Identifier.ValueText },
                ParenthesizedLambdaExpressionSyntax l => l.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray(),
                _ => Array.Empty<string>(),
            })
            .ToHashSet(StringComparer.Ordinal);
        var parts = new List<string>();
        foreach (var token in expression.DescendantTokens())
        {
            var afterDot = token.GetPreviousToken().IsKind(SyntaxKind.DotToken);
            if (token.IsKind(SyntaxKind.IdentifierToken) && !afterDot)
            {
                if (bound.TryGetValue(token.ValueText, out var value))
                {
                    parts.Add(value);
                    continue;
                }
                if (lambdaParameters.Contains(token.ValueText))
                {
                    parts.Add("_");
                    continue;
                }
            }
            parts.Add(token.Text);
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Whether the verdict <paramref name="verdict"/> (an invocation in <paramref name="method"/>'s closure)
    /// is CONSUMED — decided, never discarded: asserted directly; returned from a lambda passed to
    /// <c>JudgeSamples</c> whose result's <c>.First</c> reaches an <c>Assert</c>; bound by <c>is { } x</c> and
    /// added to a collection an <c>Assert</c> reads; assigned to a local an <c>Assert</c> reads; or, for a
    /// <c>SemanticFilter</c> verdict, its sink's <c>.Unexplained</c> asserted (directly or by a same-file helper
    /// the sink is passed to).
    /// </summary>
    public bool IsConsumed(Method method, InvocationExpressionSyntax verdict)
    {
        var host = Closure(method).FirstOrDefault(m => m.Body.DescendantNodesAndSelf().Contains(verdict)) ?? method;
        bool Asserted(SyntaxNode scope, Func<string, bool> mentions) =>
            AssertionsIn(host, scope).Any(a => mentions(a.ArgumentList.ToString()));

        var name = InvokedName(verdict);
        // An assertion is its own consumer: Assert.*, and the corpus helpers that assert every sample.
        if (IsAssertion(verdict) || name is "PropertyCorpus.AssertAllPassOrAllowed" or "AssertEverySampleIsDiagnosticFree"
            || verdict.Ancestors().OfType<InvocationExpressionSyntax>().Any(IsAssertion))
            return true;

        if (name.StartsWith("SemanticFilter.", StringComparison.Ordinal))
        {
            var sink = verdict.ArgumentList.Arguments.Select(a => a.Expression).OfType<IdentifierNameSyntax>()
                .Select(i => i.Identifier.ValueText).LastOrDefault();
            if (sink is null)
                return false;
            if (Asserted(host.Body, t => t.Contains(sink + ".Unexplained", StringComparison.Ordinal)))
                return true;
            // the sink handed to a same-file helper that asserts its Unexplained rejections
            return host.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.ArgumentList.Arguments.Any(a => a.Expression is IdentifierNameSyntax { Identifier.ValueText: var v } && v == sink))
                .Select(i => i.Expression is IdentifierNameSyntax h ? h.Identifier.ValueText : null)
                .OfType<string>()
                .SelectMany(h => _byFile[host.File].Where(m => m.Name == h))
                .Any(helper => AssertionsIn(helper, helper.Body).Any(a => a.ArgumentList.ToString().Contains(".Unexplained", StringComparison.Ordinal)));
        }

        // returned from a lambda handed to JudgeSamples: the SampleVerdicts' First must be asserted
        if (verdict.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault() is { } lambda
            && lambda.Parent?.Parent is ArgumentListSyntax { Parent: InvocationExpressionSyntax judgeSamples }
            && InvokedName(judgeSamples) == "ErrorInjector.JudgeSamples")
        {
            return Local(judgeSamples) is { } verdicts
                && Asserted(host.Body, t => t.Contains(verdicts + ".First", StringComparison.Ordinal));
        }

        // `verdict is { } failure` whose branch adds it to a collection an Assert reads
        if (verdict.Parent is IsPatternExpressionSyntax { Parent: IfStatementSyntax branch })
        {
            var collections = branch.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" or "Enqueue" or "TryAdd" })
                .Select(i => ((MemberAccessExpressionSyntax)i.Expression).Expression.ToString())
                .ToList();
            return collections.Any(c => Asserted(host.Body, t => t.Contains(c, StringComparison.Ordinal)));
        }

        return Local(verdict) is { } local && Asserted(host.Body, t => t.Contains(local, StringComparison.Ordinal));
    }

    /// <summary>Every <c>new Y("Label", () =&gt; …)</c> in the indexed <paramref name="file"/>: label, line and
    /// the generator lambda (for <see cref="GeneratorCalls"/>).</summary>
    public IEnumerable<(string Label, int Line, ExpressionSyntax Generator)> YRowGenerators(string file)
    {
        var root = _declarations[file].First().SyntaxTree.GetRoot();
        return root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(c => c.Type is IdentifierNameSyntax { Identifier.ValueText: "Y" }
                && c.ArgumentList is { Arguments.Count: >= 2 } args
                && args.Arguments[0].Expression is LiteralExpressionSyntax { Token.Value: string })
            .Select(c => ((string)((LiteralExpressionSyntax)c.ArgumentList!.Arguments[0].Expression).Token.Value!,
                c.SyntaxTree.GetLineSpan(c.Span).StartLinePosition.Line + 1,
                c.ArgumentList.Arguments[1].Expression));
    }

    /// <summary>The local a <c>var x = …</c> declaration binds <paramref name="invocation"/> to, or null.</summary>
    private static string? Local(InvocationExpressionSyntax invocation) =>
        invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            ? declarator.Identifier.ValueText
            : null;

    private static IEnumerable<InvocationExpressionSyntax> AssertionsIn(Method method, SyntaxNode scope) =>
        scope.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Where(IsAssertion);

    private static bool IsAssertion(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Assert" } };

    /// <summary><c>X.Y</c> for a member call on a simple receiver, else the simple name.</summary>
    public static string InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name: var n } =>
            $"{receiver.Identifier.ValueText}.{n.Identifier.ValueText}",
        MemberAccessExpressionSyntax { Name: var n } => n.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        _ => "",
    };

    /// <summary>The invocation nodes of <paramref name="method"/>'s closure.</summary>
    public IEnumerable<InvocationExpressionSyntax> ClosureInvocationNodes(Method method) =>
        Closure(method).SelectMany(m => m.Body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>());

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
