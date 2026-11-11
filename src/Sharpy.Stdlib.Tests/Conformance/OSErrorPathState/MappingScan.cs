using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A2 of plan-638e8b (#2476) — "the mapping is spelled once": a bound scan over the compilations G1
/// builds (<see cref="DoorScan.CoreCompilation"/>, <see cref="DoorScan.StdlibCompilation"/>), judged with
/// G1's axis-1 reach (<see cref="DoorScanResult.Reaching"/> and <see cref="DoorMap"/>). Spy sites are
/// read in their generated C#, which the staleness gate keeps equal to the source.
/// <list type="bullet">
/// <item><b>Raise.</b> The family is read by reflection: Core <c>OSError</c> and every Core type
/// derived from it. Outside <see cref="MappingScanContext.HelperFile"/>, an object creation (explicit or
/// target-typed <c>new</c>) of a type whose base chain holds a Core SUBCLASS is red with no roster; a
/// creation of plain Core <c>OSError</c> is counted per <c>file:Type.Member</c> and judged against the
/// permanent plain-<c>OSError</c> roster.</item>
/// <item><b>Catch.</b> A <c>catch</c> whose <c>try</c> block reaches a door — a reference to an external
/// door member, to a Core member the Core pass found reaching one, or to a source member in the reach
/// (an override or implementation of the referenced member included; a local function's body walked)
/// — and whose caught type can receive an <c>IOException</c>-derived exception or an
/// <c>UnauthorizedAccessException</c> (one of those, a type derived from one, a base of one, or a
/// general catch) is <see cref="CatchShape.Translate"/>, <see cref="CatchShape.Swallow"/> (judged
/// against the swallow roster) or <see cref="CatchShape.Throws"/> (red with no roster).</item>
/// </list>
/// </summary>
internal static class MappingScan
{
    public const string HelperFile = "src/Sharpy.Core/PathErrors.cs";
    public const string HelperType = "Sharpy.PathErrors";
    public const string FamilyAssembly = "Sharpy.Core";

    /// <summary>The Core OSError family, by reflection: (plain name, subclass names).</summary>
    public static (string Plain, IReadOnlySet<string> Subclasses) Family()
    {
        var root = typeof(Sharpy.OSError);
        var subclasses = root.Assembly.GetTypes()
            .Where(t => t != root && root.IsAssignableFrom(t))
            .Select(t => t.FullName!.Replace('+', '.'))
            .ToHashSet(StringComparer.Ordinal);
        return (root.FullName!, subclasses);
    }

    public static MappingScanResult Run(CSharpCompilation compilation, MappingScanContext context)
    {
        var result = new MappingScanResult();
        result.Errors.AddRange(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString()));

        var overriders = Overriders(compilation);
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (string.Equals(tree.FilePath, context.HelperFile, StringComparison.Ordinal))
            {
                result.HelperFileSeen = true;
                continue;
            }
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
                ScanCreation(model, creation, context, result);

            foreach (var tryStatement in root.DescendantNodes().OfType<TryStatementSyntax>())
            {
                if (tryStatement.Catches.Count == 0)
                    continue;
                result.TryBlocks++;
                var witness = ReachesDoor(compilation, model, tryStatement.Block, context, overriders,
                    new HashSet<SyntaxNode>());
                if (witness == null)
                    continue;
                result.TryBlocksReachingDoor++;
                foreach (var clause in tryStatement.Catches)
                {
                    var caught = clause.Declaration == null ? null : model.GetTypeInfo(clause.Declaration.Type).Type;
                    if (clause.Declaration != null && caught == null)
                    {
                        result.Unbound.Add($"{DoorScan.Site(clause)} catch type does not bind");
                        continue;
                    }
                    if (!CanReceiveIoFailure(compilation, caught))
                        continue;
                    result.Catches.Add(new CatchSite(
                        OwnerKey(model, clause), DoorScan.Site(clause),
                        caught == null ? "<general>" : caught.ToDisplayString(DoorScan.TypeFormat),
                        ShapeOf(model, clause, context), witness));
                }
            }
        }
        return result;
    }

    // ------------------------------------------------------------------ raise

    private static void ScanCreation(SemanticModel model, BaseObjectCreationExpressionSyntax creation, MappingScanContext context, MappingScanResult result)
    {
        var type = model.GetTypeInfo(creation).Type;
        if (type == null || type.TypeKind == TypeKind.Error)
        {
            result.Unbound.Add($"{DoorScan.Site(creation)} {creation} — the created type does not bind");
            return;
        }
        if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
            return;

        var key = OwnerKey(model, creation);
        if (IsFamily(named, context.Plain))
        {
            result.PlainCreations.Add(new CreationSite(key, DoorScan.Site(creation), context.Plain));
            return;
        }
        for (var t = named; t != null; t = t.BaseType)
        {
            if (IsFamily(t, null) && context.Subclasses.Contains(DoorScan.TypeKey(t)))
            {
                result.SubclassCreations.Add(new CreationSite(key, DoorScan.Site(creation), DoorScan.TypeKey(named)));
                return;
            }
        }
    }

    private static bool IsFamily(INamedTypeSymbol type, string? name)
        => type.ContainingAssembly?.Name == FamilyAssembly
           && (name == null || DoorScan.TypeKey(type) == name);

    // ------------------------------------------------------------------ catch

    /// <summary>One of IOException / UnauthorizedAccessException, derived from one, a base of one, or a general catch.</summary>
    public static bool CanReceiveIoFailure(Compilation compilation, ITypeSymbol? caught)
    {
        if (caught == null)
            return true; // a general catch
        foreach (var anchorName in new[] { "System.IO.IOException", "System.UnauthorizedAccessException" })
        {
            var anchor = compilation.GetTypeByMetadataName(anchorName)
                         ?? throw new InvalidOperationException($"{anchorName} does not bind in {compilation.AssemblyName}");
            for (var t = caught; t != null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, anchor))
                    return true; // the anchor, or derived from it
            }
            for (var t = (ITypeSymbol?)anchor; t != null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, caught))
                    return true; // a base of the anchor
            }
        }
        return false;
    }

    /// <summary>
    /// <see cref="CatchShape.Translate"/>: there is at least one <c>throw</c> in the body, each is an
    /// ALLOWED throw, and no path leaves the body otherwise (end point unreachable, no exit point). An
    /// allowed throw is <c>throw PathErrors.Translate…(&lt;the caught exception&gt;, …)</c> (a conditional
    /// whose every arm is one counts), or — when the caught type is in the Core <c>OSError</c> family, so
    /// the exception was already classified — a re-raise of it (<c>throw;</c>, <c>throw ex;</c>), which
    /// neither chooses a subclass nor lets a CLR type out. <see cref="CatchShape.Throws"/>: some
    /// <c>throw</c> is anything else (a rethrow of a CLR exception, a new exception, a translate of another
    /// value). <see cref="CatchShape.Swallow"/>: otherwise — on some path the failure is absorbed.
    /// </summary>
    public static CatchShape ShapeOf(SemanticModel model, CatchClauseSyntax clause, MappingScanContext context)
    {
        var throws = clause.Block
            .DescendantNodes(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .Where(n => n is ThrowStatementSyntax or ThrowExpressionSyntax)
            .ToList();
        if (throws.Count == 0)
            return CatchShape.Swallow;

        var local = clause.Declaration?.Identifier.IsKind(SyntaxKind.None) == false
            ? model.GetDeclaredSymbol(clause.Declaration)
            : null;
        var caught = clause.Declaration == null ? null : model.GetTypeInfo(clause.Declaration.Type).Type as INamedTypeSymbol;
        var classified = caught != null && IsFamily(caught, null)
                         && (DoorScan.TypeKey(caught) == context.Plain || context.Subclasses.Contains(DoorScan.TypeKey(caught)));
        foreach (var node in throws)
        {
            var expression = node switch
            {
                ThrowStatementSyntax s => s.Expression,
                ThrowExpressionSyntax e => e.Expression,
                _ => null,
            };
            var allowed = expression == null
                ? classified
                : IsTranslate(model, expression, local, context)
                  || (classified && Strip(expression) is IdentifierNameSyntax rethrown
                      && local != null && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(rethrown).Symbol, local));
            if (!allowed)
                return CatchShape.Throws;
        }

        var flow = model.AnalyzeControlFlow(clause.Block);
        if (flow == null || !flow.Succeeded)
            return CatchShape.Throws;
        return !flow.EndPointIsReachable && flow.ExitPoints.IsEmpty ? CatchShape.Translate : CatchShape.Swallow;
    }

    /// <summary><c>PathErrors.Translate…(&lt;the caught exception&gt;, …)</c>, or a conditional whose every arm is one.</summary>
    private static bool IsTranslate(SemanticModel model, ExpressionSyntax? expression, ILocalSymbol? local, MappingScanContext context)
    {
        if (local == null)
            return false;
        expression = Strip(expression);
        if (expression is ConditionalExpressionSyntax conditional)
            return IsTranslate(model, conditional.WhenTrue, local, context) && IsTranslate(model, conditional.WhenFalse, local, context);
        if (expression is not InvocationExpressionSyntax invocation)
            return false;
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.ContainingType == null
            || DoorScan.TypeKey(method.ContainingType) != context.HelperType
            || !method.Name.StartsWith("Translate", StringComparison.Ordinal))
            return false;
        var first = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        return Strip(first) is IdentifierNameSyntax name
               && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, local);
    }

    private static ExpressionSyntax? Strip(ExpressionSyntax? expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax p:
                    expression = p.Expression;
                    continue;
                case CastExpressionSyntax c:
                    expression = c.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    // ------------------------------------------------------------------ reach of a try block

    /// <summary>The first door reference a block makes (over axis 1's reach), or null.</summary>
    private static string? ReachesDoor(CSharpCompilation compilation, SemanticModel model, SyntaxNode block, MappingScanContext context,
        IReadOnlyDictionary<ISymbol, HashSet<ISymbol>> overriders, HashSet<SyntaxNode> walked)
    {
        if (!walked.Add(block))
            return null;
        foreach (var node in block.DescendantNodesAndSelf())
        {
            ISymbol? symbol = node switch
            {
                BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax or ElementAccessExpressionSyntax
                    => model.GetSymbolInfo(node).Symbol,
                SimpleNameSyntax name when !SyntaxFacts.IsInTypeOnlyContext(name) => model.GetSymbolInfo(name).Symbol,
                _ => null,
            };
            if (symbol is not (IMethodSymbol or IPropertySymbol or IFieldSymbol))
                continue;

            var target = DoorScan.Normalize(symbol);
            if (SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, compilation.Assembly))
            {
                if (target is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
                {
                    foreach (var reference in target.DeclaringSyntaxReferences)
                    {
                        var inner = ReachesDoor(compilation, compilation.GetSemanticModel(reference.SyntaxTree),
                            reference.GetSyntax(), context, overriders, walked);
                        if (inner != null)
                            return inner;
                    }
                    continue;
                }
                var key = DoorScan.MemberKey(target);
                if (context.Reaching.Contains(key))
                    return $"{key} at {DoorScan.Site(node)}";
                if (overriders.TryGetValue(target, out var impls))
                {
                    foreach (var impl in impls)
                    {
                        if (context.Reaching.Contains(DoorScan.MemberKey(impl)))
                            return $"{DoorScan.MemberKey(impl)} (overrides {key}) at {DoorScan.Site(node)}";
                    }
                }
                continue;
            }

            if (IsExternalDoor(target, symbol, node, context))
                return $"{DoorScan.MemberKey(target)} at {DoorScan.Site(node)}";
        }
        return null;
    }

    /// <summary>The external half of axis 1's classification, as <see cref="DoorScan.Run"/> applies it.</summary>
    private static bool IsExternalDoor(ISymbol member, ISymbol bound, SyntaxNode node, MappingScanContext context)
    {
        var type = member.ContainingType;
        if (type == null)
            return false;
        if (context.Options.UpstreamAssembly != null && member.ContainingAssembly?.Name == context.Options.UpstreamAssembly)
            return context.Options.UpstreamDoors.Contains(DoorScan.MemberKey(member));
        if (!context.Map.Types.TryGetValue(DoorScan.TypeKey(type), out var typeRow) || !typeRow.Door)
            return false;
        if (!context.Map.Members.TryGetValue(DoorScan.MemberKey(member), out var memberRow) || !memberRow.IsDoor)
            return false;
        return bound is IMethodSymbol || IsWrite(node);
    }

    private static bool IsWrite(SyntaxNode node)
    {
        var expr = node.Parent is MemberAccessExpressionSyntax access && access.Name == node ? access : node;
        return expr.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expr;
    }

    /// <summary>Every virtual, abstract or interface member → its overrides and implementations in the scanned sources.</summary>
    private static Dictionary<ISymbol, HashSet<ISymbol>> Overriders(CSharpCompilation compilation)
    {
        var map = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
        void Add(ISymbol baseMember, ISymbol member)
        {
            var key = DoorScan.Normalize(baseMember);
            if (!map.TryGetValue(key, out var set))
                map[key] = set = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            set.Add(DoorScan.Normalize(member));
        }
        void Walk(INamespaceOrTypeSymbol container)
        {
            foreach (var type in container.GetTypeMembers())
            {
                if (type.Locations.Any(l => l.IsInSource))
                {
                    foreach (var member in type.GetMembers())
                    {
                        if (member is IMethodSymbol { OverriddenMethod: { } m })
                        {
                            for (var b = m; b != null; b = b.OverriddenMethod)
                                Add(b, member);
                        }
                        else if (member is IPropertySymbol { OverriddenProperty: { } p })
                        {
                            for (var b = p; b != null; b = b.OverriddenProperty)
                                Add(b, member);
                        }
                    }
                    foreach (var iface in type.AllInterfaces)
                    {
                        foreach (var ifaceMember in iface.GetMembers().Where(m => m is IMethodSymbol or IPropertySymbol))
                        {
                            var impl = type.FindImplementationForInterfaceMember(ifaceMember);
                            if (impl != null && SymbolEqualityComparer.Default.Equals(impl.ContainingAssembly, compilation.Assembly))
                                Add(ifaceMember, impl);
                        }
                    }
                }
                Walk(type);
            }
            if (container is INamespaceSymbol ns)
            {
                foreach (var child in ns.GetNamespaceMembers())
                    Walk(child);
            }
        }
        Walk(compilation.Assembly.GlobalNamespace);
        return map;
    }

    // ------------------------------------------------------------------ keys and the verdict

    /// <summary><c>file:Type.Member</c> of the member a node belongs to (lambdas and local functions fold into it).</summary>
    public static string OwnerKey(SemanticModel model, SyntaxNode node)
    {
        ISymbol? owner = null;
        foreach (var ancestor in node.Ancestors())
        {
            owner = ancestor switch
            {
                BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax => model.GetDeclaredSymbol(ancestor),
                VariableDeclaratorSyntax v when v.Parent?.Parent is BaseFieldDeclarationSyntax => model.GetDeclaredSymbol(v),
                _ => null,
            };
            if (owner != null)
                break;
        }
        var file = node.SyntaxTree.FilePath;
        if (owner == null)
            return $"{file}:<no member>";
        var types = new System.Collections.Generic.List<string>();
        for (var t = owner.ContainingType; t != null; t = t.ContainingType)
            types.Insert(0, t.Name);
        var name = owner is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor }
            ? owner.ContainingType!.Name
            : owner.Name;
        return $"{file}:{string.Join(".", types)}.{name}";
    }

    /// <summary>
    /// The verdict over one or more passes: every subclass creation and every throwing non-helper catch is
    /// red; plain-<c>OSError</c> creations and swallows equal their rosters per key and count, both
    /// directions. A roster row's reason may not be empty.
    /// </summary>
    public static System.Collections.Generic.List<string> Judge(IEnumerable<MappingScanResult> passes,
        IReadOnlyList<MappingRow> plainRoster, IReadOnlyList<MappingRow> swallowRoster)
    {
        var problems = new System.Collections.Generic.List<string>();
        var all = passes.ToList();
        foreach (var site in all.SelectMany(p => p.SubclassCreations))
            problems.Add($"raise: {site.Site} ({site.Key}) creates {site.Type} — an OSError subclass is chosen only by {HelperType} (no roster)");
        foreach (var site in all.SelectMany(p => p.Catches).Where(c => c.Shape == CatchShape.Throws))
            problems.Add($"catch: {site.Site} ({site.Key}) catch ({site.Caught}) around a door ({site.Witness}) throws something other than `throw {HelperType}.Translate…(ex, path)` (no roster)");

        Compare("raise: plain OSError", all.SelectMany(p => p.PlainCreations).Select(s => (s.Key, s.Site)), plainRoster, problems);
        Compare("catch: swallow", all.SelectMany(p => p.Catches).Where(c => c.Shape == CatchShape.Swallow).Select(s => (s.Key, s.Site)), swallowRoster, problems);
        return problems;
    }

    private static void Compare(string label, IEnumerable<(string Key, string Site)> found, IReadOnlyList<MappingRow> roster, System.Collections.Generic.List<string> problems)
    {
        var byKey = found.GroupBy(f => f.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Site).ToList(), StringComparer.Ordinal);
        var rows = new Dictionary<string, MappingRow>(StringComparer.Ordinal);
        foreach (var row in roster)
        {
            if (!rows.TryAdd(row.Key, row))
                problems.Add($"{label}: duplicate roster row {row.Key}");
            if (string.IsNullOrWhiteSpace(row.Reason))
                problems.Add($"{label}: roster row {row.Key} needs its reason");
        }
        foreach (var (key, sites) in byKey.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (!rows.TryGetValue(key, out var row))
                problems.Add($"{label}: unlisted {key} ×{sites.Count} at {string.Join(", ", sites)}");
            else if (row.Count != sites.Count)
                problems.Add($"{label}: {key} has {sites.Count} site(s) ({string.Join(", ", sites)}); the roster row says {row.Count}");
        }
        foreach (var key in rows.Keys.Where(k => !byKey.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
            problems.Add($"{label}: stale roster row {key} — the scan finds no such site");
    }
}

