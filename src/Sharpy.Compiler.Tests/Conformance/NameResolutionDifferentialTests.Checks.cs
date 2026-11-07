using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using ParameterModifier = Sharpy.Compiler.Parser.Ast.ParameterModifier;
using ScopeKind = Sharpy.Compiler.Semantic.SymbolTable.ScopeKind;
using SymbolKind = Sharpy.Compiler.Semantic.SymbolKind;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>D3's checks on what the rosters and the generator SAY, where totality can only see that they say something.</summary>
public partial class NameResolutionDifferentialTests
{
    // ------------------------------------------------------------------------------------------- //
    // D3.1: a roster's content, by round trip through the compiler
    // ------------------------------------------------------------------------------------------- //

    /// <summary>What the compiler says a kind's declaration is. A property has no <see cref="Symbol"/> (it is the standalone <see cref="PropertySymbol"/>),
    /// so its <see cref="Declared"/> is null; a type-body member's scope is the body of the type that lists it.</summary>
    private sealed record KindRoundTrip(Symbol? Declared, TypeBodyMemberKind? Member, ScopeKind? Scope, bool IsConstructor = false, bool? PropertyIsStatic = null);

    private const string RoundTripName = "x";

    /// <summary>One program per kind, from the generator's own declarations wherever it has one, and how to reach the symbol: a module name, a member of
    /// <c>H</c>, or the read of the name inside <c>f</c>. <c>Inner</c> has two binders, the comprehension's target and the lambda's parameter, which sit on
    /// different rows of the cube, so it has two programs.</summary>
    private static IEnumerable<(BindingKind Kind, string Source, Func<SemanticResult, KindRoundTrip?> Find)> RoundTripPrograms()
    {
        foreach (var kind in NameResolutionRosters.ModuleLevelKinds)
        {
            var decl = kind switch
            {
                BindingKind.ModuleUnion => new List<string> { "union x:", "    case V(f: int)", "    case L()" },
                BindingKind.ModuleInterface => new List<string> { "interface x:", "    def m(self) -> int: ..." },
                _ => ModuleDecl(kind, ReadName.Plain),
            };
            yield return (kind, WithHelpers(Join(decl)), r => FoundAtModule(r, RoundTripName));
        }

        foreach (var kind in NameResolutionRosters.TypeLevelKinds.Concat(NameResolutionRosters.InstanceKinds))
        {
            var decl = kind == BindingKind.Constructor
                ? new List<string> { "def __init__(self) -> None:", "    pass" }
                : MemberDecl(kind, ReadName.Plain);
            yield return (kind, WithHelpers(Join(new[] { "class H:" }.Concat(Indent(decl)))), r => FoundOnType(r, kind));
        }

        string InF(params string[] body) => WithHelpers(Join(new[] { "def f() -> int:" }.Concat(Indent(body))));
        foreach (var kind in new[] { BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst })
            yield return (kind, InF(LocalDecl(kind, ReadName.Plain).Append("return x").ToArray()), r => FoundAtRead(r, RoundTripName));
        foreach (var kind in new[] { BindingKind.LocalDef, BindingKind.LambdaTypedLocal })
            yield return (kind, InF(LocalDecl(kind, ReadName.Plain).Append("return x()").ToArray()), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.EnclosingLocal, InF("x: int = 13", "def g() -> int:", "    return x", "return g()"), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.Parameter, "def f(x: int) -> int:\n    return x\n", r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.Inner, InF("return [x for x in [22]][0]"), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.Inner, InF("return (lambda x: int: x)(22)"), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.Builtin, InF("return abs(-3)"), r => FoundAtRead(r, "abs"));
        yield return (BindingKind.ImportedName, "from string import digits\n" + InF("return len(digits)"), r => FoundAtRead(r, "digits"));
    }

