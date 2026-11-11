using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// G1 axis 1 (plan-638e8b C2): the bound external-callee census and the reach over the bound call
/// graph — the ONE classifier the Core pass, the Stdlib pass and every synthetic control run through.
///
/// <para><b>References.</b> Every invocation, object creation (explicit, implicit <c>new(…)</c>,
/// constructor initializer, primary-constructor base), property or field read, property or field
/// write, indexer access and method group in the scanned sources, bound with the compilation's
/// semantic model. A candidate that does not bind is recorded in <see cref="DoorScanResult.Unbound"/>
/// — red, never a skip. A reference whose bound symbol is declared outside the scanned sources is
/// <i>external</i> and is classified by <see cref="DoorMap"/> (or, for a symbol of
/// <see cref="DoorScanOptions.UpstreamAssembly"/>, by the upstream pass's reach); a reference to a
/// source member is a call-graph edge.</para>
///
/// <para><b>Reach.</b> A lambda or local function belongs to its enclosing member; a reference to a
/// virtual, abstract or interface member is also an edge to every override and implementation in the
/// scanned sources; every constructor reaches its type's instance field and property initializers.
/// The reach is an over-approximation on purpose.</para>
/// </summary>
internal static class DoorScan
{
    public const string Invocation = "invocation";
    public const string Creation = "object creation";
    public const string PropertyRead = "property read";
    public const string PropertyWrite = "property write";
    public const string MethodGroup = "method group";

    /// <summary>The display of a member key: containing type, name, parameter types.</summary>
    internal static readonly SymbolDisplayFormat MemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    public static string MemberKey(ISymbol member) => Normalize(member).ToDisplayString(MemberFormat);

    public static string TypeKey(INamedTypeSymbol type) => type.OriginalDefinition.ToDisplayString(TypeFormat);

    public static string NamespaceKey(INamedTypeSymbol type)
    {
        var outer = type;
        while (outer.ContainingType != null)
            outer = outer.ContainingType;
        return outer.ContainingNamespace?.IsGlobalNamespace != false ? "<global>" : outer.ContainingNamespace.ToDisplayString();
    }

