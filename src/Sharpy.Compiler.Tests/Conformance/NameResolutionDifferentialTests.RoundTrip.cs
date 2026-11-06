using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure;
using Xunit;
using ParameterModifier = Sharpy.Compiler.Parser.Ast.ParameterModifier;
using ScopeKind = Sharpy.Compiler.Semantic.SymbolTable.ScopeKind;
using SymbolKind = Sharpy.Compiler.Semantic.SymbolKind;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// P77b D3.1: roster CONTENT by round trip. The totality facts prove every source value is mapped; they cannot see a
/// value mapped to the WRONG kind (the verify round swapped <c>TypeKind.Struct</c> and <c>Enum</c>, two binder images, a
/// flag and a statement value and stayed green). Here each kind's declaration, spelled by the generator's own
/// <c>ModuleDecl</c> / <c>MemberDecl</c> / <c>LocalDecl</c>, is analyzed in process and the symbol the compiler binds is
/// read back through the rosters' PREIMAGE: the inverse of <c>SymbolKinds</c> (with its refinements), <c>TypeKinds</c>,
/// <c>MemberKinds</c>, <c>ScopeSplit</c> and the variable / function cubes. <c>StoreOuter</c> / <c>StoreBodies</c> /
/// <c>LocalOuter</c> / <c>LocalBodies</c> / <c>ReadOuter</c> drive the generator, so a swapped entry there moves the
/// census (<c>Census_TheGeneratedPool_IsPinned</c>, the existing catcher) and is not read here.
/// </summary>
public partial class NameResolutionDifferentialTests
{
    /// <summary>The kinds round-tripped: every <see cref="BindingKind"/> but <c>None</c>, the one kind with no symbol.</summary>
    private const int RoundTrippedKindCount = 33;

    /// <summary>One kind's declaration: the program, the name it binds, and the type whose member it is (null: not a member).</summary>
    private sealed record RoundTripDecl(BindingKind Kind, string Source, string Name, string? Owner = null);

    private static IEnumerable<RoundTripDecl> RoundTripDecls()
    {
        var n = ReadName.Plain;
        string Program(IEnumerable<string> lines) => WithHelpers(Join(lines));

        foreach (var k in NameResolutionRosters.ModuleLevelKinds)
        {
            var decl = k switch
            {
                BindingKind.ModuleUnion => UnionDecl.ToList(),
                BindingKind.ModuleInterface => InterfaceDecl.ToList(),
                _ => ModuleDecl(k, n),
            };
            yield return new RoundTripDecl(k, Program(decl), n.Name);
        }

        foreach (var k in NameResolutionRosters.TypeLevelKinds.Concat(NameResolutionRosters.InstanceKinds))
        {
            var members = k == BindingKind.Constructor
                ? new List<string> { "def __init__(self) -> None:", "    pass" }
                : MemberDecl(k, n);
            yield return new RoundTripDecl(k, Program(new[] { "class H:" }.Concat(Indent(members))), k == BindingKind.Constructor ? "__init__" : n.Name, "H");
        }

        foreach (var k in NameResolutionRosters.PositionLocalKinds)
        {
            // The three kinds no declaration statement binds take the generator's own spellings of them (ClassLocalOuterCell).
            var body = k switch
            {
                BindingKind.EnclosingLocal => new List<string> { "def f() -> None:", "    x: int = 13", "    def g() -> int:", "        return x" },
                BindingKind.Parameter => new List<string> { "def f(x: int) -> int:", "    return x" },
                BindingKind.Inner => new List<string> { "def f() -> int:", "    return [x for x in [22]][0]" },
                _ => new[] { "def f() -> None:" }.Concat(Indent(LocalDecl(k, n))).ToList(),
            };
            yield return new RoundTripDecl(k, Program(body), n.Name);
        }

        yield return new RoundTripDecl(BindingKind.Builtin, MainBlock("print(abs(-3))"), "abs");
        yield return new RoundTripDecl(BindingKind.ImportedName, WithImport(Imported, MainBlock("print(len(digits))")), Imported.Name);
    }

    /// <summary>The kinds a roster value maps to: its image, or the image of the roster it is refined by.</summary>
    private static IReadOnlySet<BindingKind> EntryImage(RosterEntry<BindingKind> entry) => entry.RefinedBy switch
    {
        null => entry.Image.ToHashSet(),
        nameof(NameResolutionRosters.VariableCube) => Roster.ImageOf(NameResolutionRosters.VariableCube).ToHashSet(),
        nameof(NameResolutionRosters.FunctionCube) => Roster.ImageOf(NameResolutionRosters.FunctionCube).ToHashSet(),
        nameof(NameResolutionRosters.TypeKinds) => Roster.ImageOf(NameResolutionRosters.TypeKinds).ToHashSet(),
        nameof(NameResolutionRosters.ParameterModifiers) => NameResolutionRosters.ParameterModifiers.Values.SelectMany(EntryImage).ToHashSet(),
        var site when NameResolutionRosters.BinderSites.TryGetValue(site, out var image) => image.Declares.ToHashSet(),
        var other => throw new InvalidOperationException($"refined by {other}, which the round trip cannot read"),
    };

