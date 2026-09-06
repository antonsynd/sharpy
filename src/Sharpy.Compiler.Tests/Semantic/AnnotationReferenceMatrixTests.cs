using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The annotation-reference matrix -- symbol kind (10) x position (12) -- and its controls (#1737).
///
/// <para><b>Contract.</b> <c>SetTypeAnnotation(annotation, type, boundSymbol)</c> is the ONLY
/// reference-recording seam for type annotations. When <c>boundSymbol</c> is non-null,
/// <c>RecordReference(boundSymbol, annotation)</c> records the annotation position as a
/// reference to that symbol. Every branch in <c>TypeResolver.ResolveTypeAnnotation</c> must
/// answer which symbol it bound (user type, alias, type parameter, CLR import) or pass null
/// (builtins, primitives, keywords). The identifier-use twin is the positive control: an
/// expression-position reference to the same symbol is counted by <c>SetIdentifierSymbol</c>
/// and always works.</para>
///
/// <para><b>Axes.</b> Kind: {class, struct, interface, enum, union, delegate, alias,
/// generic alias, type parameter, CLR type via import} x Position: {parameter, return,
/// variable, field, base-class list, generic argument, C | None, C?, C!E, as? target,
/// isinstance argument, pattern head}.
/// Every live cell asserts <c>GetReferences(symbol).Count == 1</c>: each program spells the type
/// exactly once, in the position under test. Cells that are not live are in
/// <see cref="NotApplicable"/> with a reason, and live + rostered = 10 x 12 is asserted, so a
/// position cannot quietly go missing.</para>
///
/// <para><b>Which seam each position measures.</b> Measured by deleting the recording from
/// <c>SetTypeAnnotation</c>: 83 of the 95 tests here go red, and the nine that stay green are the
/// <c>isinstance</c> cells plus the identifier-use control. The <c>isinstance</c> second argument
/// is an EXPRESSION position -- it is recorded by <c>SetIdentifierSymbol</c>, not by the annotation
/// seam -- so those cells guard the type-position contract without guarding this seam. Every other
/// live cell is on the annotation seam.</para>
///
/// <para><b>The alias-body position</b> is not a kind x position cell -- the spelling sits inside a
/// declaration rather than at a use site -- so it has its own facts below. A generic alias body is
/// resolved under <c>_suppressAnnotationCache</c>, which used to suppress the REFERENCE along with
/// the cache: <c>type Box[T] = dict[T, D]</c> recorded 0 references to D while the non-generic
/// <c>type Box = list[D]</c> recorded 1. The suppression is about which TYPE a shared body resolves
/// to, which is instantiation-dependent; which symbol its spelling NAMES is not.</para>
/// </summary>
public class AnnotationReferenceMatrixTests
{
    /// <summary>
    /// Sharpy.Core is what makes <c>import builtins</c> and the CLR module walk resolvable. With
    /// the bare <c>new CompilerApi()</c> every <c>from System.Text import ...</c> program is SPY0300
    /// ("Cannot find module 'System'") and the CLR-import kind cannot be measured at all.
    /// </summary>
    private readonly CompilerApi _api = new(null, new[] { SharpyCoreReference.Location });

    // -- Axis sizes, anchored to literals -----------------------------------------
    private const int SymbolKindCount = 10;
    private const int PositionCount = 12;

    private static readonly string[] SymbolKinds =
    {
        "class", "struct", "interface", "enum", "union",
        "delegate", "alias", "generic_alias", "type_parameter", "clr_import",
    };

    private static readonly string[] Positions =
    {
        "parameter", "return", "variable", "field", "base_class_list", "generic_argument",
        "csharp_nullable", "optional", "result", "as_target", "isinstance_argument", "pattern_head",
    };

    /// <summary>
    /// Cells that are not live, each with the reason. Three kinds of reason appear:
    /// a language fact (the spelling is not legal in that position), a rostered future batch, and
    /// a MEASURED seam gap that is reported rather than encoded as an expectation -- those cells
    /// become live when the gap closes, which is what makes the roster drain.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NotApplicable = BuildRoster();

    private static Dictionary<string, string> BuildRoster()
    {
        var roster = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kind in SymbolKinds)
        {
            // Pattern head (`case C():`) is rostered N/A for Batch 6 per plan-4e3055.
            roster[Key(kind, "pattern_head")] = "Batch 6 -- pattern-head type positions are not "
                + "resolved through the annotation seam yet.";
        }

        // Base-class list: LIVE. Inheritance is resolved in name resolution, a pass before any
        // SemanticInfo exists, so the base annotation used to miss the seam entirely and
        // `class Sub(C)` recorded 0 references to C. The checker now resolves the annotation the
        // reference record carries (BaseTypeReference.SourceAnnotation), so the spelling reaches the
        // one seam and counts once. Only the kinds that cannot BE a base stay rostered.
        foreach (var kind in new[] { "struct", "enum", "union", "delegate", "alias", "generic_alias", "type_parameter", "clr_import" })
        {
            roster[Key(kind, "base_class_list")] =
                "not a base type: Sharpy inherits from a class and implements an interface only.";
        }

        // `as?` target: LIVE. ClassifyTypeTestAnnotation binds the target's symbol and decided a
        // type without ever recording the spelling, so `o as? C` recorded 0 references to C. The
        // classifier now records at the ONE point it binds, which covers every position that shares
        // it (`as?`/`as!`, `is`, `except`, match heads) rather than the cast alone.
        roster[Key("type_parameter", "as_target")] =
            "a bare type parameter is not a runtime type-test target.";

        // isinstance's second argument is a type position, and a generic alias is not a type there.
        roster[Key("generic_alias", "isinstance_argument")] =
            "SPY0344: a generic alias application is not a runtime type-test target.";

        return roster;
    }

    private static string Key(string kind, string position) => kind + "/" + position;

    public static TheoryData<string, string> LiveCells()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in SymbolKinds)
        {
            foreach (var position in Positions)
            {
                if (!NotApplicable.ContainsKey(Key(kind, position)))
                    data.Add(kind, position);
            }
        }

        return data;
    }

    // -- The matrix ----------------------------------------------------------------

    [Theory]
    [MemberData(nameof(LiveCells))]
    public void AnnotationPosition_RecordsExactlyOneReference(string kind, string position)
    {
        var source = BuildProgram(kind, position);
        var analysis = _api.Analyze(source);

        analysis.Success.Should().BeTrue(
            "[{0} x {1}] must compile. Errors: {2}\nSource:\n{3}",
            kind, position, string.Join(" | ", analysis.Diagnostics.Where(d => d.IsError).Select(d => d.Code + ": " + d.Message)), source);

        CountReferences(analysis, kind).Should().Be(1,
            "[{0} x {1}] the type is spelled exactly once, in the position under test, and the "
            + "annotation seam records that spelling as one reference (#1737).\nSource:\n{2}",
            kind, position, source);
    }

    // -- Controls ------------------------------------------------------------------

    /// <summary>
    /// A GENERIC alias body's spelling is a reference, exactly as a non-generic one's is. The body's
    /// annotation objects are shared across use sites, so its resolved TYPE is not cached; that must
    /// not take the reference with it.
    /// </summary>
    [Fact]
    public void GenericAliasBody_RecordsItsSpellingOnce()
    {
        var source = "class D:\n    pass\n\ntype Box[T] = dict[T, D]\n\n"
            + "def use(b: Box[str]) -> None:\n    print(len(b))\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue(
            "Errors: {0}",
            string.Join(" | ", analysis.Diagnostics.Where(d => d.IsError).Select(d => d.Code + ": " + d.Message)));

        var symbol = analysis.SymbolTable!.LookupType("D");
        symbol.Should().NotBeNull();
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1,
            "the alias body spells D once, and a suppressed CACHE must not suppress the reference");
    }

    /// <summary>
    /// The non-generic twin: the control that shows the count above is the body's own contribution
    /// and not something the generic machinery adds.
    /// </summary>
    [Fact]
    public void NonGenericAliasBody_RecordsItsSpellingOnce()
    {
        var source = "class D:\n    pass\n\ntype Box = list[D]\n\n"
            + "def use(b: Box) -> None:\n    print(len(b))\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("D");
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1);
    }

    /// <summary>
    /// ONE spelling stays ONE reference however many times the alias is applied. The cache is what
    /// keeps a re-resolved annotation from counting twice, and a shared body has no cache, so this
    /// is the cell that would catch the reference being recorded per USE SITE.
    /// </summary>
    [Fact]
    public void GenericAliasBody_AppliedTwice_StillRecordsOneReference()
    {
        var source = "class D:\n    pass\n\ntype Box[T] = dict[T, D]\n\n"
            + "def use(b: Box[str]) -> None:\n    print(len(b))\n\n"
            + "def use2(b: Box[int]) -> None:\n    print(len(b))\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("D");
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1,
            "two applications of one alias are two uses of ONE spelling of D");
    }

    /// <summary>
    /// The location-dedupe that makes the cell above hold is a property of the reference SET, not of
    /// the alias path: `o as? C` resolves a modifier-stripped copy of the target annotation and then
    /// the target itself, two resolutions of one spelling at one span.
    /// </summary>
    [Fact]
    public void OneSpelling_ResolvedTwice_IsOneReference()
    {
        var source = "class C:\n    pass\n\ndef use(o: object) -> None:\n    v = o as? C\n"
            + "    print(v is not None)\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("C");
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1,
            "one spelling at one span is one reference, however many times it is resolved");
    }

    [Fact]
    public void IdentifierUse_AlwaysCounted_PositiveControl()
    {
        // The class C is used ONLY via an identifier expression (C()), never in an annotation.
        // This proves SetIdentifierSymbol records references -- the twin control for the matrix.
        var source = "class C:\n    pass\n\ndef main():\n    c = C()\n    print(c)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1,
            "identifier-use twin: an expression-position reference is counted once, so a matrix "
            + "cell reading 1 is the annotation's own contribution and not this route's");
    }

    [Fact]
    public void AnnotationAndIdentifierUse_BothCounted()
    {
        // C is used in an annotation (`c: C`) AND as an identifier (`C()`).
        // Both should be counted, for a total of 2.
        var source = "class C:\n    pass\n\ndef main():\n    c: C = C()\n    print(c)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(2,
            "annotation + identifier: both recorded as references (#1737). This is the cell that "
            + "distinguishes 'the seam records once' from 'the seam records at all'");
    }

    /// <summary>
    /// The <c>builtins.</c>-qualified spelling re-resolves its bare tail recursively and passes
    /// <c>null</c> from the qualified arm deliberately, so ONE spelling records ONE reference
    /// (TypeResolver.cs -- "The recursive call recorded the reference on bareAnnotation"). The
    /// count is read on the type ARGUMENT, which is the only symbol-bound part of a
    /// <c>builtins.list[C]</c> annotation: builtins themselves bind no symbol. If the qualified
    /// arm ever fell through to the shared tail instead of returning, the argument would be
    /// resolved twice and this reads 2.
    /// </summary>
    [Fact]
    public void BuiltinsQualifiedAnnotation_RecordsOneReference_NotTwo()
    {
        var source = "import builtins\n\nclass C:\n    pass\n\n"
                   + "def use(xs: builtins.list[C]) -> None:\n    pass\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);

        analysis.Success.Should().BeTrue(
            "the builtins-qualified spelling must resolve. Errors: {0}",
            string.Join(" | ", analysis.Diagnostics.Where(d => d.IsError).Select(d => d.Code + ": " + d.Message)));

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1,
            "the qualified arm records nothing itself and its recursive re-resolution records once");
    }

    [Fact]
    public void BareBuiltinSpelling_RecordsOneReference_Control()
    {
        // Control for the cell above: the same annotation without the qualifier. Both spellings
        // must count the same, which is the point of the qualified arm.
        var source = "class C:\n    pass\n\ndef use(xs: list[C]) -> None:\n    pass\n\ndef main() -> None:\n    pass\n";
        var analysis = _api.Analyze(source);

        analysis.Success.Should().BeTrue();
        var symbol = analysis.SymbolTable!.LookupType("C");
        analysis.SemanticInfo!.GetReferences(symbol!).Should().HaveCount(1);
    }

    // -- Totality ------------------------------------------------------------------

    [Fact]
    public void Totality_LiveCellsPlusRosterAreTheAxisProduct()
    {
        SymbolKinds.Should().HaveCount(SymbolKindCount,
            "the kind axis is anchored to its literal");
        Positions.Should().HaveCount(PositionCount,
            "the position axis is anchored to its literal");

        var live = LiveCells().Count;

        (live + NotApplicable.Count).Should().Be(SymbolKindCount * PositionCount,
            "every kind x position cell is either a running row or a rostered N/A with a reason. "
            + "live={0}, rostered={1}", live, NotApplicable.Count);
    }

    [Fact]
    public void Totality_EveryRosterEntryNamesAnAxisPairAndGivesAReason()
    {
        foreach (var (key, reason) in NotApplicable)
        {
            var parts = key.Split('/');
            parts.Should().HaveCount(2, "the roster key is kind/position");
            SymbolKinds.Should().Contain(parts[0], "the roster names a declared kind");
            Positions.Should().Contain(parts[1], "the roster names a declared position");
            reason.Should().NotBeNullOrWhiteSpace("a rostered cell states why it is not live");
        }
    }

    // -- Program construction ------------------------------------------------------

    private static string BuildProgram(string kind, string position)
    {
        var host = kind == "type_parameter"
            ? TypeParameterHost(position)
            : Host(position, Spelling(kind));

        return Declaration(kind) + host + "\ndef main() -> None:\n    pass\n";
    }

    private static string Declaration(string kind) => kind switch
    {
        "class" => "class C:\n    pass\n\n",
        "struct" => "struct C:\n    v: int = 0\n\n",
        "interface" => "interface C:\n    def tick(self) -> None\n\n",
        "enum" => "enum C:\n    RED = 1\n\n",
        "union" => "union C:\n    case Circle(r: float)\n\n",
        "delegate" => "delegate C(v: int) -> None\n\n",
        "alias" => "type C = list[str]\n\n",
        "generic_alias" => "type C[T] = (T) -> None\n\n",
        // The host declares the type parameter; there is no separate declaration statement.
        "type_parameter" => "",
        "clr_import" => "from System.Text import StringBuilder\n\n",
        _ => throw new ArgumentException($"Unknown symbol kind: {kind}")
    };

    /// <summary>How the kind is spelled in an annotation.</summary>
    private static string Spelling(string kind) => kind switch
    {
        "generic_alias" => "C[int]",
        "type_parameter" => "T",
        "clr_import" => "StringBuilder",
        _ => "C"
    };

    /// <summary>
    /// The host program for a position. Each spells the type exactly once: the return host is an
    /// interface method (a body returning a value would spell the type a second time), and the
    /// variable and field hosts are bare annotated declarations for the same reason.
    /// </summary>
    private static string Host(string position, string spelling) => position switch
    {
        "parameter" => $"def use(a: {spelling}) -> None:\n    pass\n",
        "return" => $"interface Maker:\n    def make(self) -> {spelling}\n",
        "variable" => $"def use() -> None:\n    v: {spelling}\n    print(1)\n",
        "field" => $"class Holder:\n    f: {spelling}\n",
        "generic_argument" => $"def use(xs: list[{spelling}]) -> None:\n    pass\n",
        "csharp_nullable" => $"def use(a: {spelling} | None) -> None:\n    pass\n",
        "optional" => $"def use(a: {spelling}?) -> None:\n    pass\n",
        "result" => $"def use(a: {spelling}!str) -> None:\n    pass\n",
        "isinstance_argument" => $"def use(o: object) -> None:\n    if isinstance(o, {spelling}):\n        print(1)\n",
        // The derived class implements `tick` so the same host serves a CLASS base and an INTERFACE
        // base; neither spelling names C a second time.
        "base_class_list" => $"class Sub({spelling}):\n    def tick(self) -> None:\n        pass\n",
        "as_target" => $"def use(o: object) -> None:\n    v = o as? {spelling}\n    print(v is not None)\n",
        _ => throw new ArgumentException(
            $"Position '{position}' has no host: it is rostered N/A and must not be built.")
    };

    /// <summary>
    /// A type parameter needs a generic host, and the host's own declaration (<c>[T]</c>) is not a
    /// reference -- only the annotation is -- so each host spells T exactly once.
    /// </summary>
    private static string TypeParameterHost(string position) => position switch
    {
        "parameter" => "class Box[T]:\n    def m(self, v: T) -> None:\n        pass\n",
        "return" => "interface Maker[T]:\n    def make(self) -> T\n",
        "variable" => "class Box[T]:\n    def m(self) -> None:\n        v: T\n        print(1)\n",
        "field" => "class Box[T]:\n    val: T\n",
        "generic_argument" => "class Box[T]:\n    def m(self, xs: list[T]) -> None:\n        pass\n",
        "csharp_nullable" => "class Box[T]:\n    def m(self, a: T | None) -> None:\n        pass\n",
        "optional" => "class Box[T]:\n    def m(self, a: T?) -> None:\n        pass\n",
        "result" => "class Box[T]:\n    def m(self, a: T!str) -> None:\n        pass\n",
        "isinstance_argument" => "class Box[T]:\n    def m(self, o: object) -> None:\n        if isinstance(o, T):\n            print(1)\n",
        _ => throw new ArgumentException(
            $"Position '{position}' has no type-parameter host: it is rostered N/A.")
    };

    private static int CountReferences(SemanticResult analysis, string kind)
    {
        // TypeParameterDef is not a Symbol, so its references are found by identity rather than
        // by symbol instance -- the same route the LSP takes for a cross-compilation symbol.
        if (kind == "type_parameter")
            return analysis.SemanticInfo!.FindReferencesBySymbolIdentity("T", null).Count;

        Symbol? symbol = kind is "alias" or "generic_alias"
            ? analysis.SymbolTable!.LookupTypeAlias("C")
            : analysis.SymbolTable!.LookupType(kind == "clr_import" ? "StringBuilder" : "C");

        symbol.Should().NotBeNull("the '{0}' declaration must be in the symbol table", kind);
        return analysis.SemanticInfo!.GetReferences(symbol!).Count;
    }
}
