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

    /// <summary>One program per row a kind sits on, from the generator's own declarations wherever it has one, and how to reach the symbol: a module name, a member of
    /// <c>H</c>, or the read of the name inside <c>f</c>. A local sits on a function's row and a block's, so it has a program in each. <c>Inner</c> has three binders on three
    /// rows: the comprehension's target, the lambda's parameter and the walrus an initializer hosts.</summary>
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

        // The Block rows, read inside a construct's body. The initializer host's row is the walrus an initializer binds.
        string InBlock(params string[] body) => InF(new[] { "if True:" }.Concat(Indent(body)).Append("return 0").ToArray());
        foreach (var kind in new[] { BindingKind.LocalVar, BindingKind.LocalLet, BindingKind.LocalLiteralConst, BindingKind.LocalComputedConst })
            yield return (kind, InBlock(LocalDecl(kind, ReadName.Plain).Append("return x").ToArray()), r => FoundAtRead(r, RoundTripName));
        foreach (var kind in new[] { BindingKind.LocalDef, BindingKind.LambdaTypedLocal })
            yield return (kind, InBlock(LocalDecl(kind, ReadName.Plain).Append("return x()").ToArray()), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.EnclosingLocal, InBlock("x: int = 13", "def g() -> int:", "    return x", "return g()"), r => FoundAtRead(r, RoundTripName));
        yield return (BindingKind.Inner, "class H:\n    @static\n    c: int = (x := 5) + x\n", r => FoundAtRead(r, RoundTripName));
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
                var functionRow = NameResolutionRosters.CubeKey(SymbolKind.Function, scope, FunctionClass(found, function));
                if (symbolKind.RefinedBy != nameof(NameResolutionRosters.FunctionCube))
                    yield return $"{kind}: SymbolKind.{found.Declared.Kind} is not refined by FunctionCube";
                if (!NameResolutionRosters.FunctionCube[functionRow].Image.Contains(kind))
                    yield return $"{kind}: {functionRow}, whose row does not hold it";
                if (CubeCellsNoSymbolLandsOn.ContainsKey((functionRow, kind)))
                    yield return $"{kind}: {functionRow} is where its read's form sits and no symbol of the kind lands, and this program's did: {CubeCellsNoSymbolLandsOn[(functionRow, kind)]}";
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
        var set = VariableCubeFlagsOf(variable).ToList();
        foreach (var flag in set)
        {
            var row = NameResolutionRosters.VariableCube[NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)];
            if (row.Excluded == null && !row.Image.Contains(kind))
                yield return $"{kind}: {NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)}, whose row does not hold it";
        }
        if (set.All(f => NameResolutionRosters.VariableCube[NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, f)].Excluded != null))
            yield return $"{kind}: every row it lands on at {scope} ({string.Join(", ", set)}) is excluded, so no row decides it";

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

    private static string FunctionClass(KindRoundTrip found, FunctionSymbol function)
        => found.IsConstructor ? "__init__" : function.IsStatic ? "IsStatic" : NameResolutionRosters.NoFlag;

    /// <summary>The flags of the cube's axis the symbol sets, or no flag: each selects a row of <see cref="NameResolutionRosters.VariableCube"/>.</summary>
    private static IEnumerable<string> VariableCubeFlagsOf(VariableSymbol variable)
        => NameResolutionRosters.VariableCubeFlags.Where(f => f != NameResolutionRosters.NoFlag && VariableFlag(variable, f)).DefaultIfEmpty(NameResolutionRosters.NoFlag);

    /// <summary>The cube rows a program's symbol lands on, read off the compiler's symbol: its class, its defining scope and its flags. The two named outers have no row.</summary>
    private static IEnumerable<string> CubeRowsOf(BindingKind kind, KindRoundTrip found)
    {
        if (found.Scope is not ScopeKind scope || kind is BindingKind.Builtin or BindingKind.ImportedName)
            return Enumerable.Empty<string>();
        return found.Declared switch
        {
            FunctionSymbol function => new[] { NameResolutionRosters.CubeKey(SymbolKind.Function, scope, FunctionClass(found, function)) },
            VariableSymbol variable => VariableCubeFlagsOf(variable).Select(flag => NameResolutionRosters.CubeKey(SymbolKind.Variable, scope, flag)),
            _ => Enumerable.Empty<string>(),
        };
    }

    /// <summary>A row × kind no program's symbol can land on, with the reason. A program landing there is red, so the reason cannot go stale.</summary>
    private static readonly IReadOnlyDictionary<(string Row, BindingKind Kind), string> CubeCellsNoSymbolLandsOn = new Dictionary<(string, BindingKind), string>
    {
        [(NameResolutionRosters.CubeKey(SymbolKind.Function, ScopeKind.FunctionLike, NameResolutionRosters.NoFlag), BindingKind.LambdaTypedLocal)] =
            "a lambda-typed local's binding is a variable, which VariableCube holds; D3.a maps it here for its read's form, the call, and no FunctionSymbol is one",
    };

    /// <summary>The cube's totality as content: a mapped row × kind is reached by a program whose symbol lands on it, or <see cref="CubeCellsNoSymbolLandsOn"/> says why none can; an excluded row states
    /// its reason. Totality over the keys and the scope split cannot see a row whose image was swapped for other kinds of the split.</summary>
    internal static (List<string> Problems, int Reached, int NoSymbol, int OutRows) CubeReachProblems(
        IEnumerable<KeyValuePair<string, RosterEntry<BindingKind>>> rows,
        IReadOnlySet<(string Row, BindingKind Kind)> reached,
        IReadOnlySet<(string Row, BindingKind Kind)> noSymbol)
    {
        var problems = new List<string>();
        int reachedCells = 0, noSymbolCells = 0, outRows = 0;
        foreach (var (row, entry) in rows)
        {
            if (entry.Excluded != null)
            {
                outRows++;
                if (string.IsNullOrWhiteSpace(entry.Reason))
                    problems.Add($"{row} is excluded and states no reason");
                continue;
            }
            if (entry.Image.Count == 0)
                problems.Add($"{row} is neither mapped nor excluded");
            foreach (var kind in entry.Image)
            {
                if (reached.Contains((row, kind)))
                    reachedCells++;
                else if (noSymbol.Contains((row, kind)))
                    noSymbolCells++;
                else
                    problems.Add($"{row} says it holds {kind}, and no program's symbol lands there");
            }
        }
        return (problems, reachedCells, noSymbolCells, outRows);
    }

    /// <summary>What a kind's declaration says in the parser's own nodes, for the kinds a declaration of the name spells. <c>let</c> is no symbol flag (the cube's comment says so), so a <c>let</c> spelled as a
    /// variable lands on the same row of the same cube: only the node tells them apart. <c>Lambda</c> is the initializer's node, which tells a lambda-typed local from a variable holding anything else.</summary>
    private static readonly IReadOnlyDictionary<BindingKind, (bool IsLet, bool IsConst, bool Lambda)> DeclarationSpellings = new Dictionary<BindingKind, (bool, bool, bool)>
    {
        [BindingKind.ModuleVar] = (false, false, false),
        [BindingKind.ModuleLet] = (true, false, false),
        [BindingKind.ModuleLiteralConst] = (false, true, false),
        [BindingKind.ModuleComputedConst] = (false, true, false),
        [BindingKind.StaticField] = (false, false, false),
        [BindingKind.InstanceField] = (false, false, false),
        [BindingKind.ClassLiteralConst] = (false, true, false),
        [BindingKind.ClassComputedConst] = (false, true, false),
        [BindingKind.LocalVar] = (false, false, false),
        [BindingKind.LocalLet] = (true, false, false),
        [BindingKind.LocalLiteralConst] = (false, true, false),
        [BindingKind.LocalComputedConst] = (false, true, false),
        [BindingKind.EnclosingLocal] = (false, false, false),
        [BindingKind.LambdaTypedLocal] = (false, false, true),
    };

    /// <summary>The declaration of the name in the program is the node the kind says: <c>let x = e</c> is an <see cref="Assignment"/> with <c>IsLet</c>, and every other spelling a <see cref="VariableDeclaration"/>.</summary>
    private static IEnumerable<string> DeclarationSpellingProblems(BindingKind kind, IEnumerable<Node> program)
    {
        if (!DeclarationSpellings.TryGetValue(kind, out var says))
            yield break;
        var nodes = program.SelectMany(Descendants).ToList();
        var declared = nodes.OfType<VariableDeclaration>().Where(d => d.Name == RoundTripName).Select(d => (d.IsLet, d.IsConst, Lambda: d.InitialValue is LambdaExpression))
            .Concat(nodes.OfType<Assignment>().Where(a => a.IsLet && a.Target is Identifier { Name: RoundTripName }).Select(a => (a.IsLet, IsConst: false, Lambda: a.Value is LambdaExpression)))
            .ToList();
        if (declared.Count != 1)
            yield return $"{kind}: {declared.Count} declarations of {RoundTripName} in its program";
        else if (declared[0] != says)
            yield return $"{kind}: the parser says (IsLet, IsConst, lambda initializer) = {declared[0]}, and the kind is {says}";
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
        var reached = new HashSet<(string Row, BindingKind Kind)>();
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
            {
                problems.AddRange(RoundTripProblems(kind, found));
                problems.AddRange(DeclarationSpellingProblems(kind, result.Ast!.Body));
                reached.UnionWith(CubeRowsOf(kind, found).Select(row => (row, kind)));
            }
        }
        var (cubeProblems, reachedCells, noSymbolCells, outRows) = CubeReachProblems(
            NameResolutionRosters.VariableCube.Concat(NameResolutionRosters.FunctionCube), reached, CubeCellsNoSymbolLandsOn.Keys.ToHashSet());
        problems.AddRange(cubeProblems);
        AssertNoProblems(problems, "the compiler says what the roster says each kind is, on every row the cubes hold");

        // Literals, not the enum again: every kind but None, with a program per row it sits on.
        programs.Select(p => p.Kind).Distinct().Should().HaveCount(33);
        programs.Should().HaveCount(42);
        Enum.GetValues<BindingKind>().Except(programs.Select(p => p.Kind)).Should().Equal(BindingKind.None);
        (reachedCells, noSymbolCells, outRows).Should().Be((31, 1, 30), "the rows × kinds reached, those no symbol can land on, and the rows excluded with a reason");
        (NameResolutionRosters.VariableCube.Count, NameResolutionRosters.FunctionCube.Count).Should().Be((30, 18));
    }

    /// <summary>The spelling check can fail, on both sides of let: a variable spelled <c>let</c>, a <c>let</c> spelled as a variable. The cube cannot tell either, so these are its controls.</summary>
    [Theory]
    [InlineData(BindingKind.LocalLet, "let x = 5", null)]
    [InlineData(BindingKind.LocalLet, "let x: int = 5", null)]
    [InlineData(BindingKind.LocalVar, "x: int = 5", null)]
    [InlineData(BindingKind.LocalLet, "x: int = 5", "the parser says")]
    [InlineData(BindingKind.LocalVar, "let x = 5", "the parser says")]
    [InlineData(BindingKind.LocalVar, "const x: int = 5", "the parser says")]
    [InlineData(BindingKind.LocalLiteralConst, "x: int = 5", "the parser says")]
    [InlineData(BindingKind.LambdaTypedLocal, "x: int = 5", "the parser says")]
    [InlineData(BindingKind.LambdaTypedLocal, "x: () -> int = lambda: 5", null)]
    [InlineData(BindingKind.LocalVar, "y: int = 5", "0 declarations")]
    [InlineData(BindingKind.LocalDef, "y: int = 5", null)] // no declaration spells a def, so the map has no row for it
    internal void DeclarationSpelling_ReadsTheParsersNode(BindingKind kind, string declaration, string? fails)
    {
        var problems = DeclarationSpellingProblems(kind, new CompilerApi().Parse($"def f() -> None:\n    {declaration}\n").Ast!.Body).ToList();
        if (fails == null)
            problems.Should().BeEmpty();
        else
            problems.Should().ContainSingle().Which.Should().Contain(fails);
    }

    /// <summary>The reach check can fail: a row whose image no program lands on is red, a row × kind waived by its reason is not, and an excluded row needs a reason.</summary>
    [Fact]
    public void CubeReach_ARowNoProgramLandsOn_IsRed()
    {
        const string Row = "row";
        KeyValuePair<string, RosterEntry<BindingKind>> Mapped(params BindingKind[] kinds) => new(Row, Roster.To(kinds));
        var none = new HashSet<(string, BindingKind)>();
        var landed = new HashSet<(string, BindingKind)> { (Row, BindingKind.LocalVar) };

        CubeReachProblems(new[] { Mapped(BindingKind.LocalVar) }, landed, none).Problems.Should().BeEmpty();
        CubeReachProblems(new[] { Mapped(BindingKind.LocalVar) }, none, none).Problems.Should().ContainSingle().Which.Should().Contain("no program's symbol lands there");
        CubeReachProblems(new[] { Mapped(BindingKind.LocalLet, BindingKind.LocalVar) }, landed, none).Problems.Should().ContainSingle().Which.Should().Contain("LocalLet");
        CubeReachProblems(new[] { Mapped(BindingKind.LocalVar) }, none, landed).NoSymbol.Should().Be(1);
        CubeReachProblems(new[] { new KeyValuePair<string, RosterEntry<BindingKind>>(Row, Roster.Out<BindingKind>(ExcludedAxis.NotAKind, " ")) }, none, none).Problems.Should().ContainSingle().Which.Should().Contain("no reason");
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
        int declaring = 0, baseTyped = 0, written = 0, typeDefinitions = 0, declaringRows = 0;
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

                // Two rows of one symbol class, a def's or a parameter's, which the argument's type cannot tell apart. The phase can: NameResolver binds declarations
                // before any body is read, and the checker's declaring rows sit inside one. A store form's row is out of this: its one site binds at any level.
                if (image.Form == null && image.Target == null)
                {
                    declaringRows++;
                    if (site.Key.StartsWith("NameResolver.", StringComparison.Ordinal) && IsPositionLocal(kind))
                        problems.Add($"{site.Key} runs before any body is read and its row declares {kind}, a local");
                    if (site.Key.StartsWith("TypeChecker.", StringComparison.Ordinal) && IsModuleKind(kind))
                        problems.Add($"{site.Key} sits inside a body and its row declares {kind}, a module binding");
                    if (site.EnclosingParameters.Contains(nameof(LambdaExpression)) != (kind == BindingKind.Inner))
                        problems.Add($"{site.Key} takes [{string.Join(", ", site.EnclosingParameters)}] and its row declares {kind}: a declaring row's Inner is a lambda's parameter");
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
        declaringRows.Should().Be(28, "the declared kinds on declaring rows, read against their phase and node");
    }

    // ------------------------------------------------------------------------------------------- //
    // D3.1: a READ cell's program, where its label says
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The expression a comprehension holds at one slot, reached by the slot roster's property name: a node's own (<c>DictComprehension.Key</c>) or a clause's (<c>ForClause.Iterator</c>, the second for's on <see cref="SecondIterable"/>).</summary>
    private static Expression? ComprehensionSlotExpression(Expression comprehension, string slot)
    {
        var second = slot == SecondIterable;
        var value = second ? ComprehensionSlot.Iterable : Enum.Parse<ComprehensionSlot>(slot);
        var node = comprehension.GetType();
        var key = NameResolutionRosters.ComprehensionSlots.Where(e => e.Value.Image.Contains(value)).Select(e => e.Key)
            .Single(k => k.StartsWith(node.Name + ".", StringComparison.Ordinal) || k.StartsWith(nameof(ForClause) + ".", StringComparison.Ordinal) || k.StartsWith(nameof(IfClause) + ".", StringComparison.Ordinal));
        var ownerName = key[..key.IndexOf('.')];
        var clauses = (IEnumerable<ComprehensionClause>)node.GetProperty("Clauses")!.GetValue(comprehension)!;
        Node? owner = ownerName == node.Name ? comprehension : clauses.Where(c => c.GetType().Name == ownerName).Skip(second ? 1 : 0).FirstOrDefault();
        return owner?.GetType().GetProperty(key[(key.IndexOf('.') + 1)..])?.GetValue(owner) as Expression;
    }

    /// <summary>The fed check reads a label, and a label cannot say what the program does: <c>[x]</c> hollowed to <c>[5]</c> keeps the id, the axis key, the tag and the rule. This parses each
    /// axis-1 READ cell with the compiler's parser and reads the slot the label names: the read name is there, and the comprehension reads it nowhere else.</summary>
    internal static (List<string> Failures, int Judged) ComprehensionReadPlaceFailures(IEnumerable<NameCell> pool)
    {
        const string Prefix = "comprehension-read: ";
        var api = new CompilerApi();
        var failures = new List<string>();
        int judged = 0;
        foreach (var cell in pool.Where(c => c.AxisCell?.StartsWith(Prefix, StringComparison.Ordinal) == true))
        {
            judged++;
            var axes = cell.AxisCell![Prefix.Length..].Split(" × ");
            var node = NameResolutionRosters.ComprehensionKinds[axes[0]].Image[0];
            var slot = axes[2];
            var ast = api.Parse(cell.Source).Ast;
            var comprehension = ast?.Body.Cast<Node>().SelectMany(Descendants).OfType<Expression>().FirstOrDefault(e => e.GetType() == node);
            if (comprehension == null)
            {
                failures.Add($"ComprehensionReadPlace: {cell.Id} has no {node.Name} in its program");
                continue;
            }
            var read = ComprehensionSlotExpression(comprehension, slot);
            if (read == null || !Descendants(read).OfType<Identifier>().Any(i => i.Name == RoundTripName))
                failures.Add($"ComprehensionReadPlace: {cell.Id} says {slot} reads {RoundTripName}, and the program's {node.Name} has no read of it there");
            else if (Descendants(comprehension).OfType<Identifier>().Count(i => i.Name == RoundTripName) != 1)
                failures.Add($"ComprehensionReadPlace: {cell.Id} says {slot} reads {RoundTripName}, and the program's {node.Name} also reads it elsewhere");
        }
        return (failures, judged);
    }

    /// <summary>The pool's axis-1 reads each read what their label says. Hollowing a slot is the control: same id, same label, no read. A slot that also reads elsewhere is the other direction.</summary>
    [Fact]
    public void ComprehensionReadPlace_EachCellReadsAtTheSlotItsLabelNames()
    {
        var pool = GeneratedCells();
        var (failures, judged) = ComprehensionReadPlaceFailures(pool);
        AssertNoProblems(failures, "a label says where the program reads the name, and the program has to");
        judged.Should().BeGreaterThanOrEqualTo(150, "the judged floor, ahead of the literal so that it can be the assertion that fails");
        judged.Should().Be(172, "a measured literal: 43 kind × shape × slot, by 2 nestings and 2 orders");

        var iterable = pool.Single(c => c.AxisCell == ComprehensionReadKey("list-comprehension", ComprehensionClauseShape.OneFor, nameof(ComprehensionSlot.Iterable), ComprehensionNesting.FunctionBody, Order.Earlier));
        ComprehensionReadPlaceFailures(new[] { iterable }).Failures.Should().BeEmpty("the control's untouched program");
        ComprehensionReadPlaceFailures(new[] { iterable with { Source = iterable.Source.Replace("[x]", "[5]", StringComparison.Ordinal) } }).Failures
            .Should().ContainSingle().Which.Should().Contain("has no read of it there");
        ComprehensionReadPlaceFailures(new[] { iterable with { Source = iterable.Source.Replace("[v0 for", "[x for", StringComparison.Ordinal) } }).Failures
            .Should().ContainSingle().Which.Should().Contain("also reads it elsewhere");
    }

    // ------------------------------------------------------------------------------------------- //
    // D3.2: the models nothing read
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The same cell at the earlier order: its id with the order token swapped. Null for a cell with no later order to pair. TYPEMEMBER spells the token
    /// <c>after</c> and every other family the <see cref="Order"/> value's own name.</summary>
    internal static string? EarlierOrderTwinId(NameCell cell)
    {
        if (cell.Family == Family.TypeMember)
            return cell.Id.EndsWith("/after", StringComparison.Ordinal) ? cell.Id[..^"after".Length] + "before" : null;
        var parts = cell.Id.Split('/');
        var at = Array.IndexOf(parts, nameof(Order.Later));
        if (at < 0)
            return null;
        parts[at] = nameof(Order.Earlier);
        return string.Join("/", parts);
    }

    /// <summary>N3's promise, read as a check on the models: the later order prints what the earlier order prints. Every <c>MustRun</c> / <c>MayRefuse</c> cell at the later order, and any
    /// cell python answers there with an order-only <c>NameError</c>, pairs with its earlier twin. Where python ran the twin, the later cell's model must equal that stdout, which the sweep's python
    /// control holds <c>Python</c> to. A must-refuse cell has no model to read, so it pairs and is not judged. This reads the models python cannot confirm directly.</summary>
    internal static (List<string> Failures, int Pairs, int Judged) ModelPairing(IReadOnlyList<NameCell> pool)
    {
        var byId = pool.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var failures = new List<string>();
        int pairs = 0, judged = 0;
        foreach (var later in pool)
        {
            var twinId = EarlierOrderTwinId(later);
            var carriesModel = later.Tag is SharpyTag.MustRun or SharpyTag.MayRefuse;
            if (twinId == null || !(carriesModel || later.Python.Kind == PyKind.NameError))
                continue;
            if (!byId.TryGetValue(twinId, out var earlier))
            {
                failures.Add($"ModelPairing: {later.Id} has no twin {twinId}");
                continue;
            }
            pairs++;
            if (earlier.Python.Kind != PyKind.Ran || !carriesModel)
                continue;
            judged++;
            if (later.Model != earlier.Python.Value)
                failures.Add($"ModelPairing: {later.Id} models '{later.Model}' and {twinId} prints '{earlier.Python.Value}'");
        }
        return (failures, pairs, judged);
    }

    [Fact]
    public void ModelPairing_TheLaterOrderPrintsWhatTheEarlierOrderPrints()
    {
        var (failures, pairs, judged) = ModelPairing(GeneratedCells());

        AssertNoProblems(failures, "N3 promises the later order prints what the earlier order prints");

        // Measured literals: a generator that stops spelling the order token pairs nothing, and an absence of failures would read green.
        judged.Should().BeGreaterThanOrEqualTo(1000, "D3.2's floor, ahead of the literal so that it can be the assertion that fails");
        pairs.Should().Be(1741);
        judged.Should().Be(1276);
    }

    // ------------------------------------------------------------------------------------------- //
    // D3.3: a pattern note is an exemption, so it needs a way to be wrong
    // ------------------------------------------------------------------------------------------- //

    private static string CaseProgram(string pattern) => $"def main() -> None:\n    match 1:\n        case {pattern}:\n            print(1)\n";

    /// <summary>Each note's probe: null when the note's stated failure still holds. A note exempts by existing, so it has to be able to go stale.</summary>
    private static readonly IReadOnlyDictionary<string, Func<IReadOnlyList<NameCell>, string?>> PatternNoteFalsifiers =
        new Dictionary<string, Func<IReadOnlyList<NameCell>, string?>>(StringComparer.Ordinal)
        {
            // The note: python has no `and` pattern, so the twin of a program spelling one has nothing to judge. `|` is the control: the renderer does render a composite.
            [nameof(AndPattern)] = _ =>
            {
                TwinRenderer.Render(CaseProgram("int() | object()"));
                try
                {
                    TwinRenderer.Render(CaseProgram("int() and object()"));
                    return "the twin of `case int() and object():` renders, so the note's reason is gone";
                }
                catch (NotSupportedException refused) when (refused.Message.Contains(nameof(AndPattern), StringComparison.Ordinal))
                {
                    return null;
                }
            },
            // The note: `case v: Later` has no parser producer and the untyped capture is LOCAL's match capture. `case v:` is the control: the capture itself parses.
            [nameof(BindingPattern)] = pool =>
            {
                var api = new CompilerApi();
                string Typed = CaseProgram("v: Later");
                if (!api.Analyze(Typed).Diagnostics.Any(d => d.Code == "SPY0102"))
                    return "`case v: Later:` is no longer SPY0102, so the typed capture has a producer";
                if (api.Analyze(CaseProgram("v")).Diagnostics.Any(d => d.Code == "SPY0102"))
                    return "`case v:` is SPY0102 too, so the probe reads nothing about the type";
                return pool.Any(c => c.Family == Family.Local && c.Construct == LocalTarget.MatchCapture)
                    ? null
                    : "no generated LOCAL match-capture cell carries the untyped capture the note points at";
            },
        };

    [Fact]
    public void PatternKindRoster_EveryNote_HasAFalsifier()
    {
        var pool = GeneratedCells();
        var problems = new List<string>();
        foreach (var name in NameResolutionRosters.PatternNotes.Keys)
        {
            if (pool.Any(c => c.Family == Family.TypeMember && c.Id.Contains($"pattern/{name}", StringComparison.Ordinal)))
                problems.Add($"noted pattern generates cells: {name}");
            if (!PatternNoteFalsifiers.TryGetValue(name, out var probe))
                problems.Add($"{name} is noted and has no falsifier");
            else if (probe(pool) is string stale)
                problems.Add($"{name}: {stale}");
        }
        AssertNoProblems(problems, "a note exempts a pattern from generating cells, so it has to be true and able to go stale");

        NameResolutionRosters.PatternNotes.Should().HaveCount(2, "a measured literal: a new note is a new exemption");
        PatternNoteFalsifiers.Keys.Should().BeEquivalentTo(NameResolutionRosters.PatternNotes.Keys, "a falsifier outliving its note checks nothing");
    }

    // ------------------------------------------------------------------------------------------- //
    // D3.4: a rule's cite is an axis predicate
    // ------------------------------------------------------------------------------------------- //

    private static bool IsTypeLevel(BindingKind k) => NameResolutionRosters.TypeLevelKinds.Contains(k);

    private static bool IsInstanceLevel(BindingKind k) => NameResolutionRosters.InstanceKinds.Contains(k);

    private static bool IsModuleKind(BindingKind k) => NameResolutionRosters.ModuleLevelKinds.Contains(k);

    private static bool IsPositionLocal(BindingKind k) => NameResolutionRosters.PositionLocalKinds.Contains(k);

    private static bool IsNamedOuter(BindingKind k) => k is BindingKind.Builtin or BindingKind.ImportedName;

    private static bool IsVisibleModuleOuter(BindingKind k) => IsModuleKind(k) || IsNamedOuter(k);

    /// <summary>A segment of the cell's id: the order and spelling tokens the generator writes there and no property carries.</summary>
    private static bool Spelled(NameCell c, string token) => c.Id.Split('/').Contains(token);

    private static bool ThroughAMember(NameCell c) => Spelled(c, nameof(Spelling.Qualified)) || Spelled(c, nameof(Spelling.Alias)) || Spelled(c, nameof(Spelling.SelfDot))
        || c.Id.Contains("/base/H.x", StringComparison.Ordinal) || c.Id.Contains("/base/B.x", StringComparison.Ordinal) || c.Id.Contains("/base/self.x", StringComparison.Ordinal);

    private static bool Nested(NameCell c) => Spelled(c, nameof(Nesting.Lambda)) || Spelled(c, nameof(Nesting.ComprehensionIterable)) || Spelled(c, nameof(Nesting.ComprehensionElement));

    /// <summary>READ's module-level split: a read at load time at the later order is N3's. The nesting is the id's token, the one place the cell keeps it.</summary>
    private static bool ModuleReadAtLoadLater(NameCell c) => Spelled(c, nameof(Order.Later)) && c.Position is ReadPosition p
        && ModuleReadAtLoad(p, Spelled(c, nameof(Nesting.Lambda)) ? Nesting.Lambda : Spelled(c, nameof(Nesting.ComprehensionIterable)) ? Nesting.ComprehensionIterable : Spelled(c, nameof(Nesting.ComprehensionElement)) ? Nesting.ComprehensionElement : Nesting.Direct);

    /// <summary>A class body's bare read that sees the member: an initializer at the earlier order (the later order is N2's, or N1+N3's with no outer).</summary>
    private static bool ClassBodySeesTheMember(NameCell c) => c.Relation == DeclaringRelation.Self && Spelled(c, nameof(Order.Earlier)) && c.Position is ReadPosition p && IsInitializer(p);

    /// <summary>A position evaluated while the program loads, so a read can see a value before it is ready.</summary>
    private static bool AtLoad(NameCell c) => c.Position is ReadPosition.ModuleInitializer or ReadPosition.ModulePropertyInitializer
        or ReadPosition.StaticInitializer or ReadPosition.ConstInitializer or ReadPosition.StaticPropertyDefault;

    /// <summary>D3.4: what each rule says, as a predicate on the axes of the cell citing it, so a rule retagged to one admitting the same codes is red here and not only in the census.
    /// Every rule in the pool has an entry and every entry has a cell. The tag is on the axes: <c>MustRun</c> and <c>MustRefuse</c> under one rule are its two arms.</summary>
    internal static readonly IReadOnlyDictionary<string, Func<NameCell, bool>> RuleAxes = new Dictionary<string, Func<NameCell, bool>>(StringComparer.Ordinal)
    {
        // N1: the innermost binding that means the read.
        ["N1"] = c => c.Tag == SharpyTag.MustRun && (c.Family == Family.Read
            ? !IsPositionLocal(c.Outer) && !ThroughAMember(c) && !(c.Position == ReadPosition.ModuleInitializer && Nested(c) && !Spelled(c, nameof(Nesting.Lambda)))
                && (IsTypeLevel(c.Target) ? ClassBodySeesTheMember(c) || c.Relation == DeclaringRelation.Enclosing : !ModuleReadAtLoadLater(c))
            : c.Family == Family.Local && c.Construct == LocalTarget.Comprehension),
        ["N1+N3"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRun && IsTypeLevel(c.Target) && c.Outer == BindingKind.None
            && (Spelled(c, nameof(Order.Later)) || c.Relation == DeclaringRelation.Other),
        ["N1-arity"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Outer == BindingKind.Builtin && Spelled(c, "arity-mismatch") && !Spelled(c, "int"),
        ["N1-innermost"] = c => c.Tag == SharpyTag.MustRun && (c.Family == Family.Read ? IsLocalOuter(c.Outer) : c.Family == Family.Local && IsPositionLocal(c.Target) && !Spelled(c, "let-in")),
        ["N1-methods"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Outer == BindingKind.None && (IsTypeLevel(c.Target) || IsInstanceLevel(c.Target))
            && Spelled(c, nameof(Spelling.Bare)) && c.Relation is DeclaringRelation.Self or DeclaringRelation.Sibling,
        ["N1-nesting"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRun && IsModuleKind(c.Target) && c.Outer == BindingKind.None
            && (c.Position == ReadPosition.ComprehensionElement ? c.Id.StartsWith("read/comprehension/", StringComparison.Ordinal) : c.Position == ReadPosition.ModuleInitializer && Nested(c)),
        // The read reaches the outer, and the read is ill-typed. A class body that sees its own later member is N2's, and any binding or import under a builtin's name read at load is N2-module's.
        ["N1-type-error"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && IsVisibleModuleOuter(c.Outer) && !ThroughAMember(c)
            && !Spelled(c, "arity-mismatch") && !Spelled(c, "arity-match")
            && (!IsModuleKind(c.Target) || (c.Outer == BindingKind.Builtin && c.Target != BindingKind.ModuleDef && !ModuleReadAtLoadLater(c)))
            && (!IsTypeLevel(c.Target) || c.Relation != DeclaringRelation.Self || Spelled(c, nameof(Order.Earlier)) || Nested(c) || c.Position is not ReadPosition p || !IsInitializer(p)),

        // N2: a class body's read of an outer name it later declares.
        ["N2"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && IsTypeLevel(c.Target) && IsVisibleModuleOuter(c.Outer) && c.Position is ReadPosition p && IsInitializer(p)
            && !Spelled(c, nameof(Order.Earlier)),
        ["N2-module"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && IsModuleKind(c.Target) && IsNamedOuter(c.Outer) && Spelled(c, nameof(Order.Later)) && !Spelled(c, "arity-mismatch"),

        // N3: the later order prints what the earlier prints, and a cycle has no order to print.
        ["N3"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRun && c.Outer == BindingKind.None
            && (c.Id.StartsWith("read/order-observation/", StringComparison.Ordinal) || (IsModuleKind(c.Target) && (ModuleReadAtLoadLater(c) || c.Id.StartsWith("read/str/", StringComparison.Ordinal)))),
        ["N3-cycle"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuseOrThrow && c.Outer == BindingKind.None && AtLoad(c),

        // N5: a local is visible from its binding, in its block.
        ["N5"] = c => c.Family == Family.Local
            ? c.Tag == SharpyTag.MustRun
                ? c.Outer == BindingKind.None || (c.Outer == BindingKind.ModuleVar && Spelled(c, "inside"))
                : c.Tag == SharpyTag.MustRefuse && !Spelled(c, "self") && (c.Outer == BindingKind.None || IsTypeLevel(c.Outer))
            : c.Family == Family.Store && !IsAttributeStore(c) && c.Declaration == null && (c.Outer is BindingKind.None or BindingKind.Builtin
                ),
        ["N5-block-scope"] = c => c.Family == Family.Local && c.Tag == SharpyTag.MustRun && Spelled(c, "let-in") && c.Outer != BindingKind.None,
        ["N5-declared-later"] = c => c.Family == Family.Local && c.Tag == SharpyTag.MustRefuse && IsPositionLocal(c.Target) && c.Target != BindingKind.LocalDef
            && (Spelled(c, "above") || Spelled(c, "L28") || c.Timing is ReadTimings.SameBlockAbove or ReadTimings.BackEdge),
        ["N5-self"] = c => c.Family == Family.Local && c.Tag == SharpyTag.MustRefuse && c.Target == BindingKind.LambdaTypedLocal && Spelled(c, "self"),

        // N6: a store over a binding reaches it only through a declaration, and a declaration needs one it can write.
        ["N6"] = c => c.Family == Family.Store && c.Deviation == null && c.Form is StoreForm.Assign or StoreForm.Augmented or StoreForm.Walrus
            && (c.Outer is BindingKind.ModuleVar or BindingKind.ModuleProperty or BindingKind.EnclosingLocal || IsNamedOuter(c.Outer))
            && (c.Tag == SharpyTag.MustRun) == (Spelled(c, "global") || Spelled(c, "nonlocal") || c.Declaration != null),
        ["N6-declaration"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && c.Declaration != null,
        ["N6-same-function"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRun && c.Outer == BindingKind.LocalVar && c.Target == BindingKind.LocalVar,
        ["N6-type-kind"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && IsNonWritableByMeaning(c.Outer),

        // N7: a member through a spelling that names its owner.
        ["N7"] = c => c.Tag == SharpyTag.MustRun && (c.Family == Family.Read
            ? ThroughAMember(c) || Spelled(c, nameof(Spelling.Call)) || Spelled(c, "GenericReceiver") || (c.TypeAt != null && c.Target == BindingKind.ModuleTypeAlias)
                || (c.Id.StartsWith("read/str/", StringComparison.Ordinal) && IsTypeLevel(c.Target))
            : c.Family == Family.Store && IsAttributeStore(c) && c.Form is StoreForm.Assign or StoreForm.Augmented),

        ["#1974"] = c => c.Tag == SharpyTag.MustRun && (c.Family == Family.Store ? c.Form == StoreForm.LetOrVar
            : c.Family == Family.Local && c.Outer == BindingKind.LocalVar && Spelled(c, "let-in")),
        ["A3"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRun && c.Relation == DeclaringRelation.Base && Spelled(c, nameof(Spelling.Bare)) && IsModuleKind(c.Outer),
        ["P52"] = c => c.Family == Family.TypeMember && c.Tag == SharpyTag.MustRun && c.Outer == BindingKind.None && IsModuleKind(c.Target),
        ["R-BM"] = c => c.Tag == SharpyTag.MustRun && (c.Family == Family.Store ? c.Form == StoreForm.OutLet : c.Family == Family.Local && c.Construct == LocalTarget.OutLet),
        ["R-KG"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && IsTypeLevel(c.Target) && c.Outer == BindingKind.None && c.Relation == DeclaringRelation.Enclosing && Spelled(c, nameof(Spelling.Bare)),
        // R-KT's closure over a struct's `self`. A @static member reached through self needs no `this`, so both keep the class arm (the lead's ruling on axis 7).
        ["R-KT"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Id.StartsWith("read/struct/", StringComparison.Ordinal) && Spelled(c, nameof(Spelling.SelfDot))
            && IsInstanceLevel(c.Target) && (Spelled(c, nameof(Nesting.Lambda)) || (c.Position is ReadPosition closure && NameResolutionRosters.StructClosureBodies.Contains(closure))),
        // R-KU: a @static store through what is not its owner's name.
        ["R-KU"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && IsAttributeStore(c) && c.Target is BindingKind.StaticField or BindingKind.StaticProperty
            && (Spelled(c, nameof(AttributeReceiver.SelfDot)) || Spelled(c, nameof(AttributeReceiver.DerivedType)) || Spelled(c, nameof(AttributeReceiver.InstanceLocal))),
        ["R-Y"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRun && c.Relation == DeclaringRelation.Self && (IsTypeLevel(c.Target) || IsInstanceLevel(c.Target))
            && IsVisibleModuleOuter(c.Outer) && Spelled(c, nameof(Spelling.Bare)),
        ["R1(b)"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Relation == DeclaringRelation.Base && c.Outer == BindingKind.None && Spelled(c, nameof(Spelling.Bare)),

        // S2 / S3: a read past a binding's own visibility, over an outer name that must not silently answer it.
        ["S2"] = c => c.Family == Family.Local && c.Tag == SharpyTag.MustRefuse && c.Target == BindingKind.LocalVar && c.Construct != null && c.Outer != BindingKind.None,
        // The owner's ruling on a comprehension's walrus over a visible module name read after it (2026-11-06).
        ["S2-walrus"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && c.Form == StoreForm.Walrus && c.Outer == BindingKind.ModuleVar && c.Deviation == "walrus-in-comprehension-scope",
        ["S3"] = c => c.Family == Family.Local && c.Tag == SharpyTag.MustRefuse && c.Target == BindingKind.LocalDef && Spelled(c, "above"),

        ["SPY0401"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Position is ReadPosition p && IsConstantOnly(p),
        ["SPY0606"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && IsTypeLevelMember(c.Outer) && !IsAttributeStore(c),

        ["kind:SPY0212"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Outer == BindingKind.Builtin && c.Target == BindingKind.ModuleClass && Spelled(c, "int"),
        ["kind:SPY0225"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && c.Target is BindingKind.ModuleLiteralConst or BindingKind.ModuleComputedConst or BindingKind.ClassLiteralConst or BindingKind.ClassComputedConst,
        ["kind:SPY0290"] = c => c.Tag == SharpyTag.MustRefuse && IsInstanceLevel(c.Target) && (c.Family == Family.Read
            ? !Spelled(c, nameof(Spelling.SelfDot)) && (ThroughAMember(c) || Spelled(c, nameof(Spelling.Call)))
            : c.Family == Family.Store && IsAttributeStore(c) && (Spelled(c, nameof(AttributeReceiver.TypeName)) || Spelled(c, nameof(AttributeReceiver.Alias)) || Spelled(c, nameof(AttributeReceiver.DerivedType)))),
        ["kind:SPY0340"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MayRefuse && c.Target == BindingKind.ModuleLet,
        ["kind:SPY0606"] = c => c.Family == Family.Store && c.Tag == SharpyTag.MustRefuse && IsAttributeStore(c) && c.Target is BindingKind.StaticMethod or BindingKind.InstanceMethod,
        ["kind:enum-member-constant"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.Position == ReadPosition.EnumMemberValue,
        ["kind:instance-at-initializer"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && IsInstanceLevel(c.Target) && Spelled(c, nameof(Spelling.Bare)) && c.Position is ReadPosition p && IsInitializer(p),
        ["kind:result-wrap"] = c => c.Family == Family.Read && c.Tag == SharpyTag.MustRefuse && c.TypeAt == TypePosition.ResultError,

        ["seed"] = c => c.Family == Family.Seed && c.Id.StartsWith("seed/", StringComparison.Ordinal),
        ["unruled:global-creates-binding"] = c => c.Tag == SharpyTag.Unruled && UnruledAxesHold(c),
    };

    internal static bool RuleAxesHold(NameCell c) => RuleAxes.TryGetValue(c.Rule, out var axes) && axes(c);

    private static bool RefusalIsAnOutcome(NameCell c) => c.Tag is SharpyTag.MustRefuse or SharpyTag.MustRefuseOrThrow or SharpyTag.MayRefuse;

    /// <summary>D3.4's three directions: a cell's rule has a predicate and the cell is on its axes, a predicate has a cell, and a rule a refusal could answer to says which codes it admits. A seed carries its own.</summary>
    internal static IEnumerable<string> RuleAxesFailures(IReadOnlyList<NameCell> pool)
    {
        foreach (var rule in pool.Select(c => c.Rule).Distinct(StringComparer.Ordinal).Where(r => !RuleAxes.ContainsKey(r)))
            yield return $"RuleAxes: rule {rule} is cited and has no predicate";
        foreach (var rule in RuleAxes.Keys.Where(r => pool.All(c => c.Rule != r)))
            yield return $"RuleAxes: the predicate for {rule} is carried by no cell";

        var off = pool.Where(c => RuleAxes.ContainsKey(c.Rule) && !RuleAxesHold(c)).ToList();
        foreach (var c in off.Take(40))
            yield return $"RuleAxes: {c.Id} cites {c.Rule} and its axes are not that rule's (tag {c.Tag}, target {c.Target}, outer {c.Outer}, position {c.Position})";
        if (off.Count > 40)
            yield return $"RuleAxes: … {off.Count} cells off their rule's axes in total: {string.Join(", ", off.GroupBy(c => c.Rule).Select(g => $"{g.Key} {g.Count()}"))}";

        var refusing = pool.Where(RefusalIsAnOutcome).ToList();
        foreach (var rule in refusing.Where(c => c.SeedRefusalCodes == null).Select(c => c.Rule).Distinct(StringComparer.Ordinal).Where(r => !AdmittedRefusals.ContainsKey(r)))
            yield return $"AdmittedRefusals: rule {rule} is carried by a cell a refusal could answer and has no entry";
        foreach (var rule in AdmittedRefusals.Keys.Where(r => refusing.All(c => c.Rule != r)))
            yield return $"AdmittedRefusals: {rule} has an entry and no cell a refusal could answer";
    }

    [Fact]
    public void RuleAxes_EveryCiteIsOnItsRulesAxes()
    {
        var pool = GeneratedCells();
        AssertNoProblems(RuleAxesFailures(pool), "a rule's cite is a claim about the cell's axes, and a refusal under it says which codes it admits");

        // The retag the census alone used to see: if all of a rule's cells fit another rule's predicate, moving them there moves no axis.
        var hidden = pool.GroupBy(c => c.Rule, StringComparer.Ordinal)
            .SelectMany(q => RuleAxes.Where(r => r.Key != q.Key && q.All(r.Value)).Select(r => $"every cell of {q.Key} fits {r.Key}'s predicate"))
            .ToList();
        AssertNoProblems(hidden, "two rules that cannot be told apart by their axes are one rule, or a loose predicate");

        // Measured literals: a generator that stopped citing a rule would otherwise pass an emptier check.
        RuleAxes.Should().HaveCount(44);
        // 29 rules with an entry, and the seeds', which carry their own codes.
        pool.Where(RefusalIsAnOutcome).Select(c => c.Rule).Distinct(StringComparer.Ordinal).Should().HaveCount(30);
    }
}