    /// <summary>The roster's preimage of a kind: every source value whose entry maps to it.</summary>
    private static IReadOnlySet<TKey> Preimage<TKey>(IReadOnlyDictionary<TKey, RosterEntry<BindingKind>> roster, BindingKind k)
        where TKey : notnull
        => roster.Where(e => EntryImage(e.Value).Contains(k)).Select(e => e.Key).ToHashSet();

    /// <summary>The flag that decides a variable's cube cell, most specific first: a module property is also static
    /// (<c>VariableCube</c>'s Module × IsStatic reason), a const is also type-level.</summary>
    private static string DecidingFlag(VariableSymbol v)
        => v.IsParameter ? "IsParameter"
            : v.IsModuleProperty ? "IsModuleProperty"
            : v.IsConstant ? "IsConstant"
            : v.IsStatic ? "IsStatic"
            : NameResolutionRosters.NoFlag;

    private static string FunctionClass(FunctionSymbol f)
        => f.Name == "__init__" ? "__init__" : f.IsStatic ? "IsStatic" : NameResolutionRosters.NoFlag;

    /// <summary>The symbol bound for a declaration and where: its defining scope, and its member kind when it is a member.</summary>
    private sealed record Observed(Symbol? Symbol, ScopeKind? Scope, TypeBodyMemberKind? Member, string Where);

    private static Observed? Observe(SymbolTable table, RoundTripDecl d)
    {
        var scopes = new List<Scope>();
        // Scope ids are allocated consecutively (SymbolTable._nextScopeId); GetScope is the compiler's own lookup by id.
        for (int id = 0, misses = 0; misses < 64; id++)
        {
            if (table.GetScope(id) is { } scope)
            {
                scopes.Add(scope);
                misses = 0;
            }
            else
            {
                misses++;
            }
        }
        var module = scopes.First(s => s.Name.StartsWith("module:", StringComparison.Ordinal));

        if (d.Kind == BindingKind.Builtin)
        {
            var builtin = table.GlobalScope.Lookup(d.Name, searchParent: false);
            return builtin == null ? null : new Observed(builtin, SymbolTable.ClassifyScope(table.GlobalScope.Name), null, "the global scope");
        }

        if (d.Owner != null)
        {
            if (module.Lookup(d.Owner, searchParent: false) is not TypeSymbol owner)
                return null;
            var ownerScope = SymbolTable.ClassifyScope($"class:{owner.Name}");
            if (TypeBodyMembership.DescribeMemberOf(owner, d.Name, reachableThroughSelf: false, isInherited: false) is { } member)
                return new Observed(member.Symbol, ownerScope, member.Kind, $"{owner.Name}'s {member.Kind}");
            Symbol? other = owner.NestedTypes.FirstOrDefault(t => t.Name == d.Name)
                ?? (Symbol?)owner.Constructors.FirstOrDefault(c => c.Name == d.Name);
            return other == null ? null : new Observed(other, ownerScope, null, $"{owner.Name}'s nested types / constructors");
        }

        if (module.Lookup(d.Name, searchParent: false) is { } top)
            return new Observed(top, SymbolTable.ClassifyScope(module.Name), null, module.Name);
        foreach (var scope in scopes.Where(s => SymbolTable.ClassifyScope(s.Name) is not (ScopeKind.Module or ScopeKind.TypeBody)))
        {
            if (scope.Lookup(d.Name, searchParent: false) is { } local)
                return new Observed(local, SymbolTable.ClassifyScope(scope.Name), null, scope.Name);
        }
        return null;
    }

