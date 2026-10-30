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
/// <c>type `int` = str</c> (the sanctioned route: LEGAL, expected []), each at <c>module</c> and <c>def-body</c>; and
/// the SELF-ACCESSOR cells — <c>property get</c>/<c>property set</c> spelling <c>self</c> and using it at every
/// non-declaring position but <c>module-try</c>, an <c>event add</c>/<c>remove</c> pair using <c>self</c> at every
/// position but the type-body ones — whose exact sets (one SPY0708 per accessor) make clause (2s) and the checker's
/// decline falsifiable.
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

    /// <summary>
    /// The self-accessor edge kinds (verify round, W3/W4): an accessor that spells <c>self</c> and whose body uses it,
    /// at a host that is not a type body. The matrix's function-hosted accessors take no <c>self</c>, so the checker's
    /// decline (<c>TypeChecker.CheckStatement</c>: no <c>CheckModuleProperty</c>/<c>CheckClassProperty</c>/<c>CheckEvent</c>
    /// at a non-declaring position, and no <c>CheckEvent</c> outside a type body) had no generator cell that a second
    /// owner's SPY0232 would redden: these cells are that guard. Property accessors are built at every non-declaring
    /// position but <c>module-try</c> (a module-level accessor with <c>self</c> is SPY0232's own verdict — the positive
    /// control); the event pair at every position but the type-body ones, <c>module</c> included.
    /// </summary>
    private static readonly KindSpec[] SelfAccessorKinds =
    {
        new("property-get-self", Family.Member, n => new[]
        {
            $"property get {n}(self) -> int:",
            "    print(self)",
            "    return 0",
        }),
        new("property-set-self", Family.Member, n => new[]
        {
            $"property set {n}(self, v: int):",
            "    print(self)",
        }),
        new("event-add-self", Family.Member, n => new[]
        {
            $"event add {n}(self, handler: Action[int]):",
            "    print(self)",
            $"event remove {n}(self, handler: Action[int]):",
            "    print(self)",
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

    /// <summary>The edge cells: host axis (4 hosts × 5 kind/name pairs), spelling axis (3 spellings × 2 positions) and
    /// the self-accessor cells (2 property accessors × 9 positions + the event pair × 11).</summary>
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

        foreach (var kind in SelfAccessorKinds)
            foreach (var position in Positions.Where(p => p.Name is not ("type-body" or "type-body-if")
                         && (kind.Kind == "event-add-self" || !DeclaringPositions.Contains(p.Name) && p.Name != "module-try")))
                yield return Build(kind, position, fresh, "Fresh", edge: true);
    }

    internal static IEnumerable<Cell> Cells() => MatrixCells().Concat(EdgeCells());

    private static Family FamilyOf(string kind)
        => Kinds.Concat(SpellingKinds).Concat(SelfAccessorKinds).Single(k => k.Kind == kind).Family;

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
                    return kind is "event-add" or "event-add-self"
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
        Assert.Equal(4 * 5 + 3 * 2 + 2 * 9 + 11, EdgeCells().Count());
        Assert.Equal(3, SelfAccessorKinds.Length);
        Assert.Equal(8 * 2 * 3, NestedDefCells().Count());
        // Import binding: 4 spellings × (fresh + repeated import + 10 colliders × 2 orders), + 2 `as` spellings ×
        // ({str, print, `str`} + `def print` × 2 orders + print twice), + 2 builtins-module names, + the plain builtin
        // type name, + the dotted root × 2 orders, + the star import × {var, const, def} × 2 orders, + the star import ×
        // {class, alias, property-field, property-get, enum} × 2 orders (P21g), + `from math import e as pi` × star × 2,
        // + the star import × 5 exported kinds × 3 read positions from a Stdlib source (P21i).
        Assert.Equal(10, ImportColliders.Length);
        Assert.Equal(5, StarDeclarations.Length);
        Assert.Equal(5, StarKinds.Length);
        Assert.Equal(3, StarReadPositions.Length);
        Assert.Equal(4 * (2 + 10 * 2) + 2 * (3 + 2 + 1) + 2 + 1 + 2 + 3 * 2 + 5 * 2 + 2 + 5 * 3, ImportBindingCells().Count());
        Assert.Equal(4 * 2 * 2 + 2, ImportBindingCells().Count(c => c.Collider is "same-module-other-symbol" or "star"));
        // Alias redeclaration: 7 relations × {def-body, if-in-def, method-body, module-try} + {alias→alias, alias→def,
        // def→alias} × type-body + alias→alias × module.
        Assert.Equal(7 * 4 + 3 + 1, AliasRedeclarationCells().Count());
        // Member pairs (P21g; P21h D9): 13 type-body kinds in a class or struct, 15 in an interface (its two stub kinds),
        // × 3 hosts; 8 module kinds.
        Assert.Equal(13, TypeBodyMemberKinds.Length);
        Assert.Equal(15, KindsFor("interface").Length);
        Assert.Equal(3, InterfaceKindOverrides.Count);
        Assert.Equal(8, ModuleMemberKinds.Length);
        Assert.Equal(3, TypeBodyHosts.Length);
        // Per type-body host: K alone + K × K ordered pairs + the same-signature twin + type-param × K (K = 13, 13, 15);
        // the two-defaults pair × 2 orders × {class, struct}; the @static-disagreement pairs (2 setters × 2 orders) ×
        // 3 hosts; module: 8 alone + 8 × 8 + the same-signature def twin;
        // enum: alone + member twice; union: method alone, overload, same-signature twin, case × method, case × case
        // (adjacent and after a def), type-param × case; chains: 3 hosts × (5 heads × 3 split pairs + 3 third-half);
        // explicit members: {class, struct} × (13 kinds × 2 orders + explicit twice + two interfaces + split halves).
        Assert.Equal(2 * (13 + 13 * 13 + 1 + 13) + (15 + 15 * 15 + 1 + 15) + 2 * 2 + 3 * 2 * 2 + (8 + 8 * 8 + 1) + 2 + 4 + 3
            + 3 * (5 * 3 + 3) + 2 * (13 * 2 + 3), MemberPairCells().Count());
        Assert.Equal(2 * (13 * 2 + 3), MemberPairCells().Count(c => c.Id.StartsWith("explicit-member × ", StringComparison.Ordinal)));
        // R-JH: an explicit member claims no bare name — every explicit cell runs but the same-key twin per host.
        Assert.Equal(2 * (13 * 2 + 2), ExplicitMemberCells().Count(c => c.ExpectedErrors.Count == 0 && c.ExpectedStdout != null));
        Assert.Equal(2 * 13 + 15 + 8 + 1 + 1, MemberPairCells().Count(c => c.Id.StartsWith("member-alone × ", StringComparison.Ordinal)));
        Assert.Equal(2 * 13 + 15 + 1, MemberPairCells().Count(c => c.Id.Contains(" × type-param → ", StringComparison.Ordinal)));
        Assert.Equal(3 * (5 * 3 + 3), MemberPairCells().Count(c => c.Id.StartsWith("member-chain × ", StringComparison.Ordinal)));
        // Legal (expected [], run): a class or struct host 8 property merges (6 complementary, 2 same-slot auto +
        // function-style) × 2 orders + the event add/remove pair × 2 + the overload; an interface 11 property merges
        // (7 complementary, 4 same-slot) × 2 + the event pair × 2 + the overload; module 3 property merges × 2 + the
        // def overload + var × var.
        Assert.Equal(2 * (8 * 2 + 2 + 1) + (11 * 2 + 2 + 1) + (3 * 2 + 1 + 1), MemberPairCells().Count(c =>
            c.Id.StartsWith("member-pair × ", StringComparison.Ordinal)
            && c.Host is "class" or "struct" or "interface" or "module" && c.ExpectedErrors.Count == 0));
        // R-JG: the interface abstract × bodied refusals — 6 pairs × 2 orders, plus the 2 interface third-half chains
        // whose second half is bodied; the two-defaults SPY0407 cells.
        Assert.Equal(6 * 2, MemberPairCells().Count(c => c.Id.StartsWith("member-pair × interface × ", StringComparison.Ordinal)
            && c.ExpectedErrors.Any(e => e.Code == "SPY0617")));
        Assert.Equal(2, MemberChainCells().Count(c => c.ExpectedErrors.Any(e => e.Code == "SPY0617")));
        Assert.Equal(4, MemberPairCells().Count(c => c.ExpectedErrors.Any(e => e.Code == "SPY0407")));
        // The halves of one property agree on @static: the static auto getter × {auto setter, function-style setter} ×
        // 2 orders × {class, struct, interface}.
        Assert.Equal(3 * 2 * 2, MemberPairCells().Count(c => c.Id.Contains("property-auto-get-static", StringComparison.Ordinal)
            && c.ExpectedErrors.Count == 1 && c.ExpectedErrors[0].Code == "SPY0204"));
        // The lone event accessor beside another kind carries its SPY0420 (D9 (i)): per type-body host, each of the 2
        // accessors beside the K − 1 kinds that are not its complement, in both orders, its same-kind twin once
        // (2K − 3 cells), + type-param × 2 accessors.
        Assert.Equal(2 * (2 * (2 * 13 - 3) + 2) + (2 * (2 * 15 - 3) + 2),
            MemberPairCells().Count(c => c.ExpectedErrors.Any(e => e.Code == "SPY0420")));
        var ids = Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id))
            .Concat(ImportBindingCells().Select(c => c.Id)).Concat(AliasRedeclarationCells().Select(c => c.Id))
            .Concat(MemberPairCells().Select(c => c.Id)).ToList();
        Assert.Equal(18 * 13 * 3 + 26 + 29 + 48 + 138 + 32 + 858, ids.Distinct(StringComparer.Ordinal).Count());
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
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedWarnings, string? ExpectedStdout,
        string? Position = null, string? ModuleSource = null)
    {
        /// <summary>The star × kind cells (P21i) carry a read POSITION and the exporting module's SOURCE after the kind:
        /// <c>import-binding × from-import-star × &lt;kind&gt; × &lt;position&gt; × &lt;source&gt;</c>.</summary>
        public string Id => Position == null
            ? $"import-binding × {Spelling} × {Collider} × {Order}"
            : $"import-binding × {Spelling} × {Collider} × {Position} × {ModuleSource}";
    }

    /// <summary>The four import spellings: (id, the import line for a bound name, the name a plain spelling binds, the
    /// spelling's line beside the <c>star</c> collider — an <c>as</c> spelling binds the star-exported <c>pi</c> to
    /// ANOTHER source (<c>copy</c>), a plain spelling keeps its own line).</summary>
    private static readonly (string Name, Func<string, string> Line, string? FixedName, string StarLine)[] ImportSpellings =
    {
        ("import-as", n => $"import math as {n}", null, "import copy as pi"),
        ("import", _ => "import math", "math", "import math"),
        ("from-import-as", n => $"from math import pi as {n}", null, "from copy import deepcopy as pi"),
        ("from-import", _ => "from math import pi", "pi", "from math import pi"),
    };

    /// <summary>The colliders spelled beside the import, in both source orders: a module-level declaration of every
    /// kind, a module variable, another module's import under the same name in both import spellings, the SAME module's
    /// other symbol under the same name (K1), and the star import of the module (R-IZ; its line is the spelling's
    /// <c>StarLine</c>).</summary>
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
        ("same-module-other-symbol", n => new[] { $"from math import e as {n}" }),
        ("star", _ => new[] { "from math import *" }),
    };

    /// <summary>The declaration kinds beside a star import (#2371) beyond the variable, const and def colliders: each
    /// named <c>pi</c> (a <c>math</c> export) and read through a <c>main</c> that prints the LOCAL's value, which the
    /// star's <c>math.pi</c> would not print — (kind, lines, main's body, stdout).</summary>
    private static readonly (string Name, string[] Lines, string Body, string Stdout)[] StarDeclarations =
    {
        ("class", new[] { "class pi:", "    tag: int = 1" }, "print(pi().tag)", "1\n"),
        ("alias", new[] { "type pi = int" }, "x: pi = 7\nprint(x)", "7\n"),
        ("property-field", new[] { "property pi: int = 1" }, "print(pi)", "1\n"),
        ("property-get", new[] { "property get pi() -> int:", "    return 1" }, "print(pi)", "1\n"),
        ("enum", new[] { "enum pi:", "    A = 1" }, "print(pi.A)", "pi.A\n"),
    };

    /// <summary>The star × exported-kind × read-position cells' Stdlib source (P21i, #2385): (kind, the star line, a
    /// module-level prelude — the taken-over variable's own declaration —, the read expression, its type, python3's
    /// stdout). The kinds a Stdlib module exports: <c>math.pi</c> (variable), <c>math.sqrt</c> (def), <c>os.environ</c>
    /// (a function-style property, a static field to the CLR extraction — its oracle is the explicit
    /// <c>from os import environ</c> spelling, which prints <c>True</c> for the same probe at the base), <c>struct.Struct</c>
    /// (an exported class that constructs; <c>struct.Struct("i").size</c> is <c>4</c> in python3) and <c>pi</c> taken
    /// over by the file's own <c>pi: float = 1.5</c> (M1). The project source's twin is
    /// <c>ImportBindingProjectRouteTests.StarKindCells</c>.</summary>
    private static readonly (string Kind, string Star, string[] Prelude, string Read, string Type, string Stdout)[] StarKinds =
    {
        ("var", "from math import *", Array.Empty<string>(), "pi", "float", "3.141592653589793\n"),
        ("def", "from math import *", Array.Empty<string>(), "sqrt(4.0)", "float", "2.0\n"),
        ("property", "from os import *", Array.Empty<string>(), "len(environ) > 0", "bool", "True\n"),
        ("class", "from struct import *", Array.Empty<string>(), "Struct(\"i\").size", "int", "4\n"),
        ("var-taken-over", "from math import *", new[] { "pi: float = 1.5" }, "pi", "float", "1.5\n"),
    };

    /// <summary>The three read positions of a star-bound name (P21i): the module body (<c>x: T = name</c>, printed from
    /// <c>main</c>), a def body (<c>main</c> itself) and a method body (<c>class R: def read(self)</c>, printed through
    /// <c>R().read()</c>). Every read lands in an ANNOTATED destination (<c>x: T = read</c>), so the cell is a type probe
    /// as well as a value probe. (position, the program given the star line, the prelude lines, the read and its type.)</summary>
    internal static readonly (string Name, Func<string, string[], string, string, string> Program)[] StarReadPositions =
    {
        ("module", (star, prelude, read, type) => string.Join("\n", new[] { star }.Concat(prelude)
            .Concat(new[] { $"x: {type} = {read}", "", "def main() -> None:", "    print(x)", "" }))),
        ("def", (star, prelude, read, type) => string.Join("\n", new[] { star }.Concat(prelude)
            .Concat(new[] { "", "def main() -> None:", $"    x: {type} = {read}", "    print(x)", "" }))),
        ("method", (star, prelude, read, type) => string.Join("\n", new[] { star }.Concat(prelude)
            .Concat(new[] { "class R:", $"    def read(self) -> {type}:", $"        x: {type} = {read}", "        return x", "",
                "def main() -> None:", "    print(R().read())", "" }))),
    };

    /// <summary>
    /// Import spelling {<c>import math as N</c>, <c>import math</c> (N = <c>math</c>), <c>from math import pi as N</c>,
    /// <c>from math import pi</c> (N = <c>pi</c>)} × collider {fresh, <c>class N</c>, <c>def N</c>, <c>type N = int</c>,
    /// <c>const N</c>, <c>property N</c>, <c>N: int = 1</c>, the same import again, <c>import copy as N</c>,
    /// <c>from copy import deepcopy as N</c>} × order {collider first, import first} (fresh and the repeated import have
    /// one order), plus — for the <c>as</c> spellings — the bound name {<c>str</c>, <c>print</c>, <c>`str`</c>}, the
    /// <c>builtins</c> module's own bindings <c>from builtins import len</c> / <c>str</c>, and the dotted root
    /// <c>import os</c> + <c>import os.path</c> in both orders; for the <c>as</c> spellings also <c>print</c> beside a
    /// <c>def print</c> (both orders) and <c>print</c> imported twice; the star spelling <c>from math import *</c> ×
    /// {module variable, const, def, class, alias, field-style property, getter property, enum} named <c>pi</c>, both
    /// orders; and <c>from math import e as pi</c> beside the star (r25), both orders. P21g (#2359, R-IZ) added the
    /// <c>same-module-other-symbol</c> collider (<c>from math import e as N</c>: K1) and the <c>star</c> collider (the
    /// spelling's <c>StarLine</c> beside <c>from math import *</c>: an <c>as</c> spelling binds <c>pi</c> to
    /// <c>copy</c>, a plain spelling keeps its line — <c>import math</c> (u10) and the idempotent
    /// <c>from math import pi</c> (r23)). P21i (#2385) adds the star × KIND × read-POSITION cells from a Stdlib source
    /// (<see cref="StarKinds"/> × <see cref="StarReadPositions"/>: variable, def, property, class and the taken-over
    /// variable, read from the module body, a def and a method — no collider, python3's value; the project source's
    /// twin is <c>ImportBindingProjectRouteTests</c>). Every program's <c>main</c> prints <c>ok</c> (nothing else uses the binding,
    /// D8), except the <c>print</c> cells (whose <c>main</c> is <c>pass</c>: the rebinding is honoured), the dotted root
    /// (which prints <c>len(os.path.join("a", "b"))</c>), the idempotent star cell (<c>print(pi)</c>) and the star ×
    /// declaration cells (the local's value).
    ///
    /// <para><b>Expected</b> (D4/D5). The pipeline declares the module body BEFORE it binds imports, so an import is
    /// always the later binding against a declaration, whatever the source order: class / def / alias / const /
    /// property → [SPY0204 @ the import's ImportAlias] in BOTH orders. A module variable is declared by the checker
    /// AFTER the imports, so it is the later binding in both orders: [SPY0204 @ the variable] (c18, pinned). Two imports
    /// bind in source order: [SPY0204 @ the later one's ImportAlias] (c15, e5, g2; d11 is the from/from cell) — and so
    /// do two imports of ONE module that bind DIFFERENT symbols (K1: <c>same-module-other-symbol</c>, k1a/k1b). An
    /// explicit import of another source that spells a star-exported name is refused at the EXPLICIT import's
    /// ImportAlias in BOTH orders (R-IZ: k3a/k3b/k3c; r25 for <c>from math import e as pi</c>); a plain
    /// <c>import math</c> beside its star binds no export (u10) and a plain <c>from math import pi</c> beside it names
    /// the same symbol (r23, idempotent): [] and run. A
    /// builtin type name under an <c>as</c> spelling → [SPY0212 @ ImportAlias] (the escaped <c>`str`</c> is the opt-in: []),
    /// under a PLAIN from-import (<c>from System.Collections.Generic import IList</c> — the exporter's name) → errors [],
    /// warnings [SPY0484 @ ImportAlias], and it runs; a builtin function name
    /// → errors [], warnings [SPY0484 @ ImportAlias] (c4/c9); the <c>builtins</c> module's own binding shadows nothing
    /// → [] with no SPY0484 (d4/d5) — the absence's positive control is the <c>from-import-as × print</c> cell's
    /// SPY0484, asserted unconditionally (hard). Fresh, the repeated import (idempotent) and the dotted root (merge) →
    /// [] and run. A refused import draws its SPY0204 alone: beside <c>def print</c> the warnings are the def's
    /// SPY0483 only, never a SPY0484 claiming the import took effect; <c>print</c> imported twice warns once (the second
    /// is idempotent). A star import never displaces a declaration of the importing file, whatever its kind (#2371):
    /// the star × declaration cells are [] and print the LOCAL's value (<c>1</c>, <c>7</c>, <c>pi.A</c>), not
    /// <c>math.pi</c>. Warnings are compared over <see cref="VerdictWarnings"/>; errors over the whole program.</para>
    /// </summary>
    internal static IEnumerable<ImportBindingCell> ImportBindingCells()
    {
        const string bound = "mm";
        string[] Main(string body) => new[] { "", "def main() -> None:" }
            .Concat(body.Split('\n').Select(l => "    " + l)).Append("").ToArray();

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
                // Beside the star, the spelling's line is its StarLine (an `as` spelling binds `pi` to `copy`).
                var spelled = collider.Name == "star" ? spelling.StarLine : import;
                foreach (var order in new[] { "collider-first", "import-first" })
                {
                    var colliderFirst = order == "collider-first";
                    var first = colliderFirst ? colliderLines : new[] { spelled };
                    var second = colliderFirst ? new[] { spelled } : colliderLines;
                    var body = "print(\"ok\")";
                    var stdout = "ok\n";
                    Func<int, int, (string, int, int)[]> errors = collider.Name switch
                    {
                        // The variable is the later binding in both orders; SPY0204 at its name (column 1).
                        "var" => (f, s) => new[] { ("SPY0204", colliderFirst ? f : s, 1) },
                        // Two imports bind in source order: the later one is refused — another module's, or the same
                        // module's other symbol (K1: identity is what the import NAMES).
                        "other-import" or "other-from-import" or "same-module-other-symbol" => (f, s) => new[]
                        {
                            ("SPY0204", s, ImportAliasColumn(second[0])),
                        },
                        // R-IZ: an explicit import of another source that spells a star-exported name is refused at
                        // the EXPLICIT import, in both orders. A plain spelling binds no other export (`import math`,
                        // u10) or the same symbol (`from math import pi`, r23): legal, and it runs.
                        "star" when spelling.FixedName == null => (f, s) => new[]
                        {
                            ("SPY0204", colliderFirst ? s : f, ImportAliasColumn(spelled)),
                        },
                        "star" => (_, _) => none,
                        // A declaration is bound first whatever the order: the import is refused.
                        _ => (f, s) => new[] { ("SPY0204", colliderFirst ? s : f, aliasColumn) },
                    };
                    if (collider.Name == "star" && spelling.FixedName == "pi")
                        (body, stdout) = ("print(pi)", "3.141592653589793\n");
                    yield return Make(spelling.Name, collider.Name, order, first, second, errors, body: body, stdout: stdout);
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

            // A refused import draws its SPY0204 alone — no SPY0484 claiming it rebinds the name (R3 of the P21f
            // verify round); the def's own SPY0483 at its name is the only shadowing warning, in both orders.
            var printDef = new[] { "def print(x: int) -> None:", "    pass" };
            foreach (var order in new[] { "collider-first", "import-first" })
            {
                var colliderFirst = order == "collider-first";
                yield return Make(spelling.Name, "print-def", order,
                    colliderFirst ? printDef : new[] { print }, colliderFirst ? new[] { print } : printDef,
                    (f, s) => new[] { ("SPY0204", colliderFirst ? s : f, ImportAliasColumn(print)) },
                    (f, s) => new[] { ("SPY0483", colliderFirst ? f : s, 5) }, body: "pass", stdout: "");
            }

            // The same builtin-function-name import twice binds once and warns once: the second is idempotent.
            yield return Make(spelling.Name, "print", "twice", new[] { print }, new[] { print },
                (_, _) => none, (f, _) => new[] { ("SPY0484", f, ImportAliasColumn(print)) }, body: "pass", stdout: "");
        }

        // A star import keeps its rule (outside R-II's four spellings): a module-level declaration of a star-imported
        // name takes it over, in either order — a module variable re-declares the star-imported default like a const
        // or a def does (R1 of the P21f verify round: the module-variable cell ran `1` at the base and must again), and
        // so does every other declaration kind: class, alias, both property forms, enum (#2371).
        var star = new[] { "from math import *" };
        foreach (var collider in ImportColliders.Where(c => c.Name is "var" or "const" or "def"))
        {
            foreach (var order in new[] { "collider-first", "import-first" })
            {
                var colliderLines = collider.Lines("pi");
                yield return Make("from-import-star", collider.Name, order,
                    order == "collider-first" ? colliderLines : star, order == "collider-first" ? star : colliderLines,
                    (_, _) => Array.Empty<(string, int, int)>(),
                    body: collider.Name == "def" ? "print(pi())" : "print(pi)", stdout: "1\n");
            }
        }

        foreach (var declaration in StarDeclarations)
        {
            foreach (var order in new[] { "collider-first", "import-first" })
            {
                yield return Make("from-import-star", declaration.Name, order,
                    order == "collider-first" ? declaration.Lines : star, order == "collider-first" ? star : declaration.Lines,
                    (_, _) => Array.Empty<(string, int, int)>(), body: declaration.Body, stdout: declaration.Stdout);
            }
        }

        // P21i (#2385): a star-bound name of every exported kind, read at every position, from a Stdlib source — no
        // diagnostic, and python3's value (the taken-over variable prints the file's own 1.5 at every position: M1). The
        // star arm above reads only in `main` beside a collider; these cells are the kind × position axis.
        foreach (var kind in StarKinds)
        {
            foreach (var position in StarReadPositions)
            {
                yield return new ImportBindingCell("from-import-star", kind.Kind, "none",
                    position.Program(kind.Star, kind.Prelude, kind.Read, kind.Type),
                    Array.Empty<(string, int, int)>(), Array.Empty<(string, int, int)>(), kind.Stdout,
                    Position: position.Name, ModuleSource: "stdlib");
            }
        }

        // r25: the same module's OTHER symbol under a star-exported name is an explicit import of another source (K1's
        // identity under R-IZ) — refused at the explicit import in both orders.
        const string otherSymbol = "from math import e as pi";
        foreach (var order in new[] { "collider-first", "import-first" })
        {
            var starFirst = order == "collider-first";
            yield return Make("from-math-import-e-as-pi", "star", order,
                starFirst ? star : new[] { otherSymbol }, starFirst ? new[] { otherSymbol } : star,
                (f, s) => new[] { ("SPY0204", starFirst ? s : f, ImportAliasColumn(otherSymbol)) });
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
    /// alias→alias at the declaring positions {type body, module} (their controls, a3/a4) and alias→def / def→alias in
    /// a type body (members are walked in source order there: the later of a method and a same-named alias is refused —
    /// the method at its name, the alias at its keyword). A legal relation prints
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
                // A type body walks its members in source order, so both alias↔def orders are spellable there (the
                // later one is refused: the method at its name, the alias at its keyword); a module hoists its defs.
                if (position.Name == "module" && relation.Name != "alias-then-alias")
                    continue;
                if (position.Name == "type-body" && relation.Name is not ("alias-then-alias" or "alias-then-def" or "def-then-alias"))
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
    // The member-pair sub-matrix (P21g D9 (i), R-JA, R-JB, #2360).
    // ---------------------------------------------------------------------------------------------------------------

    internal sealed record MemberPairCell(
        string Id, string Host, string Source,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? ExpectedStdout);

    /// <summary>A member kind of the pair matrix: its lines declaring <c>A</c> (unindented; a type-body accessor
    /// spells <c>self</c>, a module-level one does not), where its verdict lands (D2's position rule — the
    /// declaration's first token, or the name token for a method), the statements of <c>main</c> that use it ALONE and
    /// what they print, and — for the interface host — the members the implementing class needs.</summary>
    internal sealed record MemberKind(
        string Kind, string[] Lines, At Where, string[] Use, string Stdout, string[]? ImplLines = null);

    /// <summary>The thirteen type-body member kinds (class, struct and interface hosts): P21g's nine plus the split auto
    /// halves <c>property-auto-get</c> / <c>property-auto-set</c> and the event accessors <c>event-add</c> /
    /// <c>event-remove</c> (P21h D9 (i)). Every kind's alone use prints a value of its own (1–9), so a dropped or
    /// displaced member is a different stdout or an error — except <c>property-auto-set</c>, which nothing can read, so
    /// its use writes and then prints <c>ok</c>. A lone event accessor is SPY0420, so an accessor's alone control is
    /// the add/remove pair (<see cref="AloneMembers"/>).</summary>
    private static readonly MemberKind[] TypeBodyMemberKinds =
    {
        new("nested-class", new[] { "class A:", "    tag: int = 1" }, At.Keyword, new[] { "print(Host.A().tag)" }, "1"),
        new("method", new[] { "def A(self) -> int:", "    return 2" }, At.Name, new[] { "print(h.A())" }, "2"),
        new("field", new[] { "A: int = 3" }, At.Name, new[] { "print(h.A)" }, "3"),
        new("const", new[] { "const A: int = 4" }, At.Keyword, new[] { "print(Host.A)" }, "4"),
        new("property-field", new[] { "property A: int = 5" }, At.Keyword, new[] { "print(h.A)" }, "5"),
        new("property-get", new[] { "property get A(self) -> int:", "    return 6" }, At.Keyword, new[] { "print(h.A)" }, "6"),
        // The setter carries no `-> None` (the spec's spelling; see the matrix's property-set kind).
        new("property-set", new[] { "property set A(self, v: int):", "    print(v)" }, At.Keyword, new[] { "h.A = 7" }, "7"),
        new("property-auto-get", new[] { "property get A: int = 5" }, At.Keyword, new[] { "print(h.A)" }, "5"),
        new("property-auto-set", new[] { "property set A: int" }, At.Keyword, new[] { "h.A = 7", "print(\"ok\")" }, "ok"),
        new("event-auto", new[] { "event A: Action[int]" }, At.Keyword, new[] { "h.A += show", "h.A -= show", "print(8)" }, "8",
            ImplLines: new[] { "event A: Action[int]" }),
        new("event-add", new[] { "event add A(self, h: Action[int]):", "    pass" }, At.Keyword,
            new[] { "h.A += show", "h.A -= show", "print(8)" }, "8"),
        new("event-remove", new[] { "event remove A(self, h: Action[int]):", "    pass" }, At.Keyword,
            new[] { "h.A += show", "h.A -= show", "print(8)" }, "8"),
        new("alias", new[] { "type A = str" }, At.Keyword, new[] { "x: Host.A = \"9\"", "print(x)" }, "9"),
    };

    /// <summary>The interface host's spellings (D9 (i)): its auto halves carry NO default — a defaulted interface auto
    /// half is #2383's cell (R-JL), which no cell of this matrix may name — so each is ABSTRACT and <c>Impl</c>'s own
    /// <c>property A: int = 5</c> implements it; the kind ids are the class host's (the id names the declaration form,
    /// the host its spelling).</summary>
    private static readonly Dictionary<string, MemberKind> InterfaceKindOverrides = new(StringComparer.Ordinal)
    {
        ["property-field"] = new("property-field", new[] { "property A: int" }, At.Keyword, new[] { "print(h.A)" }, "5",
            ImplLines: new[] { "property A: int = 5" }),
        ["property-auto-get"] = new("property-auto-get", new[] { "property get A: int" }, At.Keyword, new[] { "print(h.A)" }, "5",
            ImplLines: new[] { "property A: int = 5" }),
        ["property-auto-set"] = new("property-auto-set", new[] { "property set A: int" }, At.Keyword,
            new[] { "h.A = 7", "print(\"ok\")" }, "ok", ImplLines: new[] { "property A: int = 5" }),
    };

    /// <summary>The two interface-only kinds: a function-style accessor STUB (<c>...</c>, abstract), implemented by
    /// <c>Impl</c>'s auto property.</summary>
    private static readonly MemberKind[] InterfaceOnlyMemberKinds =
    {
        new("property-get-stub", new[] { "property get A(self) -> int: ..." }, At.Keyword, new[] { "print(h.A)" }, "5",
            ImplLines: new[] { "property A: int = 5" }),
        new("property-set-stub", new[] { "property set A(self, v: int): ..." }, At.Keyword,
            new[] { "h.A = 7", "print(\"ok\")" }, "ok", ImplLines: new[] { "property A: int = 5" }),
    };

    /// <summary>The member kinds of a type-body host: thirteen in a class or struct, fifteen in an interface (its
    /// undefaulted auto spellings and the two stub kinds).</summary>
    private static MemberKind[] KindsFor(string host) => host == "interface"
        ? TypeBodyMemberKinds.Select(k => InterfaceKindOverrides.TryGetValue(k.Kind, out var o) ? o : k).Concat(InterfaceOnlyMemberKinds).ToArray()
        : TypeBodyMemberKinds;

    /// <summary>The members of a kind's alone control: the kind, and for an event accessor its complementary half (a
    /// lone accessor is SPY0420 — P21f, measured).</summary>
    private static string[][] AloneMembers(MemberKind kind) => kind.Kind switch
    {
        "event-add" => new[] { kind.Lines, TypeBodyMemberKinds.Single(k => k.Kind == "event-remove").Lines },
        "event-remove" => new[] { kind.Lines, TypeBodyMemberKinds.Single(k => k.Kind == "event-add").Lines },
        _ => new[] { kind.Lines },
    };

    /// <summary>The two-defaults cell's second half (D3, owner ruling 2026-10-28 (a)): an auto SETTER carrying a
    /// default, spelled only beside <c>property-auto-get</c> (<c>= 5</c>) — never multiplied against the matrix.</summary>
    private static readonly MemberKind AutoSetWithDefault =
        new("property-auto-set-def", new[] { "property set A: int = 6" }, At.Keyword, new[] { "h.A = 7", "print(\"ok\")" }, "ok");

    /// <summary>The @static-disagreement cells' static half (P21h, lead decision on the verifier's cell): a
    /// <c>@static</c> auto getter, spelled only beside the instance setter halves — never multiplied against the
    /// matrix. Its keyword is the line after the decorator. The interface host spells its auto halves without a
    /// default (D9; a defaulted interface auto half is #2383's).</summary>
    private static MemberKind StaticAutoGet(string host) =>
        new("property-auto-get-static", new[] { "@static", host == "interface" ? "property get A: int" : "property get A: int = 5" },
            At.Keyword, new[] { "print(Host.A)" }, "5");

    /// <summary>The function-style accessors that carry a BODY (D2's <c>Bodied</c>) and the halves an interface
    /// leaves ABSTRACT (an undefaulted auto half, a stub).</summary>
    private static readonly HashSet<string> BodiedHalves = new(StringComparer.Ordinal) { "property-get", "property-set" };

    private static readonly HashSet<string> AbstractInterfaceHalves = new(StringComparer.Ordinal)
    {
        "property-field", "property-auto-get", "property-auto-set", "property-get-stub", "property-set-stub",
    };

    /// <summary>The eight module-level declaration kinds. A module <c>def</c>'s verdict lands at its keyword (the
    /// existing "Function 'A' is already defined with the same signature", measured — the only cell a def is the later
    /// binding in); a module variable's at its name, column 1.</summary>
    private static readonly MemberKind[] ModuleMemberKinds =
    {
        new("class", new[] { "class A:", "    tag: int = 1" }, At.Keyword, new[] { "print(A().tag)" }, "1"),
        new("def", new[] { "def A() -> int:", "    return 2" }, At.Keyword, new[] { "print(A())" }, "2"),
        new("alias", new[] { "type A = str" }, At.Keyword, new[] { "x: A = \"9\"", "print(x)" }, "9"),
        new("const", new[] { "const A: int = 4" }, At.Keyword, new[] { "print(A)" }, "4"),
        new("var", new[] { "A: int = 3" }, At.Name, new[] { "print(A)" }, "3"),
        new("property-field", new[] { "property A: int = 5" }, At.Keyword, new[] { "print(A)" }, "5"),
        new("property-get", new[] { "property get A() -> int:", "    return 6" }, At.Keyword, new[] { "print(A)" }, "6"),
        new("property-set", new[] { "property set A(v: int):", "    print(v)" }, At.Keyword, new[] { "A = 7" }, "7"),
    };

    /// <summary>The type-body hosts of the pair matrix (every one generic-capable, measured: <c>class Host[A]</c>,
    /// <c>struct Host[A]</c> and <c>interface Host[A]</c> each compile and run).</summary>
    private static readonly string[] TypeBodyHosts = { "class", "struct", "interface" };

    /// <summary>The second method of a same-kind method pair: a DIFFERENT signature (an overload — legal), or the same
    /// signature (a separate relation, <c>method-same-sig</c>).</summary>
    private static string[] SecondMethod(bool module, bool sameSignature) => (module, sameSignature) switch
    {
        (false, false) => new[] { "def A(self, x: int) -> int:", "    return x" },
        (false, true) => new[] { "def A(self) -> int:", "    return 3" },
        (true, false) => new[] { "def A(x: int) -> int:", "    return x" },
        (true, true) => new[] { "def A() -> int:", "    return 3" },
    };

    /// <summary>
    /// The LEGAL pairs and what reading through both halves prints (<paramref name="receiver"/> is <c>h.</c> in a type
    /// body, empty at module level): a function-style getter + setter (<c>properties_function_style.md</c> "both a
    /// function-style getter and setter"; module level "Split accessors are merged … following the same rules as
    /// class-level split accessors"); an AUTO half + a function-style getter or setter (<c>:88</c>, "A function-style
    /// accessor may also be paired with an auto-property of the same name supplying the complementary half" — D2 (b));
    /// a method or def overload (different signatures). Each in both orders. Every other pair is refused.
    ///
    /// <para>Host-aware (P21h D9 (i)). In a class or struct the split auto halves merge too: <c>auto-get + auto-set</c>
    /// (<c>properties_function_style.md:200</c>), an auto getter + a function-style setter and an auto setter + a
    /// function-style getter; and an <c>event add</c> + <c>event remove</c> pair is one event. In an interface the
    /// merges with an abstract half (an undefaulted auto half or a stub) beside another abstract half are implemented
    /// by <c>Impl</c>'s own auto property, which takes the write and reads it back (<c>7</c>); the function-style pair
    /// with bodies is two default accessors <c>Impl</c> does not override (<c>7\n6</c>); an abstract half beside a
    /// bodied one is REFUSED (<see cref="InterfaceBodyMismatch"/>, R-JG), so it is not legal here. An auto half
    /// beside a function-style accessor of its OWN slot is not a second part (<c>:94</c>, <c>:370</c>, and the seam's
    /// <c>AccessorDeclarations.Admits</c>): the accessor supplies that half's body, in every host.</para>
    /// </summary>
    private static (string[] Use, string Stdout)? LegalPair(string a, string b, string host)
    {
        var receiver = host == "module" ? "" : "h.";
        var pair = string.Join("+", new[] { a, b }.OrderBy(k => k, StringComparer.Ordinal));
        var write = new[] { $"{receiver}A = 7", $"print({receiver}A)" };
        var events = new[] { "h.A += show", "h.A -= show", "print(8)" };
        var read = new[] { "print(h.A)" };
        var writeOnly = new[] { "h.A = 7", "print(\"ok\")" };
        if (host == "interface")
        {
            return pair switch
            {
                "property-get+property-set" => (write, "7\n6\n"),
                "property-auto-get+property-auto-set"
                    or "property-field+property-get-stub" or "property-field+property-set-stub"
                    or "property-auto-get+property-set-stub" or "property-auto-set+property-get-stub"
                    or "property-get-stub+property-set-stub" => (write, "7\n"),
                // An auto half beside a function-style accessor of its OWN slot (:370): one accessor, which Impl's
                // auto property implements — a get-only property reads Impl's 5, a set-only one takes the write.
                "property-auto-get+property-get" or "property-auto-get+property-get-stub" => (read, "5\n"),
                "property-auto-set+property-set" or "property-auto-set+property-set-stub" => (writeOnly, "ok\n"),
                "event-add+event-remove" => (events, "8\n"),
                "method+method" => (new[] { "print(h.A())", "print(h.A(5))" }, "2\n5\n"),
                _ => null,
            };
        }

        return pair switch
        {
            // The setter prints the written 7; the getter returns its own 6.
            "property-get+property-set" => (write, "7\n6\n"),
            // The auto half's setter takes the write; the function-style getter's 6 is what reads.
            "property-field+property-get" or "property-auto-set+property-get" => (write, "6\n"),
            // The function-style setter prints 7 and stores nothing; the auto half's getter reads its default 5.
            "property-field+property-set" or "property-auto-get+property-set" => (write, "7\n5\n"),
            // One auto property: the write is what reads.
            "property-auto-get+property-auto-set" => (write, "7\n"),
            // An auto half beside a function-style accessor of its OWN slot is not a second part (:94, :370): the
            // function-style accessor supplies that half's body — a get-only property reading 6, a set-only one
            // printing the written 7.
            "property-auto-get+property-get" => (read, "6\n"),
            "property-auto-set+property-set" => (new[] { "h.A = 7" }, "7\n"),
            "event-add+event-remove" when host != "module" => (events, "8\n"),
            "method+method" or "def+def" => (new[] { $"print({receiver}A())", $"print({receiver}A(5))" }, "2\n5\n"),
            _ => null,
        };
    }

    /// <summary>R-JG (D2): in an INTERFACE an abstract half (an undefaulted auto half, a stub) beside a function-style
    /// accessor with a body that fills the complementary slot has no C# shape (CS0525, both orders — measured) and is
    /// refused at the later half: <c>[SPY0617 @ its keyword]</c>. Twelve cells: six pairs × two orders.</summary>
    private static bool InterfaceBodyMismatch(string a, string b, string host)
    {
        if (host != "interface")
            return false;
        var (abstractHalf, bodied) = BodiedHalves.Contains(b) ? (a, b) : (b, a);
        if (!BodiedHalves.Contains(bodied) || !AbstractInterfaceHalves.Contains(abstractHalf))
            return false;

        // Complementary slots: an auto-both half complements either accessor; a one-slot half the other one.
        return abstractHalf == "property-field"
            || (bodied == "property-get" && abstractHalf is "property-auto-set" or "property-set-stub")
            || (bodied == "property-set" && abstractHalf is "property-auto-get" or "property-get-stub");
    }

    /// <summary>The lone-accessor verdict (P21f; measured @ 9aae96977 under <c>Analyze</c>): a body whose
    /// <c>event add</c>/<c>event remove</c> accessors of the name lack one of the two draws ONE SPY0420, at the FIRST
    /// accessor of the name — whether the member seam admitted that accessor or refused it (the half's own verdict, not
    /// a cascade of the collision, D9 (i)); an auto <c>event</c> is not an accessor.</summary>
    private static IEnumerable<(string Code, int Line, int Column)> LoneEventAccessor(IReadOnlyList<MemberKind> kinds, int[] lines)
    {
        var accessors = Enumerable.Range(0, kinds.Count).Where(i => kinds[i].Kind is "event-add" or "event-remove").ToList();
        if (accessors.Count == 0
            || (accessors.Any(i => kinds[i].Kind == "event-add") && accessors.Any(i => kinds[i].Kind == "event-remove")))
            yield break;
        var (line, column) = MemberPosition(kinds[accessors[0]], lines[accessors[0]], 4);
        yield return ("SPY0420", line, column);
    }

    /// <summary>The 0-based column offset of the name <c>A</c> in a declaration's first line.</summary>
    private static int NameOffset(string line) => Regex.Match(line, @"(?<!\w)A(?!\w)").Index;

    private static (int Line, int Column) MemberPosition(MemberKind kind, int line, int indent)
        => kind.Where == At.Name ? (line, indent + NameOffset(kind.Lines[0]) + 1) : (line, indent + 1);

    /// <summary>
    /// The member-pair cells: host {module, class, struct, interface} × kind A × kind B (every ordered pair, same-kind
    /// included) — 9 type-body kinds × 3 hosts, 8 module kinds —, <c>type-param</c> (the <c>Host[A]</c> head, always the
    /// earlier declaration) as kind A beside each type-body kind in each host, the same-signature method/def twin as its
    /// own relation per host, one <c>member-alone</c> control per kind per host, and the host EDGES: the enum (member
    /// twice, and alone) and the union (its method alone, a method overload, the same-signature twin, and a case beside a
    /// same-named method). The union's <c>alias → method</c> pair the plan names is NOT spellable — a <c>type</c> alias in
    /// a union body is SPY0104 "Expected Case" alone (measured), a host rule — so it is not a cell; the union's other
    /// member kinds are refused by the same parser rule.
    ///
    /// <para><b>Expected</b> (D2, D4, D9; R-JB: in one body a name has ONE meaning). A refused pair is ONE SPY0204 at the
    /// LATER declaration, at the later kind's position (D2: a nested type, alias, property, event or const at its first
    /// token; a method at its name token; a field or module variable at its name). Type bodies are walked in source
    /// order, so the later is B. At module level the later is the later BINDING: a def is hoisted (always earlier), a
    /// variable is declared by the checker after every other declaration (always later), everything else is source
    /// order. The legal pairs (<see cref="LegalPair"/>) are [] and run, printing through both halves. Pinned controls:
    /// the same-signature method twin is [SPY0355 @ the second <c>def</c>] (the existing overload rule, r6) and a
    /// module def twin [SPY0204 @ the second <c>def</c>]; module <c>const</c> × <c>var</c> is [SPY0225 @ the variable]
    /// in both orders; module <c>var</c> × <c>var</c> is [] and runs (legal rebinding — the bindings plan's, #1974, not
    /// this contract's; its <c>main</c> prints <c>ok</c>, so the cell pins the verdict, not the rebinding's value). No
    /// expected set holds SPY0405/SPY0406/SPY0421/SPY0422 (D3 retires them; asserted, hard), and no cell may draw a
    /// SPY09xx under <c>Analyze</c> (hard). The alone controls are [] and run: a class or struct host is instantiated
    /// (<c>Host()</c>), an interface host through <c>class Impl(Host)</c> carrying the members an interface leaves
    /// abstract (the auto property and the auto event); the interface FIELD's alone control prints <c>ok</c> without a
    /// use — a non-const interface field is an abstract property no implementer can satisfy (findings ledger, outside
    /// this contract). A refused cell's <c>main</c> prints <c>ok</c> and uses nothing (D10: cascades are the hand
    /// fixtures').</para>
    ///
    /// <para><b>P21h</b> (#2382, #2390, #2075; R-JG, R-JI, R-JO; D9 (i) (iii) (iv)) adds the kinds the P21g matrix never
    /// enumerated — the split auto halves, the event accessors and the interface's stubs (13 kinds in a class or struct,
    /// 15 in an interface, whose auto halves carry no default) —, the host-aware legal pairs (<see cref="LegalPair"/>),
    /// the R-JG refusal of an interface abstract half beside a bodied one (<see cref="InterfaceBodyMismatch"/>), the
    /// two-defaults SPY0407 cell, the lone-accessor SPY0420 (<see cref="LoneEventAccessor"/>), the union's case × case
    /// and type-param × case edge, and the three-declaration chains (<see cref="MemberChainCells"/>).</para>
    /// </summary>
    internal static IEnumerable<MemberPairCell> MemberPairCells()
    {
        // `show` is the alone event control's handler; every cell carries it so line numbers are uniform.
        var prelude = new[] { "def show(v: int) -> None:", "    print(v)", "" };
        return TypeBodyPairCells(prelude).Concat(ModulePairCells(prelude)).Concat(EdgeMemberPairCells(prelude))
            .Concat(MemberChainCells()).Concat(ExplicitMemberCells());
    }

    /// <summary>One program: the prelude, the host head, the members (indented under a head), the interface
    /// implementer of a running cell, and <c>main</c>. Returns the source and each member's 1-based first line.</summary>
    private static (string Source, int[] MemberLines) MemberProgram(
        string[] prelude, string[]? header, IReadOnlyList<string[]> members, string[]? implementer, string[] mainBody)
    {
        var indent = header == null ? "" : "    ";
        var lines = new List<string>(prelude);
        if (header != null)
            lines.AddRange(header);
        var memberLines = new int[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            memberLines[i] = lines.Count + 1;
            lines.AddRange(members[i].Select(l => indent + l));
        }

        lines.Add("");
        if (implementer != null)
        {
            lines.AddRange(implementer);
            lines.Add("");
        }

        lines.Add("def main() -> None:");
        lines.AddRange(mainBody.Select(l => "    " + l));
        lines.Add("");
        return (string.Join("\n", lines), memberLines);
    }

    /// <summary>The statements that construct <c>h</c> for a running type-body cell, and the interface implementer.</summary>
    private static (string[] Construct, string[]? Implementer) Receiver(string host, IEnumerable<MemberKind> kinds)
    {
        if (host != "interface")
            return (new[] { "h: Host = Host()" }, null);
        var impl = kinds.SelectMany(k => k.ImplLines ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToList();
        var body = impl.Count == 0 ? new[] { "    pass" } : impl.Select(l => "    " + l).ToArray();
        return (new[] { "h: Host = Impl()" }, new[] { "class Impl(Host):" }.Concat(body).ToArray());
    }

    private static IEnumerable<MemberPairCell> TypeBodyPairCells(string[] prelude)
    {
        var okMain = new[] { "print(\"ok\")" };
        foreach (var host in TypeBodyHosts)
        {
            var header = new[] { $"{host} Host:" };
            var kinds = KindsFor(host);

            // The alone controls: each kind compiles in the host and runs through its use.
            foreach (var kind in kinds)
            {
                // The interface field: no use reaches it (an abstract property no implementer satisfies — ledgered).
                var noUse = host == "interface" && kind.Kind == "field";
                var (construct, implementer) = Receiver(host, new[] { kind });
                var main = noUse ? okMain : construct.Concat(kind.Use).ToArray();
                var (source, _) = MemberProgram(prelude, header, AloneMembers(kind), noUse ? null : implementer, main);
                yield return new MemberPairCell($"member-alone × {host} × {kind.Kind}", host, source,
                    Array.Empty<(string, int, int)>(), noUse ? "ok\n" : kind.Stdout + "\n");
            }

            foreach (var a in kinds)
            {
                foreach (var b in kinds)
                {
                    var bLines = a.Kind == "method" && b.Kind == "method" ? SecondMethod(module: false, sameSignature: false) : b.Lines;
                    var legal = LegalPair(a.Kind, b.Kind, host);
                    var id = $"member-pair × {host} × {a.Kind} → {b.Kind}";
                    if (legal is { } run)
                    {
                        var (construct, implementer) = Receiver(host, new[] { a, b });
                        var (source, _) = MemberProgram(prelude, header, new[] { a.Lines, bLines }, implementer,
                            construct.Concat(run.Use).ToArray());
                        yield return new MemberPairCell(id, host, source, Array.Empty<(string, int, int)>(), run.Stdout);
                    }
                    else
                    {
                        var (source, at) = MemberProgram(prelude, header, new[] { a.Lines, bLines }, null, okMain);
                        var (line, column) = MemberPosition(b, at[1], 4);
                        var code = InterfaceBodyMismatch(a.Kind, b.Kind, host) ? "SPY0617" : "SPY0204";
                        yield return new MemberPairCell(id, host, source,
                            new[] { (code, line, column) }.Concat(LoneEventAccessor(new[] { a, b }, at)).ToArray(), null);
                    }
                }
            }

            // Two defaulted auto halves (D3, owner ruling 2026-10-28 (a)): the seam admits the complementary slots and
            // no C# auto property holds two initializers — [SPY0407 @ the later half's keyword], both orders.
            if (host != "interface")
            {
                var autoGet = kinds.Single(k => k.Kind == "property-auto-get");
                foreach (var (a, b) in new[] { (autoGet, AutoSetWithDefault), (AutoSetWithDefault, autoGet) })
                {
                    var (source, at) = MemberProgram(prelude, header, new[] { a.Lines, b.Lines }, null, okMain);
                    var (line, column) = MemberPosition(b, at[1], 4);
                    yield return new MemberPairCell($"member-pair × {host} × {a.Kind} → {b.Kind}", host, source,
                        new[] { ("SPY0407", line, column) }, null);
                }
            }

            // The halves of one property agree on @static (P21h, lead decision on the verifier's cell): a static auto
            // getter beside an instance auto or function-style setter is a second meaning of the name — [SPY0204 @ the
            // later half's keyword], both orders (the static half's keyword is the line after its decorator), in every
            // type-body host — the static check precedes the interface's SPY0617 arm.
            {
                var staticGet = StaticAutoGet(host);
                foreach (var setter in kinds.Where(k => k.Kind is "property-auto-set" or "property-set"))
                {
                    foreach (var (a, b) in new[] { (staticGet, setter), (setter, staticGet) })
                    {
                        var (source, at) = MemberProgram(prelude, header, new[] { a.Lines, b.Lines }, null, okMain);
                        var (line, column) = MemberPosition(b, at[1] + (ReferenceEquals(b, staticGet) ? 1 : 0), 4);
                        yield return new MemberPairCell($"member-pair × {host} × {a.Kind} → {b.Kind}", host, source,
                            new[] { ("SPY0204", line, column) }, null);
                    }
                }
            }

            // The same-signature method twin: the existing overload rule, SPY0355 at the second `def`.
            {
                var method = TypeBodyMemberKinds.Single(k => k.Kind == "method");
                var (source, at) = MemberProgram(prelude, header,
                    new[] { method.Lines, SecondMethod(module: false, sameSignature: true) }, null, okMain);
                yield return new MemberPairCell($"member-pair × {host} × method → method-same-sig", host, source,
                    new[] { ("SPY0355", at[1], 5) }, null);
            }

            // The type parameter: declared by the head, always the earlier declaration.
            foreach (var b in kinds)
            {
                var (source, at) = MemberProgram(prelude, new[] { $"{host} Host[A]:" }, new[] { b.Lines }, null, okMain);
                var (line, column) = MemberPosition(b, at[0], 4);
                yield return new MemberPairCell($"member-pair × {host} × type-param → {b.Kind}", host, source,
                    new[] { ("SPY0204", line, column) }.Concat(LoneEventAccessor(new[] { b }, at)).ToArray(), null);
            }
        }
    }

    /// <summary>The split pairs a chain's second and third declarations spell (D9 (iv)).</summary>
    private static readonly (string First, string Second)[] ChainPairs =
    {
        ("property-auto-get", "property-auto-set"), ("property-get", "property-set"), ("event-add", "event-remove"),
    };

    /// <summary>The kinds a chain's FIRST declaration spells (D9 (iv)).</summary>
    private static readonly string[] ChainHeads = { "nested-class", "method", "field", "const", "alias" };

    /// <summary>The third-half chains (D9 (iv)): a legal merge (or an auto-both half) followed by a half its group
    /// already holds.</summary>
    private static readonly string[][] ThirdHalfChains =
    {
        new[] { "property-field", "property-get", "property-get" },
        new[] { "property-field", "property-set", "property-set" },
        new[] { "property-get", "property-set", "property-get" },
    };

    /// <summary>
    /// The three-declaration chains (P21h D9 (iv), R-JO on #2075): host {class, struct, interface} × a member A ∈
    /// {nested-class, method, field, const, alias} followed by BOTH halves of one split pair ∈ {auto-get + auto-set,
    /// get + set, event add + remove} — ids <c>member-chain × &lt;host&gt; × &lt;A&gt; → &lt;first&gt;+&lt;second&gt;</c>
    /// — expected exactly ONE SPY0204 at the FIRST half (the refused declaration GROUP draws one verdict; the second
    /// half is silent, D7); and the third-half chains (ids <c>member-chain × &lt;host&gt; × &lt;k1&gt; → &lt;k2&gt; →
    /// &lt;k3&gt;</c>) — ONE SPY0204 at the third and NO SPY0407 (the refused half is invisible to
    /// <c>ValidateMixedPropertyStyle</c>, D7) in a class or struct. In an interface the field-style head is abstract,
    /// so a bodied second half is the R-JG refusal — ONE SPY0617 at the second, and the third half is silent (D2: the
    /// refused name's later halves draw nothing); the bodied get + set + get chain is SPY0204 at the third. The sets
    /// are exact (hard): a second SPY0204 or a SPY0407 is red.
    /// </summary>
    internal static IEnumerable<MemberPairCell> MemberChainCells()
    {
        var prelude = new[] { "def show(v: int) -> None:", "    print(v)", "" };
        var okMain = new[] { "print(\"ok\")" };
        foreach (var host in TypeBodyHosts)
        {
            var header = new[] { $"{host} Host:" };
            var kinds = KindsFor(host).ToDictionary(k => k.Kind, StringComparer.Ordinal);
            foreach (var head in ChainHeads)
            {
                foreach (var (first, second) in ChainPairs)
                {
                    var chain = new[] { kinds[head], kinds[first], kinds[second] };
                    var (source, at) = MemberProgram(prelude, header, chain.Select(k => k.Lines).ToArray(), null, okMain);
                    var (line, column) = MemberPosition(chain[1], at[1], 4);
                    yield return new MemberPairCell($"member-chain × {host} × {head} → {first}+{second}", host, source,
                        new[] { ("SPY0204", line, column) }, null);
                }
            }

            foreach (var spelled in ThirdHalfChains)
            {
                var chain = spelled.Select(k => kinds[k]).ToArray();
                var (source, at) = MemberProgram(prelude, header, chain.Select(k => k.Lines).ToArray(), null, okMain);
                var refusedAt = InterfaceBodyMismatch(spelled[0], spelled[1], host) ? 1 : 2;
                var (line, column) = MemberPosition(chain[refusedAt], at[refusedAt], 4);
                yield return new MemberPairCell($"member-chain × {host} × {string.Join(" → ", spelled)}", host, source,
                    new[] { (refusedAt == 1 ? "SPY0617" : "SPY0204", line, column) }, null);
            }
        }
    }

    /// <summary>The explicit-interface property of the sub-matrix (D9 (ii)): it implements <c>IHost.A</c> and claims no
    /// bare name, so its use goes through the interface. It prints <c>10</c> — a value no bare kind prints (the plan's
    /// <c>4</c> is the <c>const</c> kind's, so a read that reached the bare const would not discriminate).</summary>
    private static readonly MemberKind ExplicitProperty =
        new("explicit-property", new[] { "property IHost.A: int = 10" }, At.Keyword, new[] { "i: IHost = h", "print(i.A)" }, "10");

    /// <summary>
    /// The explicit-interface sub-matrix (P21h D9 (ii), R-JH, #2390): host {class, struct} implementing <c>interface
    /// IHost: property A: int</c> × <c>explicit-property</c> (<c>property IHost.A: int = 4</c>) beside every type-body
    /// kind, in both orders — ids <c>explicit-member × &lt;host&gt; × &lt;A&gt; → &lt;B&gt;</c>. An explicit
    /// implementation claims no bare name (R-JH: the member key is <c>(Name, ExplicitInterface)</c>), so every such pair
    /// is [] and runs, printing through BOTH members: the bare kind's alone use, then <c>10</c> through the interface (an
    /// event accessor's kind is its add/remove pair, as in its alone control). Three more cells per host: the explicit
    /// member twice — [SPY0204 @ the later] (r03: one key, two declarations); an <c>IHost2.A</c> explicit member beside
    /// the <c>IHost.A</c> one — [] and runs <c>10\n20</c> (r04: two keys); the explicit function-style split halves —
    /// [] and runs <c>7\n8</c> (r05: the explicit key keeps the merge).
    /// </summary>
    internal static IEnumerable<MemberPairCell> ExplicitMemberCells()
    {
        var prelude = new[]
        {
            "def show(v: int) -> None:", "    print(v)", "",
            "interface IHost:", "    property A: int", "",
            "interface IHost2:", "    property A: int", "",
        };
        foreach (var host in new[] { "class", "struct" })
        {
            var header = new[] { $"{host} Host(IHost):" };
            var construct = new[] { "h: Host = Host()" };
            foreach (var kind in TypeBodyMemberKinds)
            {
                var bare = AloneMembers(kind);
                foreach (var explicitFirst in new[] { true, false })
                {
                    var members = explicitFirst ? new[] { ExplicitProperty.Lines }.Concat(bare).ToArray() : bare.Append(ExplicitProperty.Lines).ToArray();
                    var (source, _) = MemberProgram(prelude, header, members, null, construct.Concat(kind.Use).Concat(ExplicitProperty.Use).ToArray());
                    var id = explicitFirst
                        ? $"explicit-member × {host} × {ExplicitProperty.Kind} → {kind.Kind}"
                        : $"explicit-member × {host} × {kind.Kind} → {ExplicitProperty.Kind}";
                    yield return new MemberPairCell(id, host, source, Array.Empty<(string, int, int)>(), $"{kind.Stdout}\n{ExplicitProperty.Stdout}\n");
                }
            }

            {
                var (source, at) = MemberProgram(prelude, header, new[] { ExplicitProperty.Lines, ExplicitProperty.Lines }, null, new[] { "print(\"ok\")" });
                var (line, column) = MemberPosition(ExplicitProperty, at[1], 4);
                yield return new MemberPairCell($"explicit-member × {host} × explicit-property → explicit-property", host, source,
                    new[] { ("SPY0204", line, column) }, null);
            }
            {
                var (source, _) = MemberProgram(prelude, new[] { $"{host} Host(IHost, IHost2):" },
                    new[] { ExplicitProperty.Lines, new[] { "property IHost2.A: int = 20" } }, null,
                    construct.Concat(ExplicitProperty.Use).Concat(new[] { "j: IHost2 = h", "print(j.A)" }).ToArray());
                yield return new MemberPairCell($"explicit-member × {host} × explicit-property → explicit-property-ihost2", host, source,
                    Array.Empty<(string, int, int)>(), "10\n20\n");
            }
            {
                var (source, _) = MemberProgram(prelude, header,
                    new[] { new[] { "property get IHost.A(self) -> int:", "    return 8" }, new[] { "property set IHost.A(self, v: int):", "    print(v)" } },
                    null, construct.Concat(new[] { "i: IHost = h", "i.A = 7", "print(i.A)" }).ToArray());
                yield return new MemberPairCell($"explicit-member × {host} × explicit-get → explicit-set", host, source,
                    Array.Empty<(string, int, int)>(), "7\n8\n");
            }
        }
    }

    private static IEnumerable<MemberPairCell> ModulePairCells(string[] prelude)
    {
        var okMain = new[] { "print(\"ok\")" };
        foreach (var kind in ModuleMemberKinds)
        {
            var (source, _) = MemberProgram(prelude, null, new[] { kind.Lines }, null, kind.Use);
            yield return new MemberPairCell($"member-alone × module × {kind.Kind}", "module", source,
                Array.Empty<(string, int, int)>(), kind.Stdout + "\n");
        }

        foreach (var a in ModuleMemberKinds)
        {
            foreach (var b in ModuleMemberKinds)
            {
                var bLines = a.Kind == "def" && b.Kind == "def" ? SecondMethod(module: true, sameSignature: false) : b.Lines;
                var id = $"member-pair × module × {a.Kind} → {b.Kind}";
                var legal = LegalPair(a.Kind, b.Kind, "module");
                if (legal is { } run)
                {
                    var (source, _) = MemberProgram(prelude, null, new[] { a.Lines, bLines }, null, run.Use);
                    yield return new MemberPairCell(id, "module", source, Array.Empty<(string, int, int)>(), run.Stdout);
                    continue;
                }

                var (program, at) = MemberProgram(prelude, null, new[] { a.Lines, bLines }, null, okMain);
                if (a.Kind == "var" && b.Kind == "var")
                {
                    // Legal rebinding (the bindings plan's, #1974): pinned as measured — [] and it runs.
                    yield return new MemberPairCell(id, "module", program, Array.Empty<(string, int, int)>(), "ok\n");
                    continue;
                }

                // The later BINDING: a variable is always later, a def always earlier, else source order.
                var bLater = b.Kind == "var" || (a.Kind != "var" && (a.Kind == "def" || b.Kind != "def"));
                var later = bLater ? (Kind: b, Line: at[1]) : (Kind: a, Line: at[0]);
                var (line, column) = MemberPosition(later.Kind, later.Line, 0);
                var code = (a.Kind, b.Kind) is ("const", "var") or ("var", "const") ? "SPY0225" : "SPY0204";
                yield return new MemberPairCell(id, "module", program, new[] { (code, line, column) }, null);
            }
        }

        // The same-signature def twin: the existing rule, SPY0204 at the second `def`'s keyword.
        {
            var def = ModuleMemberKinds.Single(k => k.Kind == "def");
            var (source, at) = MemberProgram(prelude, null, new[] { def.Lines, SecondMethod(module: true, sameSignature: true) }, null, okMain);
            yield return new MemberPairCell("member-pair × module × def → def-same-sig", "module", source,
                new[] { ("SPY0204", at[1], 1) }, null);
        }
    }

    private static IEnumerable<MemberPairCell> EdgeMemberPairCells(string[] prelude)
    {
        var okMain = new[] { "print(\"ok\")" };

        // The enum host: its one member kind, twice (SPY0204 at the second member's name) and alone.
        var enumHeader = new[] { "enum Host:" };
        {
            var (source, _) = MemberProgram(prelude, enumHeader, new[] { new[] { "A = 1" } }, null, new[] { "print(Host.A)" });
            yield return new MemberPairCell("member-alone × enum × member", "enum", source, Array.Empty<(string, int, int)>(), "Host.A\n");
        }
        {
            var (source, at) = MemberProgram(prelude, enumHeader, new[] { new[] { "A = 1" }, new[] { "A = 2" } }, null, okMain);
            yield return new MemberPairCell("member-pair × enum × member → member", "enum", source, new[] { ("SPY0204", at[1], 5) }, null);
        }

        // The union host: it registers methods only (its walk hands NameResolver nothing else).
        var unionHeader = new[] { "union Host:", "    case P(v: int)", "" };
        var method = TypeBodyMemberKinds.Single(k => k.Kind == "method");
        var construct = new[] { "h: Host = Host.P(1)" };
        {
            var (source, _) = MemberProgram(prelude, unionHeader, new[] { method.Lines }, null, construct.Concat(method.Use).ToArray());
            yield return new MemberPairCell("member-alone × union × method", "union", source, Array.Empty<(string, int, int)>(), "2\n");
        }
        {
            var run = LegalPair("method", "method", "union")!.Value;
            var (source, _) = MemberProgram(prelude, unionHeader, new[] { method.Lines, SecondMethod(module: false, sameSignature: false) },
                null, construct.Concat(run.Use).ToArray());
            yield return new MemberPairCell("member-pair × union × method → method", "union", source, Array.Empty<(string, int, int)>(), run.Stdout);
        }
        {
            var (source, at) = MemberProgram(prelude, unionHeader, new[] { method.Lines, SecondMethod(module: false, sameSignature: true) },
                null, okMain);
            yield return new MemberPairCell("member-pair × union × method → method-same-sig", "union", source,
                new[] { ("SPY0355", at[1], 5) }, null);
        }
        {
            // A case and a method of one name: the case is the earlier declaration (the union's head).
            var (source, at) = MemberProgram(prelude, new[] { "union Host:", "    case A(v: int)", "" }, new[] { method.Lines }, null, okMain);
            var (line, column) = MemberPosition(method, at[0], 4);
            yield return new MemberPairCell("member-pair × union × case → method", "union", source, new[] { ("SPY0204", line, column) }, null);
        }

        // The case edge (P21h D9 (iii), R-JI): a case claims its name through the member seam like every member — a
        // second case of the name, adjacent or after a method, and a case named like a type parameter are [SPY0204 @
        // the later case's first token].
        var caseLines = new[] { "case A(v: int)" };
        var otherMethod = new[] { "def m(self) -> int:", "    return 2" };
        foreach (var (id, header, members, refused) in new[]
        {
            ("member-pair × union × case → case", new[] { "union Host:" }, new[] { caseLines, caseLines }, 1),
            ("member-pair × union × case → case-after-def", new[] { "union Host:" }, new[] { caseLines, otherMethod, caseLines }, 2),
            ("member-pair × union × type-param → case", new[] { "union Host[A]:" }, new[] { caseLines }, 0),
        })
        {
            var (source, at) = MemberProgram(prelude, header, members, null, okMain);
            yield return new MemberPairCell(id, "union", source, new[] { ("SPY0204", at[refused], 5) }, null);
        }
    }

    /// <summary>A failed run's internal codes with the C# error ids their messages carry (<c>SPY0908[CS0102]</c>).</summary>
    private static string RunFingerprint(ExecutionResult run)
    {
        var internalCodes = InternalCodes(run);
        if (internalCodes.Count == 0)
            return $"refused [{string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}]";
        return string.Join(" ", internalCodes.Select(d =>
        {
            var cs = Regex.Matches(d.Message, @"CS\d{4}").Select(m => m.Value).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal);
            return $"{d.Code}[{string.Join(",", cs)}]";
        }).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>The validator codes D3 retires into SPY0204: never in an expected set.</summary>
    private static readonly HashSet<string> RetiredMemberConflictCodes = new(StringComparer.Ordinal) { "SPY0405", "SPY0406", "SPY0421", "SPY0422" };

    [Fact]
    public void MemberPair_HasOneVerdictPerKindPair()
    {
        var allowlist = LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();
        var ran = 0;
        foreach (var cell in MemberPairCells())
        {
            // Hard: the contract in code names one verdict, SPY0204 — never a retired per-pair code.
            if (cell.ExpectedErrors.Any(e => RetiredMemberConflictCodes.Contains(e.Code)))
                problems.Add($"{cell.Id}: the expected set names a retired member-conflict code");

            var result = api.Analyze(cell.Source, CancellationToken.None);
            var line = $"{cell.Id}: {Fingerprint(result.Diagnostics)}";
            Output.WriteLine(line);
            var failures = new List<string>();

            // An internal error under Analyze is HARD, as in the other sub-matrices — never allowlisted. It was
            // ratcheted while the base's nested-type twins crashed the checker's type-scope re-registration (SPY0909
            // "Symbol 'A' is already defined in this scope", measured @ 75c65ee29); the member seam drained those rows
            // (P21g Phase 2, #2360), so an allowlist row can no longer stand in front of a crash.
            var internalUnderAnalyze = result.Diagnostics.Where(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)).ToList();
            if (internalUnderAnalyze.Count > 0)
                problems.Add($"{cell.Id}: an internal error under Analyze (hard, never allowlisted): "
                    + string.Join(" | ", internalUnderAnalyze.Select(d => d.Message)).ReplaceLineEndings(" "));

            var expected = cell.ExpectedErrors.Select(e => $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}")
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                // A refusal the analysis does not make: say what the program does under run (an ICE, or it runs), so a
                // red line tells the SPY0908 net from a silent second meaning. Diagnostic text only.
                var underRun = "";
                if (actual.Count == 0 && cell.ExpectedStdout is null)
                {
                    var silent = CompileAndExecute(cell.Source);
                    underRun = silent.Success
                        ? $" — under run it RUNS, stdout=[{silent.StandardOutput.ReplaceLineEndings("\\n")}]"
                        : $" — under run: {RunFingerprint(silent)}";
                }

                failures.Add($"errors are [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]{underRun}");
            }

            var shadow = result.Diagnostics.Where(d => !d.IsError && VerdictWarnings.Contains(d.Code ?? "")).ToList();
            if (shadow.Count > 0)
                failures.Add($"shadowing warnings [{Fingerprint(shadow)}], expected none");

            if (cell.ExpectedStdout is { } stdout)
            {
                ran++;
                var run = CompileAndExecute(cell.Source);
                var internalCodes = InternalCodes(run);
                if (internalCodes.Count > 0)
                    failures.Add($"an internal error under run: {RunFingerprint(run)} "
                        + string.Join(" | ", internalCodes.Select(d => d.Message)).ReplaceLineEndings(" "));
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));

                // A row citing #2021 claims the struct auto-constructor's duplicate parameter: the run must fail with
                // SPY0908 carrying CS0100 and nothing else.
                if (allowlist.TryGetValue(cell.Id, out var structCite) && structCite.Contains("#2021", StringComparison.Ordinal)
                    && !IsStructCtorDuplicate(run, internalCodes))
                    problems.Add($"{cell.Id}: allowlisted as the struct auto-constructor CS0100 (#2021), but under run the cell is "
                        + $"success={run.Success} [{Fingerprint(internalCodes)}] {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}");
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        Output.WriteLine($"{MemberPairCells().Count()} member-pair cells, {ran} executed, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The allowlist ratchet.
    // ---------------------------------------------------------------------------------------------------------------

    internal const string AllowlistFileName = "declaration-position-allowlist.txt";
    private static readonly Regex IssueCite = new(@"#\d+", RegexOptions.CultureInvariant);

    internal static void Ratchet(string id, List<string> failures, IReadOnlyDictionary<string, string> allowlist,
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

    /// <summary>Cell id → cite. A row must cite an issue (<c>#\d+</c>), name a generated cell, and appear once. The
    /// universe includes the project-route cells of <c>ImportBindingProjectRouteTests</c> (ids <c>project × …</c>), which
    /// ratchet over this file too (P21g), the class-body read cells of <c>ClassBodyReadPositionMatrixTests</c> (ids
    /// <c>class-body-read × …</c>, P21h), and the module-member kind × read-site cells of
    /// <c>ModuleMemberQualificationMatrixTests</c> (ids <c>module-member × …</c>, P21i).</summary>
    internal static Dictionary<string, string> LoadAllowlist(out List<string> problems)
    {
        problems = new List<string>();
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Semantic/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var universe = new HashSet<string>(
            Cells().Select(c => c.Id).Concat(NestedDefCells().Select(c => c.Id))
                .Concat(ImportBindingCells().Select(c => c.Id)).Concat(AliasRedeclarationCells().Select(c => c.Id))
                .Concat(MemberPairCells().Select(c => c.Id))
                .Concat(global::Sharpy.Compiler.Tests.Project.ImportBindingProjectRouteTests.CellIds())
                .Concat(ClassBodyReadPositionMatrixTests.CellIds())
                .Concat(global::Sharpy.Compiler.Tests.CodeGen.ModuleMemberQualificationMatrixTests.CellIds()),
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
