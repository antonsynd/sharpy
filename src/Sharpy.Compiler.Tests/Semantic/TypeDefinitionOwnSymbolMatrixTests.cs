using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The generator of the <b>declaration-position contract</b> (P21e; #2063 tracker, #2314, #2319; rulings R-CS, R-HJ,
/// R-HK): <b>a declaration statement has ONE decided verdict at every position it can be written in</b>. The declaring
/// positions — the module body and a type body — bind as the spec says (overload sets, nested types, nested aliases);
/// a type definition or an import anywhere else is refused by ONE Validation code, SPY0708 (R-CS); a builtin-named
/// <c>type</c> alias is SPY0212 at EVERY position (#2319); a nested <c>def</c> that re-declares a name of the same
/// block or of an enclosing/enclosed block of the same function is SPY0204 (R-HJ). Nothing reaches SPY0510/SPY0908,
/// nothing says "symbol not found", and no validator runs against a master or enclosing type the definition does not
/// own.
///
/// <para><b>History.</b> This began as P54's (#2250) matrix for the class <i>a type definition writes only the symbol
/// declared for it</i> — 7 kinds × 4 positions × 3 names, oracle "one SPY0202 at the definition line, the rest equals
/// the control, module <c>Foo</c>'s members equal the control". R-HK extended it into the declaration-position
/// generator; P54's rest-equals-control and <c>Foo</c>-members clauses still hold and are kept.</para>
///
/// <para><b>Matrix</b> (generated). Kind (12): <c>class</c> and <c>struct</c> (each carrying the mixed auto/setter
/// property whose backing field was P54's measured write), <c>@dataclass class</c>, <c>interface</c>, <c>union</c>,
/// <c>delegate</c>, <c>class-const</c> (a non-constant <c>const X</c>, whose body <c>ConstEligibility</c> once walked
/// against the enclosing <c>Foo</c>), <c>enum</c>, <c>alias</c> (<c>type N = str</c>; the builtin column spells
/// <c>int</c> so target ≠ name), <c>import</c> (<c>import copy as N</c>; Fresh = plain <c>import copy</c>),
/// <c>from-import</c> (<c>from copy import deepcopy as N</c>; Fresh = plain) — Core's <c>copy</c>, not the plan's
/// <c>math</c>: this harness references Sharpy.Core only, where <c>math</c> is SPY0300 (the CLI measured both modules
/// to the same silent shapes) — <c>class-cascade</c> (a class that
/// legitimately draws SPY0701 — a SAME-CLR-key duplicate <c>__getitem__</c> pair — and SPY0325 — <c>(IShape)</c> with
/// <c>area</c> unimplemented; a different-key pair draws no SPY0701, measured). × Position (13): <c>module</c>,
/// <c>module-try</c>, <c>type-body</c> (directly in <c>class Host:</c>), <c>type-body-if</c>, <c>method-body</c>,
/// <c>accessor-body</c> (<c>property get</c>), <c>def-body</c>, and <c>if</c>/<c>for</c>/<c>while</c>/<c>try</c>/
/// <c>with</c>/<c>match</c> in a def (each block runs once — no constant-false condition the checker could skip). ×
/// Name (3): builtin (<c>str</c>; <c>int</c> for the alias kind, <c>list</c> for <c>class-cascade</c>), <c>Foo</c> (a module-level user class, the
/// prelude's first line), <c>Fresh</c>. The host class is named <c>Host</c>, never <c>Foo</c> (<c>Host.Host</c> is
/// SPY0525's rule, not this one). <b>Edge cells</b> (the prober budget's two axis changes): the HOST axis — a
/// <c>@static</c> method body, an interface default-method body, a union method body and an <c>async def</c> body, ×
/// kind {class, enum, import, alias} × name {Fresh; and <c>int</c> for alias}; the SPELLING axis — a
/// <c>@suppress</c>-decorated class and import (through <c>UnwrapDecorated</c>) and the backtick-escaped
/// <c>type `int` = str</c> (the sanctioned route: LEGAL, expected []), each at <c>module</c> and <c>def-body</c>.
/// Each cell is analysed on ONE <see cref="CompilerApi"/> beside its CONTROL — the same program with the definition's
/// lines replaced by <c>pass</c> (a comment at <c>module</c>, where <c>pass</c> is itself SPY0340), so every line
/// keeps its number.</para>
///
/// <para><b>Oracle per cell.</b> (1) No SPY09xx (hard — never allowlisted). (2) The ERROR diagnostics on the
/// definition's lines (decorators included) equal <see cref="ExpectedErrors"/> exactly, as a multiset of code + line +
/// column; warnings on those lines are not compared. (3) Every diagnostic off the definition's lines equals the
/// control's (<c>main</c> reads <c>s._size</c> / <c>f._size</c>, so a written <c>str</c> or <c>Foo</c> changes it).
/// (4) Module <c>Foo</c>'s members equal the control's (hard). (5) <c>class-cascade</c> at a non-declaring position:
/// SPY0701 and SPY0325 ABSENT anywhere — its positive control, <c>class-cascade × module × Fresh</c> showing both, is
/// asserted unconditionally so the absence is never vacuous. The base's cascade SPY0701 lands at line 0 (the
/// "both at line  and line" shape — it is about the master's members, not the definition's), so it is off the
/// definition's lines: (5) catches it by looking at every diagnostic regardless of line, and (3) catches it too. Block literals:
/// <c>module-try</c>'s SPY0340 at the <c>try</c> is pinned as ONE SPY0340 at that line in the cell, not merely
/// derived from (3). <b>Execution:</b> <c>Analyze</c> never runs code generation, so an SPY0510/SPY0908 is invisible
/// to it (an enum in a def is silent under <c>Analyze</c> at the base); every cell whose whole program is expected
/// error-free is compiled AND run, and must print <c>ok</c> with no SPY05xx/SPY09xx. A cell that fails to run has its
/// control run too, so the failure is attributed: the declaration's (the control runs) or the host's (the control fails
/// the same way). A row citing #2148 (the class-body <c>if</c>) must be the host's — checked, never exempted; a row
/// citing #2021 (the struct auto-constructor) must fail with SPY0908 CS0100 and nothing else — checked too.</para>
///
/// <para><b>Ratchet.</b> <c>Semantic/declaration-position-allowlist.txt</c> lists the cell ids red at the base, each
/// citing an issue. A listed cell that passes is STALE (red); an unlisted failing cell is red; a row naming no
/// generated cell is red. Clauses (1), (4) and the control sanity check are never exempted. The allowlist drains to
/// empty across P21e.</para>
/// </summary>
public class TypeDefinitionOwnSymbolMatrixTests : IntegrationTestBase
{
    public TypeDefinitionOwnSymbolMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    internal enum Family
    {
        TypeDefinition,
        Alias,
        Import,
    }

    /// <summary>Where an expected diagnostic lands, resolved per cell by <see cref="Resolve"/>.</summary>
    internal enum At
    {
        /// <summary>The definition's keyword (<c>class</c>, <c>type</c>, <c>import</c>, …) — line and column.</summary>
        Keyword,

        /// <summary>The declared name token.</summary>
        Name,

        /// <summary><c>class-cascade</c>'s second <c>__getitem__</c> (keyword line + 3, the <c>def</c>'s column).</summary>
        SecondDunder,
    }

    /// <summary>(kind, family, the definition's lines for a spelled name, unindented).</summary>
    private static readonly (string Kind, Family Family, Func<string, string[]> Lines)[] Kinds =
    {
        ("class", Family.TypeDefinition, n => new[]
        {
            $"class {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        ("dataclass", Family.TypeDefinition, n => new[]
        {
            "@dataclass",
            $"class {n}:",
            "    x: int",
        }),
        ("struct", Family.TypeDefinition, n => new[]
        {
            $"struct {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        ("interface", Family.TypeDefinition, n => new[]
        {
            $"interface {n}:",
            "    def probe(self, value: int) -> int: ...",
        }),
        ("union", Family.TypeDefinition, n => new[]
        {
            $"union {n}:",
            "    case Probe(value: int)",
        }),
        ("delegate", Family.TypeDefinition, n => new[]
        {
            $"delegate {n}(value: int) -> int",
        }),
        ("class-const", Family.TypeDefinition, n => new[]
        {
            $"class {n}:",
            "    const X: int = len(\"abc\")",
        }),
        ("enum", Family.TypeDefinition, n => new[]
        {
            $"enum {n}:",
            "    A = 1",
        }),
        ("alias", Family.Alias, n => new[]
        {
            $"type {n} = str",
        }),
        ("import", Family.Import, n => new[]
        {
            n == "Fresh" ? "import copy" : $"import copy as {n}",
        }),
        ("from-import", Family.Import, n => new[]
        {
            n == "Fresh" ? "from copy import deepcopy" : $"from copy import deepcopy as {n}",
        }),
        ("class-cascade", Family.TypeDefinition, n => new[]
        {
            $"class {n}(IShape):",
            "    def __getitem__(self, key: int) -> str:",
            "        return \"a\"",
            "    def __getitem__(self, key: int) -> int:",
            "        return 0",
        }),
    };

    /// <summary>The spelling-axis edge kinds: a <c>@suppress</c>-decorated class and import.</summary>
    private static readonly (string Kind, Family Family, Func<string, string[]> Lines)[] SpellingKinds =
    {
        ("suppress-class", Family.TypeDefinition, n => new[]
        {
            "@suppress(\"SPY0453\")",
            $"class {n}:",
            "    x: int = 0",
        }),
        ("suppress-import", Family.Import, n => new[]
        {
            "@suppress(\"SPY0452\")",
            n == "Fresh" ? "import copy" : $"import copy as {n}",
        }),
    };

    internal sealed record Position(
        string Name, string[] Before, int Indent, string[] After, string Placeholder = "pass",
        (string Code, int BeforeIndex, int Column)[]? BlockErrors = null);

    /// <summary>The 13 positions of the matrix.</summary>
    private static readonly Position[] Positions =
    {
        new("module", Array.Empty<string>(), 0, Array.Empty<string>(), Placeholder: "# placeholder"),
        new("module-try", new[] { "try:" }, 4, new[] { "except Exception:", "    pass" },
            BlockErrors: new[] { ("SPY0340", 0, 1) }),
        new("type-body", new[] { "class Host:", "    tag: int = 0" }, 4, Array.Empty<string>()),
        new("type-body-if", new[] { "class Host:", "    tag: int = 0", "    if True:" }, 8, Array.Empty<string>()),
        new("method-body", new[] { "class Host:", "    def m(self) -> None:" }, 8, new[] { "        print(\"m\")" }),
        new("accessor-body", new[] { "class Host:", "    property get size(self) -> int:" }, 8, new[] { "        return 0" }),
        new("def-body", new[] { "def helper() -> None:" }, 4, new[] { "    print(\"helper\")" }),
        new("if-in-def", new[] { "def helper() -> None:", "    if True:" }, 8, new[] { "    print(\"helper\")" }),
        new("for-in-def", new[] { "def helper() -> None:", "    for i in range(1):" }, 8, new[] { "    print(\"helper\")" }),
        new("while-in-def", new[] { "def helper() -> None:", "    n: int = 0", "    while n < 1:" }, 8,
            new[] { "        n = n + 1", "    print(\"helper\")" }),
        new("try-in-def", new[] { "def helper() -> None:", "    try:" }, 8,
            new[] { "    except Exception:", "        pass", "    print(\"helper\")" }),
        new("with-in-def", new[] { "def helper() -> None:", "    with Ctx():" }, 8, new[] { "    print(\"helper\")" }),
        new("match-in-def", new[] { "def helper(v: int) -> None:", "    match v:", "        case 1:" }, 12,
            new[] { "        case _:", "            pass", "    print(\"helper\")" }),
    };

    /// <summary>The host-axis edge positions (R-CS: the refusal is host-agnostic, one cell per host proves it reaches it).</summary>
    private static readonly Position[] HostPositions =
    {
        new("static-method-body", new[] { "class Host:", "    @static", "    def s() -> None:" }, 8, new[] { "        print(\"s\")" }),
        new("interface-default-body", new[] { "interface IHost:", "    def d(self) -> int:" }, 8, new[] { "        return 0" }),
        new("union-method-body", new[] { "union UHost:", "    case A(v: int)", "", "    def m(self) -> int:" }, 8,
            new[] { "        return 0" }),
        new("async-def-body", new[] { "async def ahelper() -> None:" }, 4, new[] { "    print(\"helper\")" }),
    };

    /// <summary>(name axis value, the module-level lines before the shared prelude, main's body).</summary>
    private static readonly (string Name, string[] Prelude, string[] MainBody)[] Names =
    {
        ("str", Array.Empty<string>(), new[] { "    s = \"abc\"", "    print(s._size)" }),
        ("Foo", new[] { "class Foo:", "    const X: int = 5", "", "    def __init__(self) -> None:", "        pass", "" }, new[] { "    f: Foo = Foo()", "    print(f._size)" }),
        ("Fresh", Array.Empty<string>(), new[] { "    print(\"ok\")" }),
    };

    /// <summary>Shared by every cell: the interface <c>class-cascade</c> leaves unimplemented, and the Sharpy context
    /// manager <c>with-in-def</c> enters (modelled on <c>with_real_context_manager_1283.spy</c>).</summary>
    private static readonly string[] SharedPrelude =
    {
        "interface IShape:",
        "    def area(self) -> int: ...",
        "",
        "class Ctx:",
        "    def __init__(self) -> None:",
        "        pass",
        "",
        "    def __enter__(self) -> Self:",
        "        return self",
        "",
        "    def __exit__(self) -> None:",
        "        pass",
        "",
    };

    private static readonly HashSet<string> DeclaringPositions = new(StringComparer.Ordinal) { "module", "type-body" };
    private static readonly HashSet<string> BuiltinSpellings = new(StringComparer.Ordinal) { "str", "int", "list" };

    internal sealed record Cell(
        string Kind, Family Family, string Position, string Name, string Source, string Control, string Runnable,
        string RunnableControl,
        int FirstLine, int LastLine, int DefinitionLine, int Indent, int NameLine, int NameColumn,
        IReadOnlyList<(string Code, int Line, int Column)> BlockErrors, bool Edge)
    {
        public string Id => $"{Kind} × {Position} × {Name}";
    }

    /// <summary>The spelled name for a name-axis value. The builtin column is <c>str</c>, except: the alias kind spells
    /// <c>int</c> (target ≠ name), and <c>class-cascade</c> spells <c>list</c> — the validators' by-name fallback
    /// cascades against a master that has dunders and interfaces of its own (measured at the base: <c>list</c> and
    /// <c>dict</c> draw SPY0701 at line 0 + SPY0325 against the master, <c>str</c> draws nothing), so with <c>str</c>
    /// the absence clause (5) would have nothing to catch (#2316 cell 3 is the <c>list</c> shape).</summary>
    private static string Spell(string kind, string axisName) => (kind, axisName) switch
    {
        ("alias", "str") => "int",
        ("class-cascade", "str") => "list",
        _ => axisName,
    };

    private static Cell Build(
        (string Kind, Family Family, Func<string, string[]> Lines) kind, Position position,
        (string Name, string[] Prelude, string[] MainBody) name, string spelled, bool edge)
    {
        var prelude = name.Prelude.Concat(SharedPrelude).ToArray();
        var definition = kind.Lines(spelled).Select(l => new string(' ', position.Indent) + l).ToArray();
        var placeholder = definition.Select(_ => new string(' ', position.Indent) + position.Placeholder).ToArray();
        string Program(string[] body, string[] mainBody) => string.Join("\n",
            prelude.Concat(position.Before).Concat(body).Concat(position.After)
                .Concat(new[] { "", "def main() -> None:" }).Concat(mainBody).Append(""));

        var first = prelude.Length + position.Before.Length + 1;
        // 1-based line of the definition's keyword: a decorator line comes first.
        var keywordOffset = Array.FindIndex(definition, l => !l.TrimStart().StartsWith('@'));
        var keywordLine = definition[keywordOffset];
        // The name token: preceded by whitespace and not followed by an identifier character, so `struct str` finds the
        // name, not the `str` inside `struct`.
        var nameMatch = Regex.Match(keywordLine, @"(?<=\s)" + Regex.Escape(spelled) + @"(?![\w`])");
        var nameIndex = nameMatch.Success ? nameMatch.Index - 1 : -1;
        var blockErrors = (position.BlockErrors ?? Array.Empty<(string, int, int)>())
            .Select(b => (b.Code, prelude.Length + b.BeforeIndex + 1, b.Column)).ToList();
        return new Cell(kind.Kind, kind.Family, position.Name, spelled,
            Program(definition, name.MainBody), Program(placeholder, name.MainBody),
            Program(definition, new[] { "    print(\"ok\")" }), Program(placeholder, new[] { "    print(\"ok\")" }),
            first, first + definition.Length - 1, first + keywordOffset, position.Indent,
            first + keywordOffset, nameIndex < 0 ? 0 : nameIndex + 2, blockErrors, edge);
    }

    /// <summary>The 12 × 13 × 3 matrix cells.</summary>
    internal static IEnumerable<Cell> MatrixCells()
    {
        foreach (var kind in Kinds)
            foreach (var position in Positions)
                foreach (var name in Names)
                    yield return Build(kind, position, name, Spell(kind.Kind, name.Name), edge: false);
    }

    /// <summary>The edge cells: host axis (4 hosts × 5 kind/name pairs) and spelling axis (3 spellings × 2 positions).</summary>
    internal static IEnumerable<Cell> EdgeCells()
    {
        var fresh = Names.Single(n => n.Name == "Fresh");
        var builtin = Names.Single(n => n.Name == "str");
        var hostKinds = new[] { "class", "enum", "import", "alias" }.Select(k => Kinds.Single(x => x.Kind == k)).ToArray();
        foreach (var host in HostPositions)
        {
            foreach (var kind in hostKinds)
                yield return Build(kind, host, fresh, "Fresh", edge: true);
            yield return Build(Kinds.Single(x => x.Kind == "alias"), host, builtin, "int", edge: true);
        }

        foreach (var position in Positions.Where(p => p.Name is "module" or "def-body"))
        {
            foreach (var kind in SpellingKinds)
                yield return Build(kind, position, fresh, "Fresh", edge: true);
            yield return Build(Kinds.Single(x => x.Kind == "alias"), position, fresh, "`int`", edge: true);
        }
    }

    internal static IEnumerable<Cell> Cells() => MatrixCells().Concat(EdgeCells());

    private static Family FamilyOf(string kind)
        => Kinds.Concat(SpellingKinds).Single(k => k.Kind == kind).Family;

    /// <summary>
    /// The CONTRACT in code — the post-fix verdict of every cell, never the base's answer (the base's reds are the
    /// allowlist). Decision table (<c>Foo</c> = the module-level user class; builtin = <c>str</c>, <c>int</c> for
    /// the alias kind, <c>list</c> for <c>class-cascade</c>; declaring = <c>module</c>, <c>type-body</c>):
    /// <list type="table">
    /// <item><term>type definition (7 kinds + <c>enum</c> + <c>class-cascade</c> + <c>suppress-class</c>), non-declaring</term>
    /// <description>[SPY0708 @ keyword] (R-CS; refused by position, so its name is moot and no validator runs over
    /// the body: no SPY0212, no cascade).</description></item>
    /// <item><term>type definition, declaring, Fresh</term><description>[] — <c>class-cascade</c>: [SPY0701 @ second
    /// dunder, SPY0325 @ keyword], the positive control for (5).</description></item>
    /// <item><term>type definition, declaring, builtin</term><description>[SPY0212 @ name] (at <c>type-body</c>:
    /// cells u/v, #2319) — <c>class-cascade</c>: [SPY0212, SPY0701, SPY0325]. NameResolver's gate refuses and still
    /// falls through to the resolver (<c>ResolveDeclaration</c>: "refused declarations still fall through … so the
    /// symbol exists"), so the class owns its symbol and the validators run — measured at <c>module</c>; the
    /// <c>type-body</c> value is pinned to the same shape, the gate being the same call in the nested dispatcher
    /// (plan Phase 3 Task 1).</description></item>
    /// <item><term>type definition, <c>module</c>, Foo</term><description>[SPY0204 @ keyword] (a duplicate of the
    /// prelude's <c>Foo</c>; the duplicate owns no symbol, so <c>class-cascade</c> draws no cascade).</description></item>
    /// <item><term>type definition, <c>type-body</c>, Foo</term><description>[] (a nested <c>Host.Foo</c> is legal) —
    /// <c>class-cascade</c>: [SPY0701, SPY0325].</description></item>
    /// <item><term><c>class-const</c> at a declaring position</term><description>no extra error: the non-constant
    /// <c>const X: int = len("abc")</c> draws nothing of its own at the base (measured at <c>module</c> and nested in
    /// a class body), so it adds nothing to the set.</description></item>
    /// <item><term><c>alias</c></term><description>builtin (<c>int</c>) → [SPY0212 @ name] at EVERY position (#2319);
    /// Fresh and the backtick-escaped <c>`int`</c> → [] everywhere; Foo → [SPY0204 @ keyword] at <c>module</c>
    /// ("Type alias 'Foo' is already defined", measured; ONE — <c>Analyze</c> returns it twice at the base because
    /// NameResolver and the TypeChecker both report it and only <c>Compile</c>'s bag dedupes), [] at <c>type-body</c> (measured: runs) and at every
    /// non-declaring position (a block-scoped alias, D7).</description></item>
    /// <item><term><c>import</c>/<c>from-import</c>/<c>suppress-import</c></term><description><c>module</c> → [] for
    /// all three names (measured silent at the base: <c>import copy as str</c> draws nothing, <c>import copy as
    /// Foo</c> beside <c>class Foo</c> draws only the SPY0452 warning — pinned as measured; both are cells of #2359
    /// (an import alias no declaration rule sees), not of this contract — a fix there moves these literals); every other position → [SPY0708 @ keyword] (R-CS: "import at module
    /// level").</description></item>
    /// </list>
    /// <c>module-try</c>'s SPY0340 at the <c>try</c> is the block's verdict, not the declaration's (D7): it is off the
    /// definition's lines and pinned separately as a block literal.
    /// </summary>
    internal static IReadOnlyList<(string Code, At Where)> ExpectedErrors(string kind, string position, string name)
    {
        var declaring = DeclaringPositions.Contains(position);
        var builtin = BuiltinSpellings.Contains(name);
        switch (FamilyOf(kind))
        {
            case Family.TypeDefinition:
                {
                    if (!declaring)
                        return new[] { ("SPY0708", At.Keyword) };
                    var cascade = kind == "class-cascade"
                        ? new[] { ("SPY0701", At.SecondDunder), ("SPY0325", At.Keyword) }
                        : Array.Empty<(string, At)>();
                    if (name == "Foo")
                        return position == "module" ? new[] { ("SPY0204", At.Keyword) } : cascade;
                    if (builtin)
                        return new[] { ("SPY0212", At.Name) }.Concat(cascade).ToArray();
                    return cascade;
                }

            case Family.Alias:
                if (builtin)
                    return new[] { ("SPY0212", At.Name) };
                if (name == "Foo" && position == "module")
                    return new[] { ("SPY0204", At.Keyword) };
                return Array.Empty<(string, At)>();

            case Family.Import:
                return position == "module" ? Array.Empty<(string, At)>() : new[] { ("SPY0708", At.Keyword) };

            default:
                throw new InvalidOperationException(kind);
        }
    }

    private static (int Line, int Column) Resolve(Cell cell, At where) => where switch
    {
        At.Keyword => (cell.DefinitionLine, cell.Indent + 1),
        At.Name => (cell.NameLine, cell.NameColumn),
        At.SecondDunder => (cell.DefinitionLine + 3, cell.Indent + 5),
        _ => throw new InvalidOperationException(where.ToString()),
    };

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(12, Kinds.Length);
        Assert.Equal(13, Positions.Length);
        Assert.Equal(3, Names.Length);
        Assert.Equal(12 * 13 * 3, MatrixCells().Count());
        // Host axis: 4 hosts × {class, enum, import, alias} × Fresh + alias × int; spelling axis: 3 spellings × 2 positions.
        Assert.Equal(4 * 5 + 3 * 2, EdgeCells().Count());
        Assert.Equal(8 * 2 * 3, NestedDefCells().Count());
        var ids = Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id)).ToList();
        Assert.Equal(12 * 13 * 3 + 26 + 48, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryDeclarationHasOneVerdictAtEveryPosition()
    {
        var allowlist = LoadAllowlist(out var problems);
        // The Core reference, as CompileAndExecute passes it: without it `import copy` is SPY0300 under Analyze.
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var controls = new Dictionary<string, SemanticResult>(StringComparer.Ordinal);
        var red = new List<string>();
        var ran = 0;
        var cascadeControlArmed = false;
        foreach (var cell in Cells())
        {
            var result = api.Analyze(cell.Source, CancellationToken.None);
            if (!controls.TryGetValue(cell.Control, out var control))
                controls[cell.Control] = control = api.Analyze(cell.Control, CancellationToken.None);
            var line = $"{cell.Id}: {Fingerprint(result.Diagnostics)}";
            Output.WriteLine(line);
            var failures = new List<string>();

            // (1) hard: no internal error under Analyze.
            if (result.Diagnostics.Any(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)))
                problems.Add($"{line} — an internal error");

            bool OnDefinition(CompilerDiagnostic d) => d.Line >= cell.FirstLine && d.Line <= cell.LastLine;

            // Control sanity (hard): the Fresh control has no error but the position's own block literal, so no
            // cell is vacuous because its scaffold is refused.
            if (cell.Name == "Fresh")
            {
                var controlErrors = Fingerprint(control.Diagnostics.Where(d => d.IsError));
                var blockErrors = string.Join(" ", cell.BlockErrors.Select(b => $"{b.Code}@{b.Line}:{b.Column}").OrderBy(s => s, StringComparer.Ordinal));
                if (controlErrors != blockErrors)
                    problems.Add($"{cell.Id}: the control's errors are [{controlErrors}], expected only the block's [{blockErrors}]");
            }

            // (2) the exact error multiset on the definition's lines.
            var expected = ExpectedErrors(cell.Kind, cell.Position, cell.Name)
                .Select(e => { var (l, c) = Resolve(cell, e.Where); return $"{e.Code}@{l}:{c}"; })
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = result.Diagnostics.Where(d => d.IsError && OnDefinition(d))
                .Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                failures.Add($"errors on the definition are [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]");

            // (3) the rest equals the control.
            var others = Fingerprint(result.Diagnostics.Where(d => !OnDefinition(d)));
            var controlOthers = Fingerprint(control.Diagnostics.Where(d => !OnDefinition(d)));
            if (others != controlOthers)
                failures.Add($"the rest differs from the control [{controlOthers}]");

            // Block literal: the position's own verdict, pinned rather than derived from (3).
            foreach (var (code, l, c) in cell.BlockErrors)
            {
                var count = result.Diagnostics.Count(d => d.Code == code && d.Line == l && d.Column == c);
                if (count != 1)
                    failures.Add($"expected one {code} at {l}:{c} (the block's verdict), found {count}");
            }

            // (4) hard: module-level Foo's members equal the control's.
            if (cell.Name == "Foo")
            {
                var members = FooMembers(result);
                var controlMembers = FooMembers(control);
                if (members != controlMembers)
                    problems.Add($"{cell.Id}: module-level Foo is [{members}] / control [{controlMembers}]");
            }

            // (5) the cascade is absent at a non-declaring position; its positive control is the module cell.
            if (cell.Kind == "class-cascade")
            {
                var cascade = result.Diagnostics.Where(d => d.Code is "SPY0701" or "SPY0325").ToList();
                if (cell.Position == "module" && cell.Name == "Fresh")
                    cascadeControlArmed = cascade.Any(d => d.Code == "SPY0701") && cascade.Any(d => d.Code == "SPY0325");
                if (!DeclaringPositions.Contains(cell.Position) && cascade.Count > 0)
                    failures.Add($"a validator ran against a type the definition does not own: {Fingerprint(cascade)}");
            }

            // Execution: a program expected error-free must compile AND run — Analyze cannot see SPY0510/SPY0908.
            if (expected.Count == 0 && cell.BlockErrors.Count == 0)
            {
                ran++;
                var run = CompileAndExecute(cell.Runnable);
                var internalCodes = InternalCodes(run);
                string? cellRun = null, controlRun = null;
                if (!run.Success || internalCodes.Count > 0 || run.StandardOutput != "ok\n")
                {
                    // Attribute the failure: the control (the same host, `pass` for the definition) either runs — the
                    // declaration's failure — or fails the same way — the host's.
                    var controlExec = CompileAndExecute(cell.RunnableControl);
                    cellRun = Fingerprint(internalCodes);
                    controlRun = Fingerprint(InternalCodes(controlExec));
                    failures.Add($"did not run cleanly: success={run.Success} stdout=[{run.StandardOutput.TrimEnd()}] "
                        + $"{cellRun} {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}"
                        + (controlExec.Success ? " — the control runs" : $" — the control fails too [{controlRun}]"));
                }

                // A row citing #2148 claims the failure is the class-body `if`'s (an SPY0510 at the host), not the
                // declaration's: its control must fail with the same internal codes at the same places.
                if (allowlist.TryGetValue(cell.Id, out var hostCite) && hostCite.Contains("#2148", StringComparison.Ordinal)
                    && (cellRun is null || cellRun != controlRun))
                    problems.Add($"{cell.Id}: allowlisted as the host's failure (#2148), but under run the cell is [{cellRun}] "
                        + $"and its control [{controlRun}] — the failure is not the host's");

                // A row citing #2021 claims the failure is the struct auto-constructor's duplicate parameter: the
                // run must fail with SPY0908 carrying CS0100, nothing else, so a different failure in these
                // otherwise error-free cells is red rather than absorbed by the row.
                if (allowlist.TryGetValue(cell.Id, out var structCite) && structCite.Contains("#2021", StringComparison.Ordinal)
                    && !IsStructCtorDuplicate(run, internalCodes))
                    problems.Add($"{cell.Id}: allowlisted as the struct auto-constructor CS0100 (#2021), but under run the cell is "
                        + $"success={run.Success} [{Fingerprint(internalCodes)}] {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}");
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        if (!cascadeControlArmed)
            problems.Add("class-cascade × module × Fresh does not show both SPY0701 and SPY0325: the absence clause (5) is vacuous");

        Output.WriteLine($"{Cells().Count()} cells, {ran} executed, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The nested-def redeclaration sub-matrix (R-HJ, #2314).
    // ---------------------------------------------------------------------------------------------------------------

    internal sealed record NestedDefCell(
        string Relation, string Signature, string Call, string Source,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? ExpectedStdout)
    {
        public string Id => $"nested-def × {Relation} × {Signature} × {Call}";
    }

    /// <summary>
    /// Block relation {same block, sibling blocks (two <c>if</c>s), sibling <c>if</c>/<c>else</c> branches, sibling
    /// <c>try</c> body / <c>except</c> handler, two arms of one <c>match</c> (lowered to a C# <c>switch</c>), the same
    /// arms with a loop <c>break</c> in one (lowered to an is-chain), nested outer-first, nested inner-first}
    /// × signature {same
    /// <c>(x: int)</c> twice, different <c>(x: int)</c>/<c>(x: str)</c>} × call {fits first, fits second, none}. Each
    /// def returns a literal tag, <c>"first"</c> / <c>"second"</c>, and the call prints it.
    ///
    /// <para><b>Call placement</b> (stated, because it decides which def the checker binds): the call sits where the
    /// FIRST def is the binding in scope — after both defs in the same block; for nested outer-first in the outer
    /// block after the inner block; for nested inner-first in the inner block after the inner def. That is D6's
    /// premise ("the first def is the binding"). Under the same signature a call that fits one def fits both, so
    /// "fits first" and "fits second" spell the same call (<c>f(1)</c>); both ids are kept so the axis stays total.
    /// For sibling blocks the call axis maps to {block A calls its def, block B calls its def, no call}; the
    /// <c>if</c>/<c>else</c> pair and the <c>match</c> sit in a two-pass loop and the <c>try</c> body raises into its
    /// handler, so BOTH branches run.</para>
    ///
    /// <para><b>Expected</b> (R-HJ + D6): the five sibling relations → run, stdout = the tag of the def its call
    /// binds, then <c>end</c>; every other relation → [SPY0204 @ the later def's name token] + [SPY0220 @ the call's
    /// argument — the base's anchor for an argument mismatch] iff the call fits only the later (refused) def; no
    /// SPY09xx. The arms of one <c>match</c> are sibling blocks like any other (owner ruling at the P21e verify
    /// round, 2026-10-23, reverting the lead's one-scope ruling D5b). Whatever the lowering: the switch-lowered
    /// arms are SPY0908 CS0128 today because local functions are not versioned, and those rows cite their class
    /// issue with that exact fingerprint required.</para>
    /// </summary>
    internal static IEnumerable<NestedDefCell> NestedDefCells()
    {
        foreach (var relation in new[] { "same", "sibling", "sibling-else", "sibling-except", "sibling-match", "sibling-match-break", "outer-first", "inner-first" })
        {
            foreach (var signature in new[] { "same-sig", "different-sig" })
            {
                foreach (var call in new[] { "fits-first", "fits-second", "none" })
                {
                    var secondType = signature == "same-sig" ? "int" : "str";
                    var secondArg = signature == "same-sig" ? "1" : "\"a\"";
                    var arg = call == "fits-first" ? "1" : secondArg;
                    var lines = new List<string> { "def helper() -> None:" };
                    int secondDefLine = 0, callLine = 0, callIndent = 0;

                    void Def(int indent, bool second)
                    {
                        if (second)
                            secondDefLine = lines.Count + 1;
                        lines.Add(new string(' ', indent) + (second ? $"def f(x: {secondType}) -> str:" : "def f(x: int) -> str:"));
                        lines.Add(new string(' ', indent + 4) + (second ? "return \"second\"" : "return \"first\""));
                    }

                    void Call(int indent, string argument)
                    {
                        callLine = lines.Count + 1;
                        callIndent = indent;
                        lines.Add(new string(' ', indent) + $"print(f({argument}))");
                    }

                    switch (relation)
                    {
                        case "same":
                            Def(4, false);
                            Def(4, true);
                            if (call != "none")
                                Call(4, arg);
                            break;
                        case "sibling":
                            lines.Add("    if True:");
                            Def(8, false);
                            if (call == "fits-first")
                                Call(8, "1");
                            lines.Add("    if True:");
                            Def(8, true);
                            if (call == "fits-second")
                                Call(8, secondArg);
                            break;
                        case "sibling-else":
                            lines.Add("    for k in range(2):");
                            lines.Add("        if k == 0:");
                            Def(12, false);
                            if (call == "fits-first")
                                Call(12, "1");
                            lines.Add("        else:");
                            Def(12, true);
                            if (call == "fits-second")
                                Call(12, secondArg);
                            break;
                        case "sibling-except":
                            lines.Add("    try:");
                            Def(8, false);
                            if (call == "fits-first")
                                Call(8, "1");
                            lines.Add("        raise ValueError(\"go\")");
                            lines.Add("    except ValueError:");
                            Def(8, true);
                            if (call == "fits-second")
                                Call(8, secondArg);
                            break;
                        case "sibling-match":
                        case "sibling-match-break":
                            lines.Add("    for v in range(2):");
                            lines.Add("        match v:");
                            lines.Add("            case 0:");
                            Def(16, false);
                            if (call == "fits-first")
                                Call(16, "1");
                            lines.Add("            case _:");
                            Def(16, true);
                            if (call == "fits-second")
                                Call(16, secondArg);
                            if (relation == "sibling-match-break")
                                lines.Add("                break");
                            break;
                        case "outer-first":
                            Def(4, false);
                            lines.Add("    if True:");
                            Def(8, true);
                            if (call != "none")
                                Call(4, arg);
                            break;
                        case "inner-first":
                            lines.Add("    if True:");
                            Def(8, false);
                            if (call != "none")
                                Call(8, arg);
                            Def(4, true);
                            break;
                    }

                    lines.AddRange(new[] { "", "def main() -> None:", "    helper()", "    print(\"end\")", "" });
                    var source = string.Join("\n", lines);

                    if (relation.StartsWith("sibling", StringComparison.Ordinal))
                    {
                        var stdout = call switch { "fits-first" => "first\nend\n", "fits-second" => "second\nend\n", _ => "end\n" };
                        yield return new NestedDefCell(relation, signature, call, source, Array.Empty<(string, int, int)>(), stdout);
                        continue;
                    }

                    var secondDefIndent = lines[secondDefLine - 1].Length - lines[secondDefLine - 1].TrimStart().Length;
                    var expected = new List<(string, int, int)> { ("SPY0204", secondDefLine, secondDefIndent + 5) };
                    if (signature == "different-sig" && call == "fits-second")
                        expected.Add(("SPY0220", callLine, callIndent + "print(f(".Length + 1));
                    yield return new NestedDefCell(relation, signature, call, source, expected, null);
                }
            }
        }
    }

    [Fact]
    public void NestedDefRedeclaration_HasOneVerdictPerBlockRelation()
    {
        var allowlist = LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();
        foreach (var cell in NestedDefCells())
        {
            var result = api.Analyze(cell.Source, CancellationToken.None);
            var line = $"{cell.Id}: {Fingerprint(result.Diagnostics)}";
            Output.WriteLine(line);
            var failures = new List<string>();

            if (result.Diagnostics.Any(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)))
                problems.Add($"{line} — an internal error");

            var expected = cell.ExpectedErrors.Select(e => $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}")
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                failures.Add($"errors are [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]");

            if (cell.ExpectedStdout is { } stdout)
            {
                var run = CompileAndExecute(cell.Source);
                var internalCodes = InternalCodes(run);

                // A row citing the local-function collision issue claims the switch-lowered `match` arms' C# collision:
                // the run must fail with SPY0908 carrying CS0128 and nothing else. Any other internal error — in that
                // row or any other — is red, never absorbed.
                var collisionRow = allowlist.TryGetValue(cell.Id, out var collisionCite)
                    && collisionCite.Contains(LocalFunctionCollisionIssue, StringComparison.Ordinal);
                if (collisionRow && !IsLocalFunctionCollision(run, internalCodes))
                    problems.Add($"{cell.Id}: allowlisted as the local-function collision CS0128 ({LocalFunctionCollisionIssue}), but under run "
                        + $"the cell is success={run.Success} [{Fingerprint(internalCodes)}] {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}");
                else if (!collisionRow && run.RawDiagnostics.Any(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)))
                    problems.Add($"{cell.Id}: an internal error under run: {Fingerprint(run.RawDiagnostics)}");
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        Output.WriteLine($"{NestedDefCells().Count()} nested-def cells, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The allowlist ratchet.
    // ---------------------------------------------------------------------------------------------------------------

    internal const string AllowlistFileName = "declaration-position-allowlist.txt";
    private static readonly Regex IssueCite = new(@"#\d+", RegexOptions.CultureInvariant);

    private static void Ratchet(string id, List<string> failures, IReadOnlyDictionary<string, string> allowlist,
        List<string> problems, List<string> red)
    {
        var listed = allowlist.TryGetValue(id, out var cite);
        if (failures.Count > 0 && listed)
            red.Add(id);
        else if (failures.Count > 0)
            problems.Add($"{id}: {string.Join("; ", failures)} — not in Semantic/{AllowlistFileName}");
        else if (listed)
            problems.Add($"{id}: passes but is allowlisted ({cite}) — STALE: delete its row from Semantic/{AllowlistFileName}");
    }

    /// <summary>Cell id → cite. A row must cite an issue (<c>#\d+</c>), name a generated cell, and appear once.</summary>
    private static Dictionary<string, string> LoadAllowlist(out List<string> problems)
    {
        problems = new List<string>();
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Semantic/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var universe = new HashSet<string>(Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id)), StringComparer.Ordinal);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var id = split < 0 ? line : line.Substring(0, split).Trim();
            var cite = split < 0 ? "" : line.Substring(split + 2).Trim();
            if (!IssueCite.IsMatch(cite))
                problems.Add($"Semantic/{AllowlistFileName}: '{line}' must cite an issue (#N)");
            if (!universe.Contains(id))
                problems.Add($"Semantic/{AllowlistFileName}: '{id}' names no generated cell");
            if (!entries.TryAdd(id, cite))
                problems.Add($"Semantic/{AllowlistFileName}: '{id}' is listed twice");
        }

        return entries;
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Semantic");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    /// <summary>The issue the switch-lowered sibling-<c>match</c> rows cite: a nested def per arm collides in C#.</summary>
    private const string LocalFunctionCollisionIssue = "#2361";

    /// <summary>That issue's fingerprint: the run fails, its only internal diagnostics are SPY0908, and one names
    /// CS0128 (two local functions <c>F</c> in the one declaration space of a C# <c>switch</c>).</summary>
    private static bool IsLocalFunctionCollision(ExecutionResult run, List<CompilerDiagnostic> internalCodes)
        => !run.Success
           && internalCodes.Count > 0
           && internalCodes.All(d => d.Code == "SPY0908")
           && internalCodes.Any(d => d.Message.Contains("CS0128", StringComparison.Ordinal));

    /// <summary>#2021's fingerprint: the run fails, its only internal diagnostics are SPY0908, and one names CS0100.</summary>
    private static bool IsStructCtorDuplicate(ExecutionResult run, List<CompilerDiagnostic> internalCodes)
        => !run.Success
           && internalCodes.Count > 0
           && internalCodes.All(d => d.Code == "SPY0908")
           && internalCodes.Any(d => d.Message.Contains("CS0100", StringComparison.Ordinal));

    private static List<CompilerDiagnostic> InternalCodes(ExecutionResult run)
        => run.RawDiagnostics.Where(d => (d.Code ?? "").StartsWith("SPY05", StringComparison.Ordinal)
            || (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)).ToList();

    private static string Fingerprint(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join(" ", diagnostics.Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>The member names of the module-level <c>Foo</c> (the prelude's first line, column 1).</summary>
    private static string FooMembers(SemanticResult result)
    {
        var foo = result.SemanticQuery?.FindSymbolByDeclaration("Foo", 1, 1) as global::Sharpy.Compiler.Semantic.TypeSymbol
            ?? throw new InvalidOperationException("the module-level class Foo has no symbol");
        return string.Join(",", foo.Fields.Select(f => $"field:{f.Name}={f.ConstantValue}/{f.IsCompileTimeConstant}")
            .Concat(foo.Properties.Select(p => $"property:{p.Name}"))
            .Concat(foo.Methods.Select(m => $"method:{m.Name}"))
            .Concat(foo.Constructors.Select(c => $"ctor:{c.Parameters.Count}"))
            .Append($"dataclass:{foo.IsDataclass}")
            .OrderBy(s => s, StringComparer.Ordinal));
    }
}