    private static IEnumerable<string> RoundTripProblems(RoundTripDecl d, Observed o, SymbolTable table)
    {
        var k = d.Kind;
        string Tuple() => $"{o.Symbol?.GetType().Name ?? "no Symbol"} {o.Symbol?.Kind} in {o.Scope} ({o.Where})"
            + (o.Symbol is VariableSymbol v ? $" flags[{string.Join(",", NameResolutionRosters.VariableCubeFlags.Where(f => f != NameResolutionRosters.NoFlag && FlagOf(v, f)))}]" : "")
            + (o.Symbol is TypeSymbol t ? $" {t.TypeKind}" : "")
            + (o.Member is { } m ? $" member {m}" : "");

        var scopes = Preimage(NameResolutionRosters.ScopeSplit, k);
        if (o.Scope is not ScopeKind scope || !scopes.Contains(scope))
            yield return $"{k}: bound {Tuple()}; ScopeSplit's preimage is {{{string.Join(", ", scopes)}}}";

        // The named outers' source is the binder census, not the symbol rosters (BindingKindRoster_EveryKind_IsTheImageOfASource):
        // a builtin is a global-scope symbol, an imported name a module symbol its import binding recorded.
        if (k == BindingKind.Builtin)
            yield break;
        if (k == BindingKind.ImportedName)
        {
            var module = table.GetModuleScopeNames().Select(table.GetModuleScope).FirstOrDefault(s => s?.Lookup(d.Name, false) != null);
            if (module?.LookupImportBinding(d.Name) == null)
                yield return $"{k}: {d.Name} is bound with no import binding recorded on its module scope";
            yield break;
        }

        var kinds = Preimage(NameResolutionRosters.SymbolKinds, k);
        SymbolKind? observedKind = o.Symbol?.Kind;
        if (observedKind is SymbolKind sk ? !kinds.Contains(sk) : kinds.Count > 0)
            yield return $"{k}: bound {Tuple()}; SymbolKinds' preimage is {{{string.Join(", ", kinds)}}}";

        var types = Preimage(NameResolutionRosters.TypeKinds, k);
        TypeKind? observedType = (o.Symbol as TypeSymbol)?.TypeKind;
        if (observedType is TypeKind tk ? !types.Contains(tk) : types.Count > 0)
            yield return $"{k}: bound {Tuple()}; TypeKinds' preimage is {{{string.Join(", ", types)}}}";

        var members = Preimage(NameResolutionRosters.MemberKinds, k);
        if (o.Member is TypeBodyMemberKind mk ? !members.Contains(mk) : members.Count > 0)
            yield return $"{k}: bound {Tuple()}; MemberKinds' preimage is {{{string.Join(", ", members)}}}";

        if (o.Scope is ScopeKind s)
        {
            string? key = o.Symbol switch
            {
                VariableSymbol v when v.Kind == SymbolKind.Variable => NameResolutionRosters.CubeKey(SymbolKind.Variable, s, DecidingFlag(v)),
                FunctionSymbol f => NameResolutionRosters.CubeKey(SymbolKind.Function, s, FunctionClass(f)),
                _ => null,
            };
            var cube = o.Symbol is FunctionSymbol ? NameResolutionRosters.FunctionCube : NameResolutionRosters.VariableCube;
            if (key != null && !(cube.TryGetValue(key, out var cell) && EntryImage(cell).Contains(k)))
                yield return $"{k}: bound {Tuple()}; its cube cell {key} does not map to it";
            if (o.Symbol is VariableSymbol { Kind: SymbolKind.Parameter }
                && !EntryImage(NameResolutionRosters.ParameterModifiers[ParameterModifier.None]).Contains(k))
                yield return $"{k}: bound {Tuple()}; ParameterModifiers[None] does not map to it";
        }
    }

    private static bool FlagOf(VariableSymbol v, string flag) => flag switch
    {
        "IsConstant" => v.IsConstant,
        "IsStatic" => v.IsStatic,
        "IsModuleProperty" => v.IsModuleProperty,
        "IsParameter" => v.IsParameter,
        _ => throw new ArgumentOutOfRangeException(nameof(flag), flag, "not a cube flag"),
    };

    /// <summary>The symbol class of a <see cref="SymbolKind"/>: what a binder site declaring a kind of it must bind.</summary>
    private static string? SymbolClassOf(SymbolKind kind) => kind switch
    {
        SymbolKind.Variable or SymbolKind.Parameter => nameof(VariableSymbol),
        SymbolKind.Function => nameof(FunctionSymbol),
        SymbolKind.Type => nameof(TypeSymbol),
        SymbolKind.TypeAlias => nameof(TypeAliasSymbol),
        SymbolKind.Module => nameof(ModuleSymbol),
        SymbolKind.TypeParameter => nameof(TypeParameterSymbol),
        _ => null,
    };

    /// <summary>The AST node a STORE form's / LOCAL target's binder checks: its checker method's first parameter.</summary>
    private static readonly IReadOnlyDictionary<StoreForm, string> StoreFormNodes = new Dictionary<StoreForm, string>
    {
        [StoreForm.Assign] = "Assignment",
        [StoreForm.LetOrVar] = "VariableDeclaration",
        [StoreForm.Walrus] = "WalrusExpression",
        [StoreForm.OutLet] = "ModifiedArgument",
    };

    private static readonly IReadOnlyDictionary<LocalTarget, string> LocalTargetNodes = new Dictionary<LocalTarget, string>
    {
        [LocalTarget.For] = "ForStatement",
        [LocalTarget.ExceptAs] = "TryStatement",
        [LocalTarget.WithAs] = "WithStatement",
        [LocalTarget.Comprehension] = "ForClause",
        [LocalTarget.MatchCapture] = "Pattern",
        [LocalTarget.AsPatternCapture] = "AsPattern",
        [LocalTarget.OutLet] = "ModifiedArgument",
    };