    /// <summary>The definition a reference is keyed on: original definition, extension form, property for an accessor.</summary>
    public static ISymbol Normalize(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            if (method.ReducedFrom != null)
                method = method.ReducedFrom;
            if (method.AssociatedSymbol is IPropertySymbol or IEventSymbol)
                return method.AssociatedSymbol.OriginalDefinition;
            return method.OriginalDefinition;
        }
        return symbol.OriginalDefinition;
    }

    /// <summary>A type a path can be spelled in: <c>string</c>, <c>object</c>, or a sequence of either.</summary>
    public static bool IsPathCarrier(ITypeSymbol? type)
    {
        if (type == null)
            return false;
        if (type.SpecialType is SpecialType.System_String or SpecialType.System_Object)
            return true;
        if (type is IArrayTypeSymbol array)
            return array.ElementType.SpecialType is SpecialType.System_String or SpecialType.System_Object;
        var sequences = type.AllInterfaces.Concat(type is INamedTypeSymbol n ? new[] { n } : Array.Empty<INamedTypeSymbol>());
        foreach (var candidate in sequences)
        {
            if (candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && candidate.TypeArguments[0].SpecialType is SpecialType.System_String or SpecialType.System_Object)
                return true;
        }
        if (type is INamedTypeSymbol span && span.Name is "ReadOnlySpan" or "Span" && span.TypeArguments.Length == 1
            && span.TypeArguments[0].SpecialType is SpecialType.System_String or SpecialType.System_Object)
            return true;
        return false;
    }

    /// <summary>
    /// Path-capable, read off the BOUND symbol: a parameter, or the property or field written, has a
    /// path-carrier type. A read is never path-capable.
    /// </summary>
    public static bool IsPathCapable(ISymbol bound, string kind) => bound switch
    {
        IMethodSymbol m => m.Parameters.Any(p => IsPathCarrier(p.Type)),
        IPropertySymbol p when p.IsIndexer => p.Parameters.Any(x => IsPathCarrier(x.Type)) || (kind == PropertyWrite && IsPathCarrier(p.Type)),
        IPropertySymbol p => kind == PropertyWrite && IsPathCarrier(p.Type),
        IFieldSymbol f => kind == PropertyWrite && IsPathCarrier(f.Type),
        _ => false,
    };

    public static DoorScanResult Run(CSharpCompilation compilation, DoorMap map, DoorScanOptions options)
    {
        var result = new DoorScanResult { Files = compilation.SyntaxTrees.Count() };
        result.Errors.AddRange(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString()));

        var sourceAssembly = compilation.Assembly;
        bool InSource(ISymbol s) => SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, sourceAssembly);

        // ---- Overrides and implementations in the scanned sources ----
        var overriders = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
        void AddOverrider(ISymbol baseMember, ISymbol member)
        {
            var key = Normalize(baseMember);
            if (!overriders.TryGetValue(key, out var set))
                overriders[key] = set = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            set.Add(Normalize(member));
        }

        var sourceTypes = new System.Collections.Generic.List<INamedTypeSymbol>();
        void CollectTypes(INamespaceOrTypeSymbol container)
        {
            foreach (var t in container.GetTypeMembers())
            {
                if (t.Locations.Any(l => l.IsInSource))
                    sourceTypes.Add(t);
                CollectTypes(t);
            }
            if (container is INamespaceSymbol ns)
            {
                foreach (var child in ns.GetNamespaceMembers())
                    CollectTypes(child);
            }
        }
        CollectTypes(sourceAssembly.GlobalNamespace);

        foreach (var type in sourceTypes)
        {
            foreach (var member in type.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { OverriddenMethod: { } o }:
                        for (var b = o; b != null; b = b.OverriddenMethod)
                            AddOverrider(b, member);
                        break;
                    case IPropertySymbol { OverriddenProperty: { } p }:
                        for (var b = p; b != null; b = b.OverriddenProperty)
                            AddOverrider(b, member);
                        break;
                }
            }
            foreach (var iface in type.AllInterfaces)
            {
                foreach (var ifaceMember in iface.GetMembers())
                {
                    if (ifaceMember is not (IMethodSymbol or IPropertySymbol))
                        continue;
                    var impl = type.FindImplementationForInterfaceMember(ifaceMember);
                    if (impl != null && InSource(impl))
                        AddOverrider(ifaceMember, impl);
                }
            }
        }

        // ---- References, per owning member ----
        var edges = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
        var owners = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        HashSet<ISymbol> EdgesOf(ISymbol owner)
        {
            owners.Add(owner);
            if (!edges.TryGetValue(owner, out var set))
                edges[owner] = set = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            return set;
        }

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                if (node.Ancestors().Any(a => a is AttributeListSyntax))
                    continue;

                var (symbol, kind, reported) = BindReference(model, node, result);
                if (reported || symbol == null)
                    continue;

                var owner = OwnerOf(model, node);
                if (owner == null)
                {
                    result.Unbound.Add($"{Site(node)} {Clip(node)} — no owning member");
                    continue;
                }

                var target = Normalize(symbol);
                var ownerEdges = EdgesOf(owner);
                if (InSource(target))
                {
                    if (target is IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction })
                        continue; // belongs to its enclosing member, already walked
                    ownerEdges.Add(target);
                }
                else
                {
                    var reference = new ExternalReference(owner, target, symbol, kind, IsPathCapable(symbol, kind), Site(node));
                    result.External.Add(reference);
                }

                if (overriders.TryGetValue(target, out var impls))
                    ownerEdges.UnionWith(impls);
            }
        }

        // Each constructor reaches its type's instance field and property initializers.
        foreach (var type in sourceTypes)
        {
            var initializers = type.GetMembers()
                .Where(m => m is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false })
                .Where(owners.Contains)
                .ToList();
            if (initializers.Count == 0)
                continue;
            foreach (var ctor in type.InstanceConstructors)
                EdgesOf(ctor).UnionWith(initializers);
        }

        result.Edges = edges;

        // ---- Classification of external references ----
        var doorRefs = new Dictionary<ISymbol, System.Collections.Generic.List<ExternalReference>>(SymbolEqualityComparer.Default);
        foreach (var reference in result.External)
        {
            var member = reference.Member;
            var type = member.ContainingType;
            if (type == null)
                continue;

            var assembly = member.ContainingAssembly?.Name;
            if (options.UpstreamAssembly != null && assembly == options.UpstreamAssembly)
            {
                if (options.UpstreamDoors.Contains(MemberKey(member)))
                    AddDoor(doorRefs, reference);
                continue;
            }

            var typeKey = TypeKey(type);
            var ns = NamespaceKey(type);
            var hasType = map.Types.TryGetValue(typeKey, out var typeRow);
            var hasNamespace = map.Namespaces.ContainsKey(ns);
            if (hasType && hasNamespace)
            {
                result.DoubleClassified.Add(typeKey);
                continue;
            }

            if (hasNamespace)
            {
                result.SeenNamespaceRows.Add(ns);
                if (reference.PathCapable)
                    result.Census.Add(("namespace " + ns, MemberKey(member)));
                continue;
            }

            if (!hasType)
            {
                result.Unclassified.Add($"{typeKey} (first reference {reference.Site}: {reference.Kind} {MemberKey(member)})");
                result.UnclassifiedTypes.Add(typeKey);
                continue;
            }

            result.SeenTypeRows.Add(typeKey);
            if (!typeRow!.Door)
            {
                if (reference.PathCapable)
                    result.Census.Add(("type " + typeKey, MemberKey(member)));
                continue;
            }

            var memberKey = MemberKey(member);
            if (!map.Members.TryGetValue(memberKey, out var memberRow))
            {
                result.MissingMemberRows.Add($"{memberKey} (door type {typeKey}; first reference {reference.Site}: {reference.Kind})");
                continue;
            }

            result.SeenMemberRows.Add(memberKey);
            // A property or field row's path is the value WRITTEN; reading it back opens nothing.
            if (memberRow.IsDoor && (member is IMethodSymbol || reference.Kind == PropertyWrite))
                AddDoor(doorRefs, reference);
        }

        // ---- Reach: every source member from which a door reference is reachable ----
        var reverse = new Dictionary<ISymbol, System.Collections.Generic.List<ISymbol>>(SymbolEqualityComparer.Default);
        foreach (var (from, tos) in edges)
        {
            foreach (var to in tos)
            {
                if (!reverse.TryGetValue(to, out var list))
                    reverse[to] = list = new System.Collections.Generic.List<ISymbol>();
                list.Add(from);
            }
        }

        var witness = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var queue = new Queue<ISymbol>();
        foreach (var (owner, refs) in doorRefs)
        {
            witness[owner] = MemberKey(refs[0].Member) + " at " + refs[0].Site;
            queue.Enqueue(owner);
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reverse.TryGetValue(current, out var callers))
                continue;
            foreach (var caller in callers)
            {
                if (witness.ContainsKey(caller))
                    continue;
                witness[caller] = MemberKey(current) + " → " + witness[current];
                queue.Enqueue(caller);
            }
        }

        foreach (var (member, path) in witness)
        {
            var key = MemberKey(member);
            result.Reaching[key] = path;
            if (IsEntryPoint(member))
                result.EntryPoints[key] = new EntryPoint(key, member, path);
        }

        return result;
    }

    private static void AddDoor(Dictionary<ISymbol, System.Collections.Generic.List<ExternalReference>> doorRefs, ExternalReference reference)
    {
        if (!doorRefs.TryGetValue(reference.Owner, out var list))
            doorRefs[reference.Owner] = list = new System.Collections.Generic.List<ExternalReference>();
        list.Add(reference);
    }

    /// <summary>A public member of a public type (every containing type public).</summary>
    public static bool IsEntryPoint(ISymbol member)
    {
        if (member.DeclaredAccessibility != Accessibility.Public)
            return false;
        if (member is IMethodSymbol { MethodKind: MethodKind.StaticConstructor })
            return false;
        for (var t = member.ContainingType; t != null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility != Accessibility.Public)
                return false;
        }
        return true;
    }

    /// <summary>The member a node's references belong to; lambdas and local functions fold into it.</summary>
    private static ISymbol? OwnerOf(SemanticModel model, SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax:
                    return model.GetDeclaredSymbol(ancestor) is { } s ? Normalize(s) : null;
                case VariableDeclaratorSyntax v when v.Parent?.Parent is BaseFieldDeclarationSyntax:
                    return model.GetDeclaredSymbol(v);
                case EnumMemberDeclarationSyntax e:
                    return model.GetDeclaredSymbol(e);
                case TypeDeclarationSyntax t when t.ParameterList != null:
                    // A primary constructor's base arguments.
                    return (model.GetDeclaredSymbol(t) as INamedTypeSymbol)?.InstanceConstructors
                        .FirstOrDefault(c => c.Parameters.Length == t.ParameterList.Parameters.Count
                                             && !c.IsImplicitlyDeclared);
            }
        }
        return null;
    }

    /// <summary>
    /// The bound member a node references and its kind, or null when the node is not a reference
    /// candidate. A candidate that binds to nothing is recorded as unbound (reported = true).
    /// </summary>
    private static (ISymbol? Symbol, string Kind, bool Reported) BindReference(SemanticModel model, SyntaxNode node, DoorScanResult result)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax:
                {
                    var info = model.GetSymbolInfo(node);
                    if (info.Symbol is IMethodSymbol ctor)
                        return (ctor, Creation, false);
                    // Delegate creation `new Func<int>(M)` binds to no constructor; its method group is walked.
                    if (model.GetTypeInfo(node).Type is { TypeKind: TypeKind.Delegate })
                        return (null, "", false);
                    // A creation of a type parameter (`new T()`) has no constructor to bind.
                    if (model.GetTypeInfo(node).Type is { TypeKind: TypeKind.TypeParameter })
                        return (null, "", false);
                    return Unbound(node, info, result);
                }
            case ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax:
                {
                    var info = model.GetSymbolInfo(node);
                    return info.Symbol is IMethodSymbol ctor ? (ctor, Creation, false) : Unbound(node, info, result);
                }
            case ElementAccessExpressionSyntax or ElementBindingExpressionSyntax:
                {
                    var info = model.GetSymbolInfo(node);
                    if (info.Symbol is IPropertySymbol indexer)
                        return (indexer, IsWritten(node) ? PropertyWrite : PropertyRead, false);
                    if (info.Symbol == null && info.CandidateSymbols.Length > 0)
                        return Unbound(node, info, result);
                    return (null, "", false); // array element, pointer element: no member
                }
            case SimpleNameSyntax name:
                return BindName(model, name, result);
            default:
                return (null, "", false);
        }
    }

    private static (ISymbol?, string, bool) BindName(SemanticModel model, SimpleNameSyntax name, DoorScanResult result)
    {
        // Names that are not expressions: declarations, type syntax, qualified type names, nameof.
        if (SyntaxFacts.IsInTypeOnlyContext(name) || name.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax
            or NameColonSyntax or NameEqualsSyntax or ExplicitInterfaceSpecifierSyntax or TypeConstraintSyntax
            or UsingDirectiveSyntax or ExternAliasDirectiveSyntax)
            return (null, "", false);
        if (name.Ancestors().Any(a => a is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } inv
                                      && inv.ArgumentList.Span.Contains(name.Span)))
            return (null, "", false);
        if (name is IdentifierNameSyntax { Identifier.Text: "nameof" } && name.Parent is InvocationExpressionSyntax)
            return (null, "", false);

        // The expression this name completes: `x.Name`, `x?.Name`, or the bare name.
        ExpressionSyntax expr = name;
        if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
            expr = access;
        else if (name.Parent is MemberBindingExpressionSyntax binding && binding.Name == name)
            expr = binding;
        else if (name.Parent is MemberAccessExpressionSyntax { Expression: var left } && left == name)
            expr = name;

        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol;
        if (symbol == null)
        {
            if (info.CandidateSymbols.Length == 0 && info.CandidateReason == CandidateReason.None)
            {
                // A name the binder answers with nothing at all: `var`, a discard `_`, a dynamic member.
                if (name.IsVar || name is IdentifierNameSyntax { Identifier.Text: "_" })
                    return (null, "", false);
                if (model.GetTypeInfo(expr).Type is { TypeKind: TypeKind.Dynamic })
                    return Unbound(name, info, result);
                return Unbound(name, info, result);
            }
            return Unbound(name, info, result);
        }

        switch (symbol)
        {
            case IMethodSymbol method:
                if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
                    return (method, Invocation, false);
                if (method.MethodKind == MethodKind.DelegateInvoke)
                    return (null, "", false);
                var invoked = expr.Parent is InvocationExpressionSyntax inv && inv.Expression == expr;
                return (method, invoked ? Invocation : MethodGroup, false);
            case IPropertySymbol property:
                return (property, IsWritten(expr) ? PropertyWrite : PropertyRead, false);
            case IFieldSymbol field:
                if (field.ContainingType?.TypeKind == TypeKind.Enum || field.IsConst)
                    return (field, PropertyRead, false);
                return (field, IsWritten(expr) ? PropertyWrite : PropertyRead, false);
            case IEventSymbol evt:
                return (evt, PropertyWrite, false);
            default:
                return (null, "", false); // locals, parameters, types, namespaces, labels, range variables
        }
    }

    /// <summary>The target of an assignment (compound included), an initializer slot, <c>++</c>/<c>--</c>, or a ref/out argument.</summary>
    private static bool IsWritten(SyntaxNode expr)
    {
        var node = expr;
        while (node.Parent is ConditionalAccessExpressionSyntax c && c.WhenNotNull == node)
            node = c;
        return node.Parent switch
        {
            AssignmentExpressionSyntax a => a.Left == node,
            PrefixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PreIncrementExpression) || p.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax p => p.IsKind(SyntaxKind.PostIncrementExpression) || p.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax arg => arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            TupleExpressionSyntax or ArgumentSyntax => false,
            _ => false,
        };
    }

    private static (ISymbol?, string, bool) Unbound(SyntaxNode node, SymbolInfo info, DoorScanResult result)
    {
        result.Unbound.Add($"{Site(node)} {Clip(node)} — {info.CandidateReason}"
                           + (info.CandidateSymbols.Length > 0 ? " [" + string.Join(", ", info.CandidateSymbols.Take(3)) + "]" : ""));
        return (null, "", true);
    }

    private static string Clip(SyntaxNode node)
    {
        var text = node.ToString().Replace('\n', ' ');
        return text.Length > 80 ? text[..80] + "…" : text;
    }

    public static string Site(SyntaxNode node)
        => $"{node.SyntaxTree.FilePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    // ---- The two real compilations ----

    internal static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview,
        preprocessorSymbols: new[] { "NET", "NET10_0", "NET10_0_OR_GREATER", "NETCOREAPP", "RELEASE" });

    internal static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable,
        allowUnsafe: true);

    /// <summary>The Stdlib exactly as <see cref="StdlibModuleGlobalRosterTests"/> builds it.</summary>
    public static CSharpCompilation StdlibCompilation()
    {
        var references = FrameworkReferences().ToList();
        var frameworkDir = RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in typeof(Sharpy.NumpyRandom).Assembly.GetReferencedAssemblies())
        {
            var location = Assembly.Load(name).Location;
            if (!location.StartsWith(frameworkDir, StringComparison.Ordinal))
                references.Add(MetadataReference.CreateFromFile(location));
        }
        return CSharpCompilation.Create("Sharpy.Stdlib", SourceTrees("Sharpy.Stdlib", "bin", "obj", "spy", "modules"), references, CompilationOptions);
    }

    /// <summary><c>src/Sharpy.Core</c> from source: the net10.0 symbol view, the framework, Core's own references.</summary>
    public static CSharpCompilation CoreCompilation()
    {
        var references = FrameworkReferences().ToList();
        var frameworkDir = RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in typeof(Sharpy.Builtins).Assembly.GetReferencedAssemblies())
        {
            var location = Assembly.Load(name).Location;
            if (!location.StartsWith(frameworkDir, StringComparison.Ordinal))
                references.Add(MetadataReference.CreateFromFile(location));
        }
        return CSharpCompilation.Create("Sharpy.Core", SourceTrees("Sharpy.Core", "bin", "obj"), references, CompilationOptions);
    }

    private static System.Collections.Generic.List<SyntaxTree> SourceTrees(string project, params string[] excludedDirs)
    {
        var root = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", project);
        var excluded = excludedDirs.Select(d => SysPath.Combine(root, d) + SysPath.DirectorySeparatorChar).ToArray();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !excluded.Any(x => f.StartsWith(x, StringComparison.Ordinal)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => CSharpSyntaxTree.ParseText(
                File.ReadAllText(f), ParseOptions,
                path: SysPath.GetRelativePath(FixtureRoots.RepositoryRoot, f).Replace('\\', '/')))
            .ToList();
    }

    internal static IEnumerable<MetadataReference> FrameworkReferences()
        => Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Where(IsManagedAssembly)
            .Select(f => MetadataReference.CreateFromFile(f));

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}

