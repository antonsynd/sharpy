using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
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
/// <para><b>P21f</b> (#2360, #2362, #2359; rulings R-IJ, R-IK, R-II) extends the contract to three more declaration
/// kinds: a MEMBER declaration (<c>property</c>, <c>event</c>) outside a type body is SPY0708 (a property is also legal
/// at module level); an IMPORT binding is a module binding that collides like any other (SPY0204), refuses a builtin
/// type name (SPY0212) and warns on a builtin function name (SPY0484) — <see cref="ImportBindingCells"/>; a <c>type</c>
/// alias RE-DECLARED in one scope is SPY0204 at every position — <see cref="AliasRedeclarationCells"/>.</para>
///
/// <para><b>Matrix</b> (generated). Kind (18 = P21e's 12 + P21f's six member kinds): <c>class</c> and <c>struct</c> (each carrying the mixed auto/setter
/// property whose backing field was P54's measured write), <c>@dataclass class</c>, <c>interface</c>, <c>union</c>,
/// <c>delegate</c>, <c>class-const</c> (a non-constant <c>const X</c>, whose body <c>ConstEligibility</c> once walked
/// against the enclosing <c>Foo</c>), <c>enum</c>, <c>alias</c> (<c>type N = str</c>; the builtin column spells
/// <c>int</c> so target ≠ name), <c>import</c> (<c>import copy as N</c>; Fresh = plain <c>import copy</c>),
/// <c>from-import</c> (<c>from copy import deepcopy as N</c>; Fresh = plain) — Core's <c>copy</c>, not the plan's
/// <c>math</c>: this harness references Sharpy.Core only, where <c>math</c> is SPY0300 (the CLI measured both modules
/// to the same silent shapes) — <c>class-cascade</c> (a class that
/// legitimately draws SPY0701 — a SAME-CLR-key duplicate <c>__getitem__</c> pair — and SPY0325 — <c>(IShape)</c> with
/// <c>area</c> unimplemented; a different-key pair draws no SPY0701, measured), and the member kinds
/// <c>property-field</c> (<c>property N: int = 0</c>), <c>property-get</c>, <c>property-set</c> (accessors spell
/// <c>self</c> only at <c>type-body</c>/<c>type-body-if</c>; module-level and function-hosted accessors take none),
/// <c>property-observer</c> (the field form + <c>before_set(v):</c>, analysed and run with the experimental
/// <c>property_observers</c> feature per cell), <c>event-auto</c> (<c>event N: Action[int]</c>) and
/// <c>event-add</c> (an <c>event add</c>/<c>event remove</c> pair with <c>self</c>). × Position (13): <c>module</c>,
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
/// column; warnings on those lines are not compared, except for the member kinds: (2w) their builtin-name shadowing
/// warnings (<see cref="VerdictWarnings"/>) on those lines equal <see cref="ExpectedWarnings"/>, and (2s) no SPY0232
/// ("Module-level properties cannot have a 'self' parameter") appears anywhere in a member cell not at
/// <c>module</c>/<c>module-try</c> — its positive control, a module-level <c>property get size(self)</c> showing
/// SPY0232, is asserted unconditionally. (3) Every diagnostic off the definition's lines equals the
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
/// generated cell is red. Clauses (1), (4), the positive controls and the control sanity check are never exempted.
/// P21e's rows drained; the rows left cite issues outside the contract, and P21f's rows (created by its Phase 1)
/// drain across its Phases 2–4.</para>
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
        Member,
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

        /// <summary><c>event-add</c>'s <c>event remove</c> accessor (keyword line + 2, the keyword's column).</summary>
        SecondAccessor,

        /// <summary>The import's <c>ImportAlias</c> node — the module name of <c>import m as N</c>, the imported name of
        /// <c>from m import x as N</c> (where SPY0484 lands, and where an import-binding verdict lands, D4).</summary>
        ImportAlias,
    }

    /// <summary>A kind of the matrix: its family, the definition's lines for a spelled name (unindented), the lines a
    /// type-body position spells instead (a member accessor takes <c>self</c> only in a type body — module-level and
    /// function-hosted spellings take none, so SPY0232 never enters an expected set), and the experimental features the
    /// kind needs (the observer form's <c>property_observers</c>, passed per cell to <c>Analyze</c> and the run).</summary>
    internal sealed record KindSpec(
        string Kind, Family Family, Func<string, string[]> Lines, Func<string, string[]>? TypeBodyLines = null,
        FeatureFlags? Features = null);

    private static readonly FeatureFlags ObserverFeatures = FeatureFlags.None.Enable("property_observers");

    /// <summary>The kinds of the position matrix: P21e's 12 and P21f's six member kinds.</summary>
    private static readonly KindSpec[] Kinds =
    {
        new("class", Family.TypeDefinition, n => new[]
        {
            $"class {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        new("dataclass", Family.TypeDefinition, n => new[]
        {
            "@dataclass",
            $"class {n}:",
            "    x: int",
        }),
        new("struct", Family.TypeDefinition, n => new[]
        {
            $"struct {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        new("interface", Family.TypeDefinition, n => new[]
        {
            $"interface {n}:",
            "    def probe(self, value: int) -> int: ...",
        }),
        new("union", Family.TypeDefinition, n => new[]
        {
            $"union {n}:",
            "    case Probe(value: int)",
        }),
        new("delegate", Family.TypeDefinition, n => new[]
        {
            $"delegate {n}(value: int) -> int",
        }),
        new("class-const", Family.TypeDefinition, n => new[]
        {
            $"class {n}:",
            "    const X: int = len(\"abc\")",
        }),
        new("enum", Family.TypeDefinition, n => new[]
        {
            $"enum {n}:",
            "    A = 1",
        }),
        new("alias", Family.Alias, n => new[]
        {
            $"type {n} = str",
        }),
        new("import", Family.Import, n => new[]
        {
            n == "Fresh" ? "import copy" : $"import copy as {n}",
        }),
        new("from-import", Family.Import, n => new[]
        {
            n == "Fresh" ? "from copy import deepcopy" : $"from copy import deepcopy as {n}",
        }),
        new("class-cascade", Family.TypeDefinition, n => new[]
        {
            $"class {n}(IShape):",
            "    def __getitem__(self, key: int) -> str:",
            "        return \"a\"",
            "    def __getitem__(self, key: int) -> int:",
            "        return 0",
        }),

        // P21f (#2362, R-IK): the member kinds. A property is legal in a type body and at module level (#844); an
        // event only in a type body; anywhere else both are SPY0708.
        new("property-field", Family.Member, n => new[]
        {
            $"property {n}: int = 0",
        }),
        new("property-get", Family.Member, n => new[]
        {
            $"property get {n}() -> int:",
            "    return 0",
        }, TypeBodyLines: n => new[]
        {
            $"property get {n}(self) -> int:",
            "    return 0",
        }),
        // The setter carries no `-> None`: a set-only property annotated `-> None` takes `void` as its type (CS0547, a
        // finding of its own) — the spec's spelling is the unannotated one (properties_function_style.md).
        new("property-set", Family.Member, n => new[]
        {
            $"property set {n}(v: int):",
            "    print(v)",
        }, TypeBodyLines: n => new[]
        {
            $"property set {n}(self, v: int):",
            "    print(v)",
        }),
        new("property-observer", Family.Member, n => new[]
        {
            $"property {n}: int = 0",
            "    before_set(v):",
            "        print(v)",
        }, Features: ObserverFeatures),
        new("event-auto", Family.Member, n => new[]
        {
            $"event {n}: Action[int]",
        }),
        // A function-style event is an add/remove PAIR — an `event add` alone is SPY0420 in a type body — and each
        // accessor is its own statement, so a refusal is one SPY0708 per accessor. Event accessors have no
        // module-level (self-less) form, so `self` is spelled at every position.
        new("event-add", Family.Member, n => new[]
        {
            $"event add {n}(self, handler: Action[int]):",
            "    pass",
            $"event remove {n}(self, handler: Action[int]):",
            "    pass",
        }),
    };

    /// <summary>The spelling-axis edge kinds: a <c>@suppress</c>-decorated class and import.</summary>
    private static readonly KindSpec[] SpellingKinds =
    {
        new("suppress-class", Family.TypeDefinition, n => new[]
        {
            "@suppress(\"SPY0453\")",
            $"class {n}:",
            "    x: int = 0",
        }),
        new("suppress-import", Family.Import, n => new[]
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
        IReadOnlyList<(string Code, int Line, int Column)> BlockErrors, bool Edge, FeatureFlags Features,
        int ImportAliasColumn)
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
        KindSpec kind, Position position,
        (string Name, string[] Prelude, string[] MainBody) name, string spelled, bool edge)
    {
        var prelude = name.Prelude.Concat(SharedPrelude).ToArray();
        var lines = kind.TypeBodyLines != null && position.Name is "type-body" or "type-body-if"
            ? kind.TypeBodyLines(spelled)
            : kind.Lines(spelled);
        var definition = lines.Select(l => new string(' ', position.Indent) + l).ToArray();
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
            first + keywordOffset, nameIndex < 0 ? 0 : nameIndex + 2, blockErrors, edge,
            kind.Features ?? FeatureFlags.None, ImportAliasColumn(keywordLine));
    }

    /// <summary>The 1-based column of an import line's <c>ImportAlias</c> node: the module name after <c>import </c>, or the
    /// imported name after <c>from m import </c>; 0 for a line that is not an import.</summary>
    internal static int ImportAliasColumn(string line)
    {
        var indent = line.Length - line.TrimStart().Length;
        var text = line.TrimStart();
        if (text.StartsWith("import ", StringComparison.Ordinal))
            return indent + "import ".Length + 1;
        if (text.StartsWith("from ", StringComparison.Ordinal))
        {
            var at = text.IndexOf(" import ", StringComparison.Ordinal);
            return at < 0 ? 0 : indent + at + " import ".Length + 1;
        }

        return 0;
    }

    /// <summary>The 18 × 13 × 3 matrix cells.</summary>
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
    /// <item><term><c>import</c>/<c>from-import</c>/<c>suppress-import</c></term><description><c>module</c> → an
    /// import binding is a module binding (R-II, #2359): builtin (<c>str</c>) → [SPY0212 @ ImportAlias] (a builtin type
    /// name; the backtick-escaped alias is the opt-in), Foo → [SPY0204 @ ImportAlias] (beside <c>class Foo</c>, which is
    /// declared before imports bind), Fresh → []; every other position → [SPY0708 @ keyword] (R-CS: "import at module
    /// level").</description></item>
    /// <item><term>member — <c>property-field</c>/<c>-get</c>/<c>-set</c>/<c>-observer</c> (P21f D3/D7a, R-IK)</term>
    /// <description><c>type-body</c> → [] for all three names (a member named <c>str</c> or <c>Foo</c> is reached
    /// through <c>self.</c>); <c>module</c> → [] for builtin and Fresh (module-level properties are spec'd, #844), [SPY0204
    /// @ keyword] for Foo ("Property 'Foo' is already defined" — NameResolver's module-property duplicate rule,
    /// measured at the base); every other position → [SPY0708 @ keyword] (refused by position: no SPY0232 "Module-level
    /// properties…", no checker verdict on the body).</description></item>
    /// <item><term>member — <c>event-auto</c>/<c>event-add</c></term><description><c>type-body</c> → []; every other
    /// position INCLUDING <c>module</c> → [SPY0708 @ keyword] — one per accessor statement, so <c>event-add</c>'s
    /// add/remove pair is [SPY0708 @ keyword, SPY0708 @ the <c>event remove</c> keyword] (no SPY0340 "Executable
    /// statements…" at module: the verdict has one owner).</description></item>
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
                if (position != "module")
                    return new[] { ("SPY0708", At.Keyword) };
                if (builtin)
                    return new[] { ("SPY0212", At.ImportAlias) };
                return name == "Foo" ? new[] { ("SPY0204", At.ImportAlias) } : Array.Empty<(string, At)>();

            case Family.Member:
                {
                    if (position == "type-body")
                        return Array.Empty<(string, At)>();
                    var isEvent = kind.StartsWith("event-", StringComparison.Ordinal);
                    if (position == "module" && !isEvent)
                        return name == "Foo" ? new[] { ("SPY0204", At.Keyword) } : Array.Empty<(string, At)>();
                    return kind == "event-add"
                        ? new[] { ("SPY0708", At.Keyword), ("SPY0708", At.SecondAccessor) }
                        : new[] { ("SPY0708", At.Keyword) };
                }

            default:
                throw new InvalidOperationException(kind);
        }
    }

    /// <summary>The builtin-name shadowing verdicts — the only warnings the P21f clauses compare. Naming (SPY0453) and
    /// usage (SPY0451/SPY0452) lints are not verdicts of this contract and are not compared.</summary>
    internal static readonly HashSet<string> VerdictWarnings = new(StringComparer.Ordinal) { "SPY0483", "SPY0484" };

    /// <summary>
    /// The WARNING verdicts on the definition's lines, compared (as a multiset over <see cref="VerdictWarnings"/>) for
    /// the member kinds only — P21e's kinds keep their error-only oracle. A module-level property is a value-position
    /// module binding, so a builtin FUNCTION-or-type spelling warns SPY0483 like <c>len = 5</c> (D5, f3: measured silent
    /// at the base); a class member is reached through <c>self.</c> and stays silent; a refused member (SPY0708) draws
    /// nothing else.
    /// </summary>
    internal static IReadOnlyList<(string Code, At Where)> ExpectedWarnings(string kind, string position, string name)
    {
        if (FamilyOf(kind) != Family.Member)
            return Array.Empty<(string, At)>();
        var isProperty = kind.StartsWith("property-", StringComparison.Ordinal);
        return isProperty && position == "module" && BuiltinSpellings.Contains(name)
            ? new[] { ("SPY0483", At.Name) }
            : Array.Empty<(string, At)>();
    }

    private static (int Line, int Column) Resolve(Cell cell, At where) => where switch
    {
        At.Keyword => (cell.DefinitionLine, cell.Indent + 1),
        At.Name => (cell.NameLine, cell.NameColumn),
        At.SecondDunder => (cell.DefinitionLine + 3, cell.Indent + 5),
        At.SecondAccessor => (cell.DefinitionLine + 2, cell.Indent + 1),
        At.ImportAlias => (cell.DefinitionLine, cell.ImportAliasColumn),
        _ => throw new InvalidOperationException(where.ToString()),
    };

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(18, Kinds.Length);
        Assert.Equal(6, Kinds.Count(k => k.Family == Family.Member));
        Assert.Equal(13, Positions.Length);
        Assert.Equal(3, Names.Length);
        Assert.Equal(18 * 13 * 3, MatrixCells().Count());
        Assert.Equal(6 * 13 * 3, MatrixCells().Count(c => c.Family == Family.Member));
        // Host axis: 4 hosts × {class, enum, import, alias} × Fresh + alias × int; spelling axis: 3 spellings × 2 positions.
        Assert.Equal(4 * 5 + 3 * 2, EdgeCells().Count());
        Assert.Equal(8 * 2 * 3, NestedDefCells().Count());
        // Import binding: 4 spellings × (fresh + repeated import + 8 colliders × 2 orders), + 2 `as` spellings ×
        // {str, print, `str`}, + 2 builtins-module names, + the plain builtin type name, + the dotted root × 2 orders,
        // + the star import × {var, const, def} × 2 orders.
        Assert.Equal(4 * (2 + 8 * 2) + 2 * 3 + 2 + 1 + 2 + 3 * 2, ImportBindingCells().Count());
        // Alias redeclaration: 7 relations × {def-body, if-in-def, method-body, module-try} + alias→alias × {type-body, module}.
        Assert.Equal(7 * 4 + 2, AliasRedeclarationCells().Count());
        var ids = Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id))
            .Concat(ImportBindingCells().Select(c => c.Id)).Concat(AliasRedeclarationCells().Select(c => c.Id)).ToList();
        Assert.Equal(18 * 13 * 3 + 26 + 48 + 89 + 30, ids.Distinct(StringComparer.Ordinal).Count());
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

        // The positive control of the SPY0232-absence clause (P21f): the probe — any SPY0232 in the result — hits a
        // module-level accessor that spells `self` (measured at the base), so its absence in a member cell is not
        // vacuous. Hard: never allowlisted.
        var selfControl = api.Analyze(SelfAccessorControlSource, CancellationToken.None);
        if (!selfControl.Diagnostics.Any(IsSelfAtModuleLevel))
            problems.Add($"the SPY0232 positive control (a module-level `property get size(self)`) draws no SPY0232 "
                + $"[{Fingerprint(selfControl.Diagnostics)}]: the member cells' SPY0232-absence clause is vacuous");

        foreach (var cell in Cells())
        {
            var options = OptionsFor(cell.Features);
            var result = api.Analyze(cell.Source, options, CancellationToken.None);
            var controlKey = $"{string.Join(",", cell.Features.EnabledFeatures)}|{cell.Control}";
            if (!controls.TryGetValue(controlKey, out var control))
                controls[controlKey] = control = api.Analyze(cell.Control, options, CancellationToken.None);
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

            if (cell.Family == Family.Member)
            {
                // (2w) the builtin-name shadowing WARNINGS on the definition's lines (member kinds only).
                var expectedWarnings = ExpectedWarnings(cell.Kind, cell.Position, cell.Name)
                    .Select(e => { var (l, c) = Resolve(cell, e.Where); return $"{e.Code}@{l}:{c}"; })
                    .OrderBy(s => s, StringComparer.Ordinal).ToList();
                var actualWarnings = result.Diagnostics
                    .Where(d => !d.IsError && OnDefinition(d) && VerdictWarnings.Contains(d.Code ?? ""))
                    .Select(d => $"{d.Code}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
                if (!expectedWarnings.SequenceEqual(actualWarnings, StringComparer.Ordinal))
                    failures.Add($"shadowing warnings on the definition are [{string.Join(" ", actualWarnings)}], "
                        + $"expected [{string.Join(" ", expectedWarnings)}]");

                // (2s) no "Module-level properties cannot have a 'self' parameter" for a host that is not module
                // level — anywhere in the program; armed by the positive control above.
                if (cell.Position is not ("module" or "module-try"))
                {
                    var selfErrors = result.Diagnostics.Where(IsSelfAtModuleLevel).ToList();
                    if (selfErrors.Count > 0)
                        failures.Add($"SPY0232 names a module-level host that is not module level: {Fingerprint(selfErrors)}");
                }
            }

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
                var run = CompileAndExecute(cell.Runnable, features: cell.Features);
                var internalCodes = InternalCodes(run);
                string? cellRun = null, controlRun = null;
                if (!run.Success || internalCodes.Count > 0 || run.StandardOutput != "ok\n")
                {
                    // Attribute the failure: the control (the same host, `pass` for the definition) either runs — the
                    // declaration's failure — or fails the same way — the host's.
                    var controlExec = CompileAndExecute(cell.RunnableControl, features: cell.Features);
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

    /// <summary>The SPY0232 positive control: a module-level accessor that spells <c>self</c>.</summary>
    private const string SelfAccessorControlSource =
        "property get size(self) -> int:\n    return 0\n\ndef main() -> None:\n    print(\"ok\")\n";

    /// <summary>The probe of the SPY0232-absence clause, shared with its positive control.</summary>
    private static bool IsSelfAtModuleLevel(CompilerDiagnostic d) => d.Code == "SPY0232";

    /// <summary>The library-analysis options <c>Analyze</c> uses by default, with the cell's experimental features.</summary>
    private static CompilerOptions OptionsFor(FeatureFlags features)
    {
        var options = CompilerOptionsFactory.ForLibraryAnalysis();
        options.Features = features;
        return options;
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
    // The import-binding sub-matrix (P21f D4/D5/D7b, R-II, #2359).
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>Set by the import-binding test: its cells import Stdlib's <c>math</c> and <c>os</c> (the plan's measured
    /// programs), so its runs reference Sharpy.Stdlib as well as Sharpy.Core. xUnit builds one instance per test, so the
    /// flag never reaches another test's runs.</summary>
    private bool _withStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => _withStdlib ? new[] { SharpyStdlibReference.Location } : Enumerable.Empty<string>();

    internal sealed record ImportBindingCell(
        string Spelling, string Collider, string Order, string Source,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedWarnings, string? ExpectedStdout)
    {
        public string Id => $"import-binding × {Spelling} × {Collider} × {Order}";
    }

    /// <summary>The four import spellings: (id, the import line for a bound name, the name a plain spelling binds).</summary>
    private static readonly (string Name, Func<string, string> Line, string? FixedName)[] ImportSpellings =
    {
        ("import-as", n => $"import math as {n}", null),
        ("import", _ => "import math", "math"),
        ("from-import-as", n => $"from math import pi as {n}", null),
        ("from-import", _ => "from math import pi", "pi"),
    };

    /// <summary>The colliders spelled beside the import, in both source orders: a module-level declaration of every
    /// kind, a module variable, and another module's import under the same name in both import spellings.</summary>
    private static readonly (string Name, Func<string, string[]> Lines)[] ImportColliders =
    {
        ("class", n => new[] { $"class {n}:", "    pass" }),
        ("def", n => new[] { $"def {n}() -> int:", "    return 1" }),
        ("alias", n => new[] { $"type {n} = int" }),
        ("const", n => new[] { $"const {n}: int = 1" }),
        ("property", n => new[] { $"property {n}: int = 0" }),
        ("var", n => new[] { $"{n}: int = 1" }),
        ("other-import", n => new[] { $"import copy as {n}" }),
        ("other-from-import", n => new[] { $"from copy import deepcopy as {n}" }),
    };

    /// <summary>
    /// Import spelling {<c>import math as N</c>, <c>import math</c> (N = <c>math</c>), <c>from math import pi as N</c>,
    /// <c>from math import pi</c> (N = <c>pi</c>)} × collider {fresh, <c>class N</c>, <c>def N</c>, <c>type N = int</c>,
    /// <c>const N</c>, <c>property N</c>, <c>N: int = 1</c>, the same import again, <c>import copy as N</c>,
    /// <c>from copy import deepcopy as N</c>} × order {collider first, import first} (fresh and the repeated import have
    /// one order), plus — for the <c>as</c> spellings — the bound name {<c>str</c>, <c>print</c>, <c>`str`</c>}, the
    /// <c>builtins</c> module's own bindings <c>from builtins import len</c> / <c>str</c>, and the dotted root
    /// <c>import os</c> + <c>import os.path</c> in both orders; and the star spelling <c>from math import *</c> ×
    /// {module variable, const, def} named <c>pi</c>, both orders. Every program's <c>main</c> prints <c>ok</c> (nothing
    /// else uses the binding, D8), except the <c>print</c> cells (whose <c>main</c> is <c>pass</c>: the rebinding is
    /// honoured) and the dotted root (which prints <c>len(os.path.join("a", "b"))</c>).
    ///
    /// <para><b>Expected</b> (D4/D5). The pipeline declares the module body BEFORE it binds imports, so an import is
    /// always the later binding against a declaration, whatever the source order: class / def / alias / const /
    /// property → [SPY0204 @ the import's ImportAlias] in BOTH orders. A module variable is declared by the checker
    /// AFTER the imports, so it is the later binding in both orders: [SPY0204 @ the variable] (c18, pinned). Two imports
    /// bind in source order: [SPY0204 @ the later one's ImportAlias] (c15, e5, g2; d11 is the from/from cell). A
    /// builtin type name under an <c>as</c> spelling → [SPY0212 @ ImportAlias] (the escaped <c>`str`</c> is the opt-in: []),
    /// under a PLAIN from-import (<c>from System.Collections.Generic import IList</c> — the exporter's name) → errors [],
    /// warnings [SPY0484 @ ImportAlias], and it runs; a builtin function name
    /// → errors [], warnings [SPY0484 @ ImportAlias] (c4/c9); the <c>builtins</c> module's own binding shadows nothing
    /// → [] with no SPY0484 (d4/d5) — the absence's positive control is the <c>from-import-as × print</c> cell's
    /// SPY0484, asserted unconditionally (hard). Fresh, the repeated import (idempotent) and the dotted root (merge) →
    /// [] and run. A star import refuses no declaration: the star cells are [] and print the LOCAL's <c>1</c>, not
    /// <c>math.pi</c> (star × property is left out — the import silently wins there, #2371). Warnings are compared over
    /// <see cref="VerdictWarnings"/>; errors over the whole program.</para>
    /// </summary>
    internal static IEnumerable<ImportBindingCell> ImportBindingCells()
    {
        const string bound = "mm";
        string[] Main(string body) => new[] { "", "def main() -> None:", "    " + body, "" };

        ImportBindingCell Make(string spelling, string collider, string order, string[] first, string[] second,
            Func<int, int, (string, int, int)[]> errors, Func<int, int, (string, int, int)[]>? warnings = null,
            string body = "print(\"ok\")", string? stdout = "ok\n")
        {
            var lines = first.Concat(second).Concat(Main(body)).ToArray();
            // 1-based lines of the first and second group's first line.
            var firstLine = 1;
            var secondLine = first.Length + 1;
            var expected = errors(firstLine, secondLine);
            return new ImportBindingCell(spelling, collider, order, string.Join("\n", lines), expected,
                warnings?.Invoke(firstLine, secondLine) ?? Array.Empty<(string, int, int)>(),
                expected.Length == 0 ? stdout : null);
        }

        foreach (var spelling in ImportSpellings)
        {
            var name = spelling.FixedName ?? bound;
            var import = spelling.Line(name);
            var aliasColumn = ImportAliasColumn(import);
            var none = Array.Empty<(string, int, int)>();

            yield return Make(spelling.Name, "fresh", "none", Array.Empty<string>(), new[] { import }, (_, _) => none);
            yield return Make(spelling.Name, "same-import", "twice", new[] { import }, new[] { import }, (_, _) => none);

            foreach (var collider in ImportColliders)
            {
                var colliderLines = collider.Lines(name);
                foreach (var order in new[] { "collider-first", "import-first" })
                {
                    var colliderFirst = order == "collider-first";
                    var first = colliderFirst ? colliderLines : new[] { import };
                    var second = colliderFirst ? new[] { import } : colliderLines;
                    Func<int, int, (string, int, int)[]> errors = collider.Name switch
                    {
                        // The variable is the later binding in both orders; SPY0204 at its name (column 1).
                        "var" => (f, s) => new[] { ("SPY0204", colliderFirst ? f : s, 1) },
                        // Two imports bind in source order: the later one is refused.
                        "other-import" or "other-from-import" => (f, s) => new[]
                        {
                            ("SPY0204", s, ImportAliasColumn(second[0])),
                        },
                        // A declaration is bound first whatever the order: the import is refused.
                        _ => (f, s) => new[] { ("SPY0204", colliderFirst ? s : f, aliasColumn) },
                    };
                    yield return Make(spelling.Name, collider.Name, order, first, second, errors);
                }
            }

            if (spelling.FixedName != null)
                continue;

            // The bound name spells a builtin: a type name is refused, a function name warns, the escape opts in.
            var str = spelling.Line("str");
            yield return Make(spelling.Name, "str", "none", Array.Empty<string>(), new[] { str },
                (_, s) => new[] { ("SPY0212", s, ImportAliasColumn(str)) });
            var print = spelling.Line("print");
            yield return Make(spelling.Name, "print", "none", Array.Empty<string>(), new[] { print },
                (_, _) => none, (_, s) => new[] { ("SPY0484", s, ImportAliasColumn(print)) }, body: "pass", stdout: "");
            yield return Make(spelling.Name, "escaped-str", "none", Array.Empty<string>(), new[] { spelling.Line("`str`") },
                (_, _) => none);
        }

        // A star import keeps its rule (outside R-II's four spellings): a module-level declaration of a star-imported
        // name takes it over, in either order — a module variable re-declares the star-imported default like a const
        // or a def does (R1 of the P21f verify round: the module-variable cell ran `1` at the base and must again).
        // A module property is left out: star × property is the pre-existing silent-wrong #2371 (the import wins).
        foreach (var collider in ImportColliders.Where(c => c.Name is "var" or "const" or "def"))
        {
            foreach (var order in new[] { "collider-first", "import-first" })
            {
                var colliderLines = collider.Lines("pi");
                var star = new[] { "from math import *" };
                yield return Make("from-import-star", collider.Name, order,
                    order == "collider-first" ? colliderLines : star, order == "collider-first" ? star : colliderLines,
                    (_, _) => Array.Empty<(string, int, int)>(),
                    body: collider.Name == "def" ? "print(pi())" : "print(pi)", stdout: "1\n");
            }
        }

        // The builtins module's own binding (#1322) shadows nothing.
        foreach (var name in new[] { "len", "str" })
            yield return Make("from-builtins", name, "none", Array.Empty<string>(), new[] { $"from builtins import {name}" },
                (_, _) => Array.Empty<(string, int, int)>());

        // A PLAIN from-import whose exporter's name spells a builtin TYPE (the registry knows `IList`) is warned and
        // honoured, not refused: the importer did not choose the name (owner ruling at P21f Phase 4, option b — the
        // refusal is the `as` spellings' only, the `str` cells above). The project-route twin is `from lib import
        // double` (ImportBindingProjectRouteTests).
        var plainType = "from System.Collections.Generic import IList";
        yield return Make("from-import", "builtin-type-name", "none", Array.Empty<string>(), new[] { plainType },
            (_, _) => Array.Empty<(string, int, int)>(),
            (_, s) => new[] { ("SPY0484", s, ImportAliasColumn(plainType)) });

        // The dotted root: `import os` and `import os.path` merge into one `os` binding, in both orders (D4 i —
        // dotted-first binds a structural parent with no FilePath).
        foreach (var order in new[] { "root-first", "dotted-first" })
        {
            var first = order == "root-first" ? "import os" : "import os.path";
            var second = order == "root-first" ? "import os.path" : "import os";
            yield return Make("import-dotted", "os-root", order, new[] { first }, new[] { second },
                (_, _) => Array.Empty<(string, int, int)>(), body: "print(len(os.path.join(\"a\", \"b\")))", stdout: "3\n");
        }
    }

    [Fact]
    public void ImportBinding_HasOneVerdictPerCollider()
    {
        _withStdlib = true;
        var allowlist = LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location, SharpyStdlibReference.Location });
        var red = new List<string>();
        var ran = 0;
        var shadowControlArmed = false;
        foreach (var cell in ImportBindingCells())
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

            // The shadowing warnings, over the whole program: the `print` cells' SPY0484 present, every other cell's
            // absent — the builtins-module cells included (their absence is the d4/d5 claim).
            var shadow = result.Diagnostics.Where(d => !d.IsError && VerdictWarnings.Contains(d.Code ?? "")).ToList();
            var expectedWarnings = cell.ExpectedWarnings.Select(e => $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actualWarnings = shadow.Select(d => $"{d.Code}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expectedWarnings.SequenceEqual(actualWarnings, StringComparer.Ordinal))
                failures.Add($"shadowing warnings are [{string.Join(" ", actualWarnings)}], expected [{string.Join(" ", expectedWarnings)}]");

            // The positive control of the builtins-module absence: the same probe (SPY0484 anywhere) hits a builtin
            // function name imported out of a module that is not `builtins` (c9, measured at the base).
            if (cell.Spelling == "from-import-as" && cell.Collider == "print")
                shadowControlArmed = shadow.Any(d => d.Code == "SPY0484");

            if (cell.ExpectedStdout is { } stdout && expected.Count == 0)
            {
                ran++;
                var run = CompileAndExecute(cell.Source);
                if (run.RawDiagnostics.Any(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)
                        || (d.Code ?? "").StartsWith("SPY05", StringComparison.Ordinal)))
                    failures.Add($"an internal error under run: {Fingerprint(InternalCodes(run))}");
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        if (!shadowControlArmed)
            problems.Add("import-binding × from-import-as × print × none draws no SPY0484: the builtins-module cells' SPY0484 absence is vacuous");

        Output.WriteLine($"{ImportBindingCells().Count()} import-binding cells, {ran} executed, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The alias-redeclaration sub-matrix (P21f D2/D2b/D7c, R-IJ, #2360).
    // ---------------------------------------------------------------------------------------------------------------

    internal sealed record AliasRedeclarationCell(
        string Relation, string Position, string Source,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? ExpectedStdout)
    {
        public string Id => $"alias-redeclaration × {Relation} × {Position}";
    }

    /// <summary>(relation, its lines unindented, the 0-based index of the refused line and the column offset of its
    /// diagnostic from the indent — or -1 for a legal relation —, the stdout of a legal relation).</summary>
    private static readonly (string Name, string[] Lines, int Refused, int ColumnOffset, string? Stdout)[] AliasRelations =
    {
        ("alias-then-alias", new[] { "type A = str", "type A = int" }, 1, 1, null),
        ("alias-then-def", new[] { "type A = str", "def A() -> int:", "    return 1" }, 1, 5, null),
        ("def-then-alias", new[] { "def A() -> int:", "    return 1", "type A = str" }, 2, 1, null),
        ("var-then-alias", new[] { "A: int = 1", "type A = str" }, 1, 1, null),
        ("nested-outer-first", new[]
        {
            "type A = str", "if True:", "    type A = int", "    x: A = 5", "    print(x)", "y: A = \"s\"", "print(y)",
        }, -1, 0, "5\ns\n"),
        ("nested-inner-first", new[]
        {
            "if True:", "    type A = int", "    x: A = 5", "    print(x)", "type A = str", "y: A = \"s\"", "print(y)",
        }, -1, 0, "5\ns\n"),
        ("sibling-blocks", new[]
        {
            "if True:", "    type A = str", "    y: A = \"s\"", "    print(y)", "if True:", "    type A = int", "    x: A = 5", "    print(x)",
        }, -1, 0, "s\n5\n"),
    };

    /// <summary>(position, the lines before the relation, its indent, the lines after, main's body, the block's own
    /// error as (code, 0-based index into Before, column) or null).</summary>
    private static readonly (string Name, string[] Before, int Indent, string[] After, string Main, (string Code, int Index, int Column)? Block)[] AliasPositions =
    {
        ("def-body", new[] { "def helper() -> None:" }, 4, Array.Empty<string>(), "helper()", null),
        ("if-in-def", new[] { "def helper() -> None:", "    if True:" }, 8, Array.Empty<string>(), "helper()", null),
        ("method-body", new[] { "class Host:", "    def m(self) -> None:" }, 8, Array.Empty<string>(), "Host().m()", null),
        ("module-try", new[] { "try:" }, 4, new[] { "except Exception:", "    pass" }, "print(\"ok\")", ("SPY0340", 0, 1)),
        ("type-body", new[] { "class Host:", "    tag: int = 0" }, 4, Array.Empty<string>(), "print(\"ok\")", null),
        ("module", Array.Empty<string>(), 0, Array.Empty<string>(), "print(\"ok\")", null),
    };

    /// <summary>
    /// Relation {same-block alias→alias, alias→def, def→alias, variable→alias; nested outer-first, nested inner-first,
    /// sibling blocks} × position {def body, an <c>if</c> in a def, a method body, a module <c>try:</c>} plus
    /// alias→alias at the declaring positions {type body, module} (their controls, a3/a4). A legal relation prints
    /// through both aliases (<c>x: A = 5</c> / <c>y: A = "s"</c>), so a dropped or leaked alias is a SPY0220 or a
    /// different stdout, never the same <c>5</c>/<c>s</c>.
    ///
    /// <para><b>Expected</b> (D2/D2b). An alias re-declared in ONE scope is SPY0204 at every position, at the later
    /// statement's keyword (the checker's "Type alias 'A' is already defined", as NameResolver's at module/type body);
    /// a def after a same-block alias is SPY0204 at the def's name token ("function 'A' is already defined in this
    /// scope" — the validator's nested-def rule widened to alias names); def→alias and variable→alias are the
    /// checker's existing SPY0204 (d3/d9 — controls). A nested block may shadow (B2) and sibling blocks are fresh
    /// bindings: [] and the program runs with the pinned stdout. <c>module-try</c> adds its block literal SPY0340 at
    /// the <c>try</c> and runs nothing. The generator's cells carry no use after a refusal (D8: cascades are the hand
    /// fixtures').</para>
    /// </summary>
    internal static IEnumerable<AliasRedeclarationCell> AliasRedeclarationCells()
    {
        foreach (var position in AliasPositions)
        {
            foreach (var relation in AliasRelations)
            {
                if (position.Name is "type-body" or "module" && relation.Name != "alias-then-alias")
                    continue;
                var body = relation.Lines.Select(l => new string(' ', position.Indent) + l);
                var lines = position.Before.Concat(body).Concat(position.After)
                    .Concat(new[] { "", "def main() -> None:", "    " + position.Main, "" }).ToArray();
                var expected = new List<(string, int, int)>();
                if (position.Block is { } block)
                    expected.Add((block.Code, block.Index + 1, block.Column));
                if (relation.Refused >= 0)
                    expected.Add(("SPY0204", position.Before.Length + relation.Refused + 1, position.Indent + relation.ColumnOffset));
                var stdout = expected.Count == 0 ? relation.Stdout : null;
                yield return new AliasRedeclarationCell(relation.Name, position.Name, string.Join("\n", lines), expected, stdout);
            }
        }
    }

    [Fact]
    public void AliasRedeclaration_HasOneVerdictPerRelation()
    {
        var allowlist = LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();
        var ran = 0;
        foreach (var cell in AliasRedeclarationCells())
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

            var shadow = result.Diagnostics.Where(d => !d.IsError && VerdictWarnings.Contains(d.Code ?? "")).ToList();
            if (shadow.Count > 0)
                failures.Add($"shadowing warnings [{Fingerprint(shadow)}], expected none");

            if (cell.ExpectedStdout is { } stdout)
            {
                ran++;
                var run = CompileAndExecute(cell.Source);
                if (InternalCodes(run).Count > 0)
                    failures.Add($"an internal error under run: {Fingerprint(InternalCodes(run))}");
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        Output.WriteLine($"{AliasRedeclarationCells().Count()} alias-redeclaration cells, {ran} executed, {red.Count} allowlisted red");
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
        var universe = new HashSet<string>(
            Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id))
                .Concat(ImportBindingCells().Select(c => c.Id)).Concat(AliasRedeclarationCells().Select(c => c.Id)),
            StringComparer.Ordinal);
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