    private static IEnumerable<string> BinderSiteContentProblems()
    {
        // The node maps are total over the forms that have a site and over the targets.
        foreach (var p in Roster.TotalityProblems("StoreFormNodes", Enum.GetValues<StoreForm>().Except(NameResolutionRosters.NoSiteStoreForms), StoreFormNodes.Keys))
            yield return p;
        foreach (var p in Roster.TotalityProblems("LocalTargetNodes", Enum.GetValues<LocalTarget>(), LocalTargetNodes.Keys))
            yield return p;

        var sites = CompilerBinders.Value.Sites.ToDictionary(s => s.Key, StringComparer.Ordinal);
        foreach (var (key, image) in NameResolutionRosters.BinderSites)
        {
            if (!sites.TryGetValue(key, out var site))
                continue; // BinderSiteRoster's totality fact owns a row with no site
            if (image.Form is StoreForm f && site.CheckedNodeType != StoreFormNodes.GetValueOrDefault(f))
                yield return $"{key}: binds STORE {f} (the {StoreFormNodes.GetValueOrDefault(f)} binder) and checks a {site.CheckedNodeType}";
            if (image.Target is LocalTarget t && site.CheckedNodeType != LocalTargetNodes.GetValueOrDefault(t))
                yield return $"{key}: binds LOCAL {t} (the {LocalTargetNodes.GetValueOrDefault(t)} binder) and checks a {site.CheckedNodeType}";

            // The bound argument is the symbol class of every kind declared; a base-typed (`Symbol`) argument cannot
            // discriminate and a kind with no symbol-kind preimage (the named outers, a property) names no class.
            foreach (var k in image.Declares)
            {
                var classes = Preimage(NameResolutionRosters.SymbolKinds, k).Select(SymbolClassOf).OfType<string>().ToHashSet(StringComparer.Ordinal);
                if (classes.Count > 0 && site.BoundArgumentType is string bound && bound != nameof(Symbol) && !classes.Contains(bound))
                    yield return $"{key}: binds a {bound} and declares {k}, whose symbol class is {string.Join(" / ", classes)}";
            }
        }
    }

    [Fact]
    public void BindingKindRoster_Content_RoundTripsThroughTheCompiler()
    {
        var problems = new List<string>();
        var decls = RoundTripDecls().ToList();
        // A Core-only CompilerApi resolves no Stdlib module; the import row references the monolith (deferred finding 7).
        var core = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var withStdlib = new CompilerApi(null, new[] { SharpyCoreReference.Location, SharpyStdlibReference.Location });
        foreach (var d in decls)
        {
            var result = (d.Kind == BindingKind.ImportedName ? withStdlib : core).Analyze(d.Source);
            // A refused declaration binds an error-recovery symbol (SPY0300 for an unresolved import still binds the name), so
            // a round trip reads only a clean analysis. The one refusal read is the one a roster records as the kind's own
            // (ReadOuter's RefusedByKind reason names its code: module `let` is SPY0340 and still binds its symbol).
            var errors = result.Diagnostics.Where(x => x.Severity == CompilerDiagnosticSeverity.Error).Select(x => x.Code ?? "").ToList();
            bool refusedByKind = NameResolutionRosters.ReadOuter.TryGetValue(d.Kind, out var outer)
                && outer.Excluded == ExcludedAxis.RefusedByKind && errors.All(c => outer.Reason!.Contains(c, StringComparison.Ordinal));
            if (errors.Count > 0 && !refusedByKind)
            {
                problems.Add($"{d.Kind}: the declaration is refused [{string.Join(",", errors)}]");
                continue;
            }
            if (result.SymbolTable == null || Observe(result.SymbolTable, d) is not Observed o)
            {
                problems.Add($"{d.Kind}: `{d.Name}` is bound by no scope the round trip reads [{string.Join(",", result.Diagnostics.Select(x => x.Code))}]");
                continue;
            }
            problems.AddRange(RoundTripProblems(d, o, result.SymbolTable));
        }

        var covered = decls.Select(d => d.Kind).ToHashSet();
        problems.AddRange(Enum.GetValues<BindingKind>().Except(NameResolutionRosters.NonSymbolKinds).Where(k => !covered.Contains(k))
            .Select(k => $"{k} has no round-trip declaration"));
        if (covered.Count != RoundTrippedKindCount)
            problems.Add($"{covered.Count} kinds round-tripped and the literal is {RoundTrippedKindCount}");
        problems.AddRange(BinderSiteContentProblems());

        AssertNoProblems(problems, "every roster maps a source value to the kind the compiler binds for it (P77b D3.1)");
    }
}