    private static ScopeKind? DefiningScope(SymbolTable table, Symbol symbol)
    {
        var scopes = Enumerable.Range(0, 4096).Select(table.GetScope).Where(s => s != null).Select(s => s!)
            .Concat(table.GetModuleScopeNames().Select(n => table.GetModuleScope(n)!))
            .Append(table.GlobalScope);
        var owner = scopes.FirstOrDefault(s => s.GetAllSymbols().Any(d => ReferenceEquals(d, symbol))
            || s.GetAllFunctionOverloads().Any(o => o.Overloads.Any(d => ReferenceEquals(d, symbol))));
        return owner == null ? null : SymbolTable.ClassifyScope(owner.Name);
    }

    private static KindRoundTrip? FoundAtModule(SemanticResult result, string name)
    {
        var table = result.SymbolTable!;
        var symbol = table.LookupInModuleScopes(name);
        return symbol == null ? null : new KindRoundTrip(symbol, null, DefiningScope(table, symbol));
    }

    private static KindRoundTrip? FoundOnType(SemanticResult result, BindingKind kind)
    {
        if (result.SymbolTable!.LookupInModuleScopes("H") is not TypeSymbol host)
            return null;
        if (kind == BindingKind.Constructor)
            return new KindRoundTrip(host.Constructors.FirstOrDefault(), null, ScopeKind.TypeBody, IsConstructor: true);
        if (host.Fields.FirstOrDefault(f => f.Name == RoundTripName) is { } field)
            return new KindRoundTrip(field, TypeBodyMemberKind.Field, ScopeKind.TypeBody);
        if (host.Methods.FirstOrDefault(m => m.Name == RoundTripName) is { } method)
            return new KindRoundTrip(method, TypeBodyMemberKind.Method, ScopeKind.TypeBody);
        if (host.NestedTypes.FirstOrDefault(t => t.Name == RoundTripName) is { } nested)
            return new KindRoundTrip(nested, null, ScopeKind.TypeBody);
        if (host.Properties.FirstOrDefault(p => p.Name == RoundTripName) is { } property)
            return new KindRoundTrip(null, TypeBodyMemberKind.Property, ScopeKind.TypeBody, PropertyIsStatic: property.IsStatic);
        return null;
    }

    private static KindRoundTrip? FoundAtRead(SemanticResult result, string name)
    {
        var symbol = result.Ast!.Body.Cast<Node>().SelectMany(Descendants).OfType<Identifier>()
            .Where(i => i.Name == name)
            .Select(i => result.SemanticInfo!.GetIdentifierSymbol(i))
            .FirstOrDefault(s => s != null);
        return symbol == null ? null : new KindRoundTrip(symbol, null, DefiningScope(result.SymbolTable!, symbol));
    }

    private static bool VariableFlag(VariableSymbol variable, string flag)
        => (bool)typeof(VariableSymbol).GetProperty(flag, BindingFlags.Public | BindingFlags.Instance)!.GetValue(variable)!;