/// <summary>Options of one pass: the upstream assembly whose members are doors by the upstream pass's reach.</summary>
internal sealed record DoorScanOptions(string? UpstreamAssembly, IReadOnlySet<string> UpstreamDoors)
{
    public static readonly DoorScanOptions None = new(null, new HashSet<string>());
}

/// <summary>One external reference: the owning source member, the bound member's definition, its kind and site.</summary>
internal sealed record ExternalReference(ISymbol Owner, ISymbol Member, ISymbol Bound, string Kind, bool PathCapable, string Site);

/// <summary>A public member of a public type from which a door is reachable, with one witness chain.</summary>
internal sealed record EntryPoint(string Key, ISymbol Symbol, string Witness);

internal sealed class DoorScanResult
{
    public int Files { get; init; }
    public System.Collections.Generic.List<string> Errors { get; } = new();
    public System.Collections.Generic.List<string> Unbound { get; } = new();
    public System.Collections.Generic.List<ExternalReference> External { get; } = new();

    /// <summary>T1: external types no namespace or type row covers.</summary>
    public System.Collections.Generic.List<string> Unclassified { get; } = new();
    public SortedSet<string> UnclassifiedTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>A type covered by both a namespace row and a type row (ambiguous).</summary>
    public SortedSet<string> DoubleClassified { get; } = new(StringComparer.Ordinal);

    /// <summary>T2: referenced members of door types with no member row.</summary>
    public System.Collections.Generic.List<string> MissingMemberRows { get; } = new();

    /// <summary>T2: (negative row, path-capable member) pairs the scan found.</summary>
    public SortedSet<(string Row, string Member)> Census { get; } = new();

    public HashSet<string> SeenNamespaceRows { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SeenTypeRows { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SeenMemberRows { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The bound call graph over the scanned sources: owning member → the source members it references
    /// (overrides and implementations of a virtual or interface target included; every constructor →
    /// its type's instance initializers). R2 propagates PathErrors stamps over it.
    /// </summary>
    public IReadOnlyDictionary<ISymbol, HashSet<ISymbol>> Edges { get; set; } = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

    /// <summary>Every source member (any accessibility) reaching a door → witness chain.</summary>
    public SortedDictionary<string, string> Reaching { get; } = new(StringComparer.Ordinal);

    /// <summary>The public members of public types among <see cref="Reaching"/>.</summary>
    public SortedDictionary<string, EntryPoint> EntryPoints { get; } = new(StringComparer.Ordinal);
}