/// <summary>One pass's inputs: axis 1's map, upstream doors and reach (by member key), the family, the helper.</summary>
internal sealed record MappingScanContext(DoorMap Map, DoorScanOptions Options, IReadOnlySet<string> Reaching,
    string Plain, IReadOnlySet<string> Subclasses, string HelperFile = MappingScan.HelperFile, string HelperType = MappingScan.HelperType);

internal enum CatchShape
{
    Translate,
    Swallow,
    Throws,
}

internal sealed record CreationSite(string Key, string Site, string Type);

internal sealed record CatchSite(string Key, string Site, string Caught, CatchShape Shape, string Witness);

/// <summary>A permanent roster row: <c>file:Type.Member</c>, how many sites it holds, and why each is not path state.</summary>
internal sealed record MappingRow(string Key, int Count, string Reason);

internal sealed class MappingScanResult
{
    public System.Collections.Generic.List<string> Errors { get; } = new();
    public System.Collections.Generic.List<string> Unbound { get; } = new();
    public System.Collections.Generic.List<CreationSite> SubclassCreations { get; } = new();
    public System.Collections.Generic.List<CreationSite> PlainCreations { get; } = new();
    public System.Collections.Generic.List<CatchSite> Catches { get; } = new();
    public int TryBlocks { get; set; }
    public int TryBlocksReachingDoor { get; set; }
    public bool HelperFileSeen { get; set; }
}