    /// <summary>The kind must be in the image of the row the compiler's own facts select: the inverse of each map. The two named outers have no row
    /// (<see cref="BindingKindRoster_EveryKind_IsTheImageOfASource"/>: their source is the binder census), so they answer to the scope split alone.</summary>
    private static IEnumerable<string> RoundTripProblems(BindingKind kind, KindRoundTrip found)
    {
        if (found.Scope is not ScopeKind scope)
        {
            yield return $"{kind}: the symbol is in no scope the table retains";
            yield break;
        }
        if (NameResolutionRosters.ScopeSplit[scope].Image.Contains(kind) != true)
            yield return $"{kind}: declared in {scope}, whose split does not hold it";

        if (found.Member is TypeBodyMemberKind member && !NameResolutionRosters.MemberKinds[member].Image.Contains(kind))
            yield return $"{kind}: a {member} member, whose row does not hold it";

        if (kind is BindingKind.Builtin or BindingKind.ImportedName)
            yield break;

        // A class property has no Symbol: MemberKinds' row is all the compiler says about it, and its staticness the only other fact.
        if (found.Declared == null)
        {
            if (found.PropertyIsStatic != (kind == BindingKind.StaticProperty))
                yield return $"{kind}: PropertySymbol.IsStatic is {found.PropertyIsStatic}";
            yield break;
        }

        var symbolKind = NameResolutionRosters.SymbolKinds[found.Declared.Kind];
        switch (found.Declared)
        {
            case TypeSymbol type:
                if (symbolKind.RefinedBy != nameof(NameResolutionRosters.TypeKinds))
                    yield return $"{kind}: SymbolKind.{found.Declared.Kind} is not refined by TypeKinds";
                if (!NameResolutionRosters.TypeKinds[type.TypeKind].Image.Contains(kind))
                    yield return $"{kind}: TypeKind.{type.TypeKind}, whose row does not hold it";
                break;
            case FunctionSymbol function:
                var functionClass = found.IsConstructor ? "__init__" : function.IsStatic ? "IsStatic" : NameResolutionRosters.NoFlag;
                if (symbolKind.RefinedBy != nameof(NameResolutionRosters.FunctionCube))
                    yield return $"{kind}: SymbolKind.{found.Declared.Kind} is not refined by FunctionCube";
                if (!NameResolutionRosters.FunctionCube[NameResolutionRosters.CubeKey(SymbolKind.Function, scope, functionClass)].Image.Contains(kind))
                    yield return $"{kind}: {NameResolutionRosters.CubeKey(SymbolKind.Function, scope, functionClass)}, whose row does not hold it";
                break;
            case VariableSymbol variable:
                foreach (var problem in VariableRoundTripProblems(kind, found.Declared.Kind, variable, scope))
                    yield return problem;
                break;
            default:
                if (!symbolKind.Image.Contains(kind))
                    yield return $"{kind}: SymbolKind.{found.Declared.Kind}, whose row does not hold it";
                break;
        }
    }

    private static IEnumerable<string> VariableRoundTripProblems(BindingKind kind, SymbolKind symbolKind, VariableSymbol variable, ScopeKind scope)
    {
        if (symbolKind == SymbolKind.Parameter && !NameResolutionRosters.ParameterModifiers[ParameterModifier.None].Image.Contains(kind) && kind != BindingKind.Inner)
            yield return $"{kind}: a parameter symbol, whose row does not hold it";

        // Every flag of the cube's axis the symbol sets selects a row that holds the kind or names why the flag does not decide it, and no flag selects the NoFlag row.
        var set = NameResolutionRosters.VariableCubeFlags.Where(f => f != NameResolutionRosters.NoFlag && VariableFlag(variable, f)).ToList();
        foreach (var flag in set.DefaultIfEmpty(NameResolutionRosters.NoFlag))
        {
            var row = NameResolutionRosters.VariableCube[NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)];
            if (row.Excluded == null && !row.Image.Contains(kind))
                yield return $"{kind}: {NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)}, whose row does not hold it";
        }
        if (set.Count > 0 && set.All(f => NameResolutionRosters.VariableCube[NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, f)].Excluded != null))
            yield return $"{kind}: every flag it sets ({string.Join(", ", set)}) is excluded at {scope}, so no row decides it";

        // VariableFlags names the kinds each flag refines: all of them set it, and anything else setting it is a row the cube already holds or names as out.
        foreach (var (flag, entry) in NameResolutionRosters.VariableFlags.Where(e => e.Value.Image.Count > 0))
        {
            var isSet = VariableFlag(variable, flag);
            if (entry.Image.Contains(kind) && !isSet)
                yield return $"{kind}: VariableFlags says {flag} refines it, and the symbol does not set it";
            if (isSet && !entry.Image.Contains(kind) && !NameResolutionRosters.VariableCubeFlags.Contains(flag))
                yield return $"{kind}: the symbol sets {flag}, and VariableFlags does not hold it";
        }
    }

    /// <summary>D3.1: totality can only say each source value is mapped. The verify round swapped <c>TypeKind.Struct ↔ ModuleEnum</c>, two binder rows and a
    /// flag and every fact stayed green. This compiles each kind's declaration and asks the compiler. The maps that drive the generator
    /// (<c>StoreOuter</c>, <c>StoreBodies</c>, <c>LocalOuter</c>, <c>LocalBodies</c>, <c>ReadOuter</c>) are not read here: a swapped entry moves the census.</summary>
    [Fact]
    public void BindingKindRoster_Content_RoundTripsThroughTheCompiler()
    {
        var api = ImportResolvingCompilerApi();
        var problems = new List<string>();
        var programs = RoundTripPrograms().ToList();
        foreach (var (kind, source, find) in programs)
        {
            var result = api.Analyze(source);
            // The refusal of a module `let` is its own declaration's (fidelity row 2), and the symbol is bound beside it.
            var errors = result.Diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Select(d => d.Code).Distinct().ToList();
            if (kind == BindingKind.ModuleLet ? !errors.SequenceEqual(new[] { "SPY0340" }) : errors.Count > 0)
                problems.Add($"{kind}: analysis says [{string.Join(",", errors)}] on\n{source}");
            var found = result.SymbolTable == null ? null : find(result);
            if (found == null)
                problems.Add($"{kind}: no symbol reached in\n{source}");
            else
                problems.AddRange(RoundTripProblems(kind, found));
        }
        AssertNoProblems(problems, "the compiler says what the roster says each kind is");

        // Literals, not the enum again: every kind but None, one program each and two for Inner.
        programs.Select(p => p.Kind).Distinct().Should().HaveCount(33);
        programs.Should().HaveCount(34);
        Enum.GetValues<BindingKind>().Except(programs.Select(p => p.Kind)).Should().Equal(BindingKind.None);
    }

    /// <summary>The AST node each STORE form and LOCAL target is written as: what the enclosing checker method of its binder must be handed.</summary>
    private static readonly IReadOnlyDictionary<StoreForm, string> StoreFormNodes = new Dictionary<StoreForm, string>
    {
        [StoreForm.Assign] = nameof(Assignment),
        [StoreForm.LetOrVar] = nameof(VariableDeclaration),
        [StoreForm.Walrus] = nameof(WalrusExpression),
        [StoreForm.OutLet] = nameof(ModifiedArgument),
    };

    private static readonly IReadOnlyDictionary<LocalTarget, string> LocalTargetNodes = new Dictionary<LocalTarget, string>
    {
        [LocalTarget.For] = nameof(ForStatement),
        [LocalTarget.ExceptAs] = nameof(TryStatement),
        [LocalTarget.WithAs] = nameof(WithStatement),
        [LocalTarget.Comprehension] = nameof(ForClause),
        [LocalTarget.MatchCapture] = nameof(Pattern),
        [LocalTarget.AsPatternCapture] = nameof(AsPattern),
        [LocalTarget.OutLet] = nameof(ModifiedArgument),
    };

    /// <summary>The declaration node each <see cref="TypeKind"/> is written as, which tells two <see cref="TypeSymbol"/> binders apart where their argument's class cannot.</summary>
    private static readonly IReadOnlyDictionary<string, TypeKind> TypeDefinitionNodes = new Dictionary<string, TypeKind>
    {
        [nameof(ClassDef)] = TypeKind.Class,
        [nameof(StructDef)] = TypeKind.Struct,
        [nameof(InterfaceDef)] = TypeKind.Interface,
        [nameof(EnumDef)] = TypeKind.Enum,
        [nameof(UnionDef)] = TypeKind.Union,
    };

    /// <summary>The symbol classes a kind's maps say it is: the preimage of <see cref="NameResolutionRosters.SymbolKinds"/>. A lambda-typed local is on two cubes, a variable by binding and a function by form.
    /// The two named outers have no row (their source is the census itself) and a class property has no Symbol, so those preimages are empty.</summary>
    private static IReadOnlySet<string> SymbolClassesOf(BindingKind kind)
    {
        var classes = new HashSet<string>(StringComparer.Ordinal);
        if (NameResolutionRosters.VariableCube.Values.Any(e => e.Image.Contains(kind)) || NameResolutionRosters.ParameterModifiers.Values.Any(e => e.Image.Contains(kind)))
            classes.Add(nameof(VariableSymbol));
        if (NameResolutionRosters.FunctionCube.Values.Any(e => e.Image.Contains(kind)))
            classes.Add(nameof(FunctionSymbol));
        if (NameResolutionRosters.TypeKinds.Values.Any(e => e.Image.Contains(kind)))
            classes.Add(nameof(TypeSymbol));
        if (NameResolutionRosters.SymbolKinds[SymbolKind.TypeAlias].Image.Contains(kind))
            classes.Add(nameof(TypeAliasSymbol));
        return classes;
    }

    /// <summary>D3.1's other half: a binder row is a claim about one call site, and the call site has a type. The site's bound argument is the symbol class of
    /// every kind it declares (a <see cref="Symbol"/>-typed argument is the wrapper's own, and the count of those is pinned), and a site with a form or target
    /// sits in the checker method that is handed that form's AST node.</summary>
    [Fact]
    public void BinderSiteRoster_Content_EachSiteBindsWhatItsRowDeclares()
    {
        var problems = new List<string>();
        int declaring = 0, baseTyped = 0, written = 0, typeDefinitions = 0;
        foreach (var site in CompilerBinders.Value.Sites)
        {
            if (!NameResolutionRosters.BinderSites.TryGetValue(site.Key, out var image))
                continue; // the census fact names an unmapped site

            foreach (var kind in image.Declares)
            {
                var classes = SymbolClassesOf(kind);
                if (classes.Count == 0)
                    continue;
                declaring++;
                if (site.ArgumentType == nameof(Symbol))
                    baseTyped++;
                else if (!classes.Contains(site.ArgumentType ?? ""))
                    problems.Add($"{site.Key} binds a {site.ArgumentType} and its row declares {kind}, which is a {string.Join(" or ", classes)}");

                if (site.ArgumentType == nameof(TypeSymbol) && site.EnclosingParameters.FirstOrDefault(TypeDefinitionNodes.ContainsKey) is { } node)
                {
                    typeDefinitions++;
                    if (!NameResolutionRosters.TypeKinds[TypeDefinitionNodes[node]].Image.Contains(kind))
                        problems.Add($"{site.Key} sits in a method handed a {node} and its row declares {kind}, which TypeKind.{TypeDefinitionNodes[node]} does not hold");
                }
            }

            if (image.Form is StoreForm form)
            {
                written++;
                if (!site.EnclosingParameters.Contains(StoreFormNodes[form]))
                    problems.Add($"{site.Key} is StoreForm.{form}, written as {StoreFormNodes[form]}, and its method takes [{string.Join(", ", site.EnclosingParameters)}]");
            }
            if (image.Target is LocalTarget target)
            {
                written++;
                if (!site.EnclosingParameters.Contains(LocalTargetNodes[target]))
                    problems.Add($"{site.Key} is LocalTarget.{target}, written as {LocalTargetNodes[target]}, and its method takes [{string.Join(", ", site.EnclosingParameters)}]");
            }
        }
        AssertNoProblems(problems, "a binder row says what the call site binds");

        // Measured literals: 40 declared kinds judged, 5 of them TryDeclareMember's Symbol-typed wrapper (a field or a method), 11 written forms and targets, 8 type definitions.
        (declaring, baseTyped, written, typeDefinitions).Should().Be((40, 5, 11, 8));
    }
}
