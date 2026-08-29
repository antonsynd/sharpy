using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Conformance sweep for Design Decision 3 of the import-fidelity batch:
/// <b>a module qualifier changes lookup scope, never the produced representation.</b>
/// <c>lib.Shape</c> and a <c>from lib import Shape</c>-bound <c>Shape</c> name one type, so the two
/// spellings of one program must produce the same diagnostics, the same resolved types and the same
/// output.
///
/// <para>They did not, repeatedly, and always on the qualified side only: inherited members
/// invisible through <c>lib.Child</c> (#1366), assignment/argument/constraint relations empty
/// through <c>shapes.Shape</c> (#1407), <c>isinstance</c> silently testing the wrong type (#1410),
/// a bare qualified generic escaping the open-generic guard into CS0305 (#1411). Each was found by
/// someone happening to write the qualified spelling. This sweep writes both, everywhere, on
/// purpose.</para>
///
/// <para><b>Matrix</b> — <c>position × shape × sourceSet</c>, each cell a PAIR of compilations
/// differing only in spelling:</para>
/// <list type="bullet">
///   <item><description><b>position</b> — the type-reference positions: <c>annotation</c>,
///     <c>baseList</c>, <c>typeTest</c> (<c>isinstance</c>), <c>typeTestCast</c> (<c>as?</c>),
///     <c>typeTestExcept</c> (<c>except</c>), <c>constraint</c>, <c>typeAlias</c>,
///     <c>pattern</c>, <c>nested</c> (a three-segment <c>lib.Registry.Entry</c>, #1523),
///     <c>valuePattern</c> (<c>case lib.Color.RED:</c>, #1524), <c>value</c> (a constructor
///     reference, #1182 tiers), <c>call</c> (construction and a type-alias call, #1527), plus
///     <c>declSiteAnnotation</c>, where the spelling varies inside the
///     IMPORTED module rather than
///     at the use site (see below). Type-testing is three positions rather than one because #1411
///     has two routes: <c>isinstance</c> parses its operand as a <c>MemberAccess</c> and goes
///     through <c>ClassifyTypeTestExpressionOperand</c>, while <c>as?</c>/<c>as!</c>,
///     <c>except</c> and match class patterns classify from a <c>TypeAnnotation</c> via
///     <c>ClassifyTypeTestAnnotation</c>. A row covering only the first would report a green
///     position while measuring half of it.</description></item>
///   <item><description><b>shape</b> — <c>plain</c> (a non-generic class) and <c>generic</c> (a
///     generic class at a concrete argument). The generic column exists because the two spellings
///     are made to agree by a deliberate normalization — <c>TypeResolver.cs:745</c> stores the BARE
///     type name on a module-qualified generic so both spellings build the same
///     <c>GenericType</c> (#1134/#1244). That normalization holding is this sweep's contract, not a
///     violation of it; the column is what would notice if it were removed — and the plain column
///     is what noticed that the normalization was never applied to the NON-generic arm beside it
///     (#1446). The same gate now covers the non-generic arm at <c>:281</c>.</description></item>
///   <item><description><b>sourceSet</b> — whether the imported module is itself a compilation
///     unit. <c>inSourceSet</c> resolves through the compilation's own symbols (#1366/#1407);
///     <c>outsideSourceSet</c> resolves through <c>ModuleLoader</c>'s extraction, which is the
///     thinner path and the one where a divergence is expected first.</description></item>
/// </list>
///
/// <para><b>Why <c>declSiteAnnotation</c> is a position of its own.</b>
/// <c>ModuleLoader.ConvertTypeAnnotationToSemanticType</c> (<c>ModuleLoader.cs:723</c>) builds
/// <c>new GenericType { Name = typeAnnotation.Name, … }</c> — the annotation's name VERBATIM, so a
/// module-qualified annotation written inside an extracted module keeps its dotted name, where
/// <c>TypeResolver</c> normalizes the same spelling to the bare one. It is reachable only when the
/// module carrying the annotation is outside the source set, and probing it takes a three-file
/// arrangement — so it is written as an explicit cell rather than left to the matrix to stumble
/// into. It fired on the first run: #1448.</para>
///
/// <para><b>Verdict per cell.</b> The two spellings must agree on (a) the multiset of diagnostic
/// CODES — messages legitimately differ, since they quote the source — (b) the position's
/// structural probe where it has one, and (c) stdout, for cells that compile. Execution runs only
/// for <c>inSourceSet</c> cells: an outside-the-source-set module has no compilation unit, so
/// emission fails on the missing namespace and would drown the semantic answer.</para>
///
/// <para><b>Ratchet.</b> <c>Conformance/qualified-bare-allowlist.txt</c>, per
/// <c>docs/design/gap-discovery-contracts.md</c>: a divergent cell whose key is not listed fails the
/// suite, every listed key cites the issue that will drain it, and a listed cell that has started
/// AGREEING also fails — deleting the line is part of landing the fix.</para>
///
/// <para><b>Baseline and cost.</b> 44 cells, 88 compilations plus the executing subset, ~3 s —
/// cheap enough for the regular suite, so it carries no <c>GapDiscovery</c> trait. Its first runs
/// found fourteen divergent cells and every one was a defect: #1445 (a dotted class pattern does
/// not parse at all), #1446 (a qualified non-generic annotation keeps its dotted name, and outside
/// the source set refuses assignment, <c>isinstance</c>, constraint, <c>as?</c> and <c>except</c> —
/// the last a walk to a BUILTIN base coming back empty), #1447 (a closed qualified generic in
/// type-test position leaks CS0305) and #1448 (the extractor keeps the dotted name — the cell
/// written deliberately to reach <c>ModuleLoader.cs:723</c>).</para>
/// </summary>
public class QualifiedBareSpellingConformanceTests
{
    private readonly ITestOutputHelper _output;

    public QualifiedBareSpellingConformanceTests(ITestOutputHelper output) => _output = output;

    // --- Spellings -----------------------------------------------------------------------------

    /// <summary>
    /// One way of naming <c>lib</c>'s types. The two instances differ ONLY in the import form and
    /// the qualifier; every scenario below is written once against this record so a cell cannot
    /// accidentally compare two different programs.
    /// </summary>
    private sealed record Spelling(string Name, string Module, bool IsQualified)
    {
        /// <summary>
        /// The import block naming exactly the types the cell uses. Generated per cell rather than
        /// fixed, because an unused <c>from lib import Box</c> draws SPY0452 while an unused name
        /// behind <c>import lib</c> does not — a difference about the import statement, not about
        /// the type reference the cell is measuring, which would otherwise mask every real
        /// divergence behind two warnings.
        /// </summary>
        public string Imports(params string[] names)
            => IsQualified
                ? $"import {Module}\n"
                : string.Concat(names.Select(n => $"from {Module} import {n}\n"));

        /// <summary>The cell's way of naming one of the module's types.</summary>
        public string Ref(string name) => IsQualified ? $"{Module}.{name}" : name;
    }

    private static readonly Spelling Qualified = new("qualified", "lib", IsQualified: true);
    private static readonly Spelling Bare = new("bare", "lib", IsQualified: false);

    /// <summary>The declaration-site pair: the same two forms, one module deeper.</summary>
    private static readonly Spelling QualifiedInner = new("qualified", "inner", IsQualified: true);
    private static readonly Spelling BareInner = new("bare", "inner", IsQualified: false);

    private const string LibSource = @"class Shape:
    sides: int

    def __init__(self, sides: int) -> None:
        self.sides = sides


class Circle(Shape):
    def __init__(self) -> None:
        super().__init__(4)


class Box[T]:
    value: T

    def __init__(self, value: T) -> None:
        self.value = value

    def get(self) -> T:
        return self.value


class AppError(Exception):
    def __init__(self, msg: str) -> None:
        super().__init__(msg)


class Registry:
    class Entry:
        tag: int

        def __init__(self, tag: int) -> None:
            self.tag = tag


enum Color:
    RED = 1
    GREEN = 2


type Handle = int
type Pair[T] = tuple[T, T]
";

    // --- Scenarios -----------------------------------------------------------------------------

    private sealed record SourceFile(string Name, string Source);

    /// <summary>
    /// One (position, shape) program, written as a function of the spelling. <c>Probe</c> reads the
    /// representation the position actually produced from the entry file's module scope, so a cell
    /// says more than "neither spelling errored".
    /// </summary>
    private sealed record Scenario(
        string Position,
        string Shape,
        Func<Spelling, IReadOnlyList<SourceFile>> Build,
        Func<Scope, string>? Probe);

    private static readonly Scenario[] Scenarios =
    {
        // ---- annotation ----
        new("annotation", "plain", s => Two($@"{s.Imports("Shape", "Circle")}

def probe(shape: {s.Ref("Shape")}) -> int:
    return shape.sides


def main() -> None:
    print(probe({s.Ref("Circle")}()))
"), scope => ParameterTypeOf(scope, "probe")),

        new("annotation", "generic", s => Two($@"{s.Imports("Box")}

def probe(box: {s.Ref("Box")}[int]) -> int:
    return box.get()


def main() -> None:
    b: {s.Ref("Box")}[int] = {s.Ref("Box")}(7)
    print(probe(b))
"), scope => ParameterTypeOf(scope, "probe")),

        // ---- base list ----
        new("baseList", "plain", s => Two($@"{s.Imports("Shape")}

class Sub({s.Ref("Shape")}):
    def __init__(self) -> None:
        super().__init__(3)


def main() -> None:
    print(Sub().sides)
"), scope => BaseOf(scope, "Sub")),

        new("baseList", "generic", s => Two($@"{s.Imports("Box")}

class Sub({s.Ref("Box")}[int]):
    def __init__(self) -> None:
        super().__init__(5)


def main() -> None:
    print(Sub().get())
"), scope => BaseOf(scope, "Sub")),

        // ---- type test ----
        new("typeTest", "plain", s => Two($@"{s.Imports("Shape", "Circle")}

def main() -> None:
    shape: {s.Ref("Shape")} = {s.Ref("Circle")}()
    if isinstance(shape, {s.Ref("Circle")}):
        print(""circle"")
    else:
        print(""shape"")
"), null),

        new("typeTest", "generic", s => Two($@"{s.Imports("Box")}

def main() -> None:
    box: {s.Ref("Box")}[int] = {s.Ref("Box")}(7)
    if isinstance(box, {s.Ref("Box")}[int]):
        print(""box"")
    else:
        print(""other"")
"), null),

        // ---- type test, annotation-shaped arm ----
        //
        // #1411 turned out to have TWO arms and the isinstance row above covers only one:
        // `isinstance` parses its operand as a MemberAccess and routes through
        // ClassifyTypeTestExpressionOperand -> TryResolveExpressionAsType's QualifiedName arm,
        // while `as?`/`as!`, `except` and match class patterns classify from a TypeAnnotation via
        // ClassifyTypeTestAnnotation. A row that exercised only the first would pass while covering
        // half the position. (The match-pattern spelling is the `pattern` position below, which is
        // where #1445 surfaced.)
        new("typeTestCast", "plain", s => Two($@"{s.Imports("Shape", "Circle")}

def main() -> None:
    shape: {s.Ref("Shape")} = {s.Ref("Circle")}()
    narrowed = shape as? {s.Ref("Circle")}
    if narrowed is not None:
        print(""circle"")
    else:
        print(""shape"")
"), null),

        new("typeTestCast", "generic", s => Two($@"{s.Imports("Box")}

def main() -> None:
    box: {s.Ref("Box")}[int] = {s.Ref("Box")}(7)
    narrowed = box as? {s.Ref("Box")}[int]
    if narrowed is not None:
        print(""box"")
    else:
        print(""other"")
"), null),

        new("typeTestExcept", "plain", s => Two($@"{s.Imports("AppError")}

def main() -> None:
    try:
        raise {s.Ref("AppError")}(""boom"")
    except {s.Ref("AppError")}:
        print(""caught"")
"), null),

        // ---- constraint ----
        new("constraint", "plain", s => Two($@"{s.Imports("Shape", "Circle")}

def describe[T: {s.Ref("Shape")}](shape: T) -> int:
    return shape.sides


def main() -> None:
    print(describe({s.Ref("Circle")}()))
"), null),

        new("constraint", "generic", s => Two($@"{s.Imports("Box")}

def unwrap[T: {s.Ref("Box")}[int]](box: T) -> int:
    return box.get()


def main() -> None:
    b: {s.Ref("Box")}[int] = {s.Ref("Box")}(7)
    print(unwrap(b))
"), null),

        // ---- type alias ----
        //
        // An alias is an export like any other since #1363, but the qualified path had no way to
        // ANSWER with one — both final-segment arms of ResolveQualifiedType require a TypeSymbol —
        // so `lib.Handle` was SPY0202 for a name `from lib import Handle` binds (#1436). Two shapes
        // because the generic arm expands through a different helper (ExpandGenericTypeAlias).
        new("typeAlias", "plain", s => Two($@"{s.Imports("Handle")}

def probe(value: {s.Ref("Handle")}) -> int:
    return value


def main() -> None:
    print(probe(7))
"), scope => ParameterTypeOf(scope, "probe")),

        new("typeAlias", "generic", s => Two($@"{s.Imports("Pair")}

def probe(pair: {s.Ref("Pair")}[int]) -> int:
    return pair[0]


def main() -> None:
    p: {s.Ref("Pair")}[int] = (3, 4)
    print(probe(p))
"), scope => ParameterTypeOf(scope, "probe")),

        // ---- pattern ----
        new("pattern", "plain", s => Two($@"{s.Imports("Shape", "Circle")}

def main() -> None:
    shape: {s.Ref("Shape")} = {s.Ref("Circle")}()
    match shape:
        case {s.Ref("Circle")}():
            print(""circle"")
        case _:
            print(""other"")
"), null),

        new("pattern", "generic", s => Two($@"{s.Imports("Box")}

def main() -> None:
    box: {s.Ref("Box")}[int] = {s.Ref("Box")}(7)
    match box:
        case {s.Ref("Box")}():
            print(""box"")
        case _:
            print(""other"")
"), null),

        // ---- declaration-site annotation (the ModuleLoader.cs:723 probe) ----
        //
        // The spelling varies INSIDE lib.spy, whose annotations are what an extraction converts.
        // main.spy is byte-identical across the pair, so any difference is attributable to the
        // annotation lib itself wrote.
        new("declSiteAnnotation", "plain", s => Three(
            lib: $@"{s.Imports("Shape", "Circle")}

def widen(shape: {s.Ref("Shape")}) -> int:
    return shape.sides


def make() -> {s.Ref("Circle")}:
    return {s.Ref("Circle")}()
",
            main: @"import lib


def main() -> None:
    print(lib.widen(lib.make()))
"), scope => ModuleExportParameterTypeOf(scope, "lib", "widen")),

        new("declSiteAnnotation", "generic", s => Three(
            lib: $@"{s.Imports("Box")}

def unwrap(box: {s.Ref("Box")}[int]) -> int:
    return box.get()


def make() -> {s.Ref("Box")}[int]:
    return {s.Ref("Box")}(9)
",
            main: @"import lib


def main() -> None:
    print(lib.unwrap(lib.make()))
"), scope => ModuleExportParameterTypeOf(scope, "lib", "unwrap")),

        // ---- nested type (the three-segment walk, #1523) ----
        //
        // The qualified spelling appends the module segment in FRONT of an Outer.Inner chain, so
        // the walk has to cross the module/type boundary once (lib -> Registry, then
        // NestedTypes for Entry). The bare spelling starts at the imported Outer.
        new("nested", "plain", s => Two($@"{s.Imports("Registry")}

def main() -> None:
    e: {s.Ref("Registry")}.Entry = {s.Ref("Registry")}.Entry(9)
    print(e.tag)
"), null),

        // ---- value pattern (module-qualified enum member, #1524) ----
        new("valuePattern", "plain", s => Two($@"{s.Imports("Color")}

def main() -> None:
    color: {s.Ref("Color")} = {s.Ref("Color")}.RED
    match color:
        case {s.Ref("Color")}.RED:
            print(""red"")
        case _:
            print(""other"")
"), null),

        // ---- value (constructor reference, the #1182 tiers) ----
        new("value", "plain", s => Two($@"{s.Imports("Circle")}

def main() -> None:
    mk: () -> {s.Ref("Circle")} = {s.Ref("Circle")}
    print(mk().sides)
"), null),

        new("value", "generic", s => Two($@"{s.Imports("Box")}

def main() -> None:
    mk = {s.Ref("Box")}[int]
    print(mk(7).get())
"), null),

        // ---- call (construction, and a type-alias call via #1527's transparency) ----
        new("call", "plain", s => Two($@"{s.Imports("Circle")}

def main() -> None:
    print({s.Ref("Circle")}().sides)
"), null),

        new("call", "generic", s => Two($@"{s.Imports("Box")}

def main() -> None:
    print({s.Ref("Box")}[int](7).get())
"), null),

        new("call", "canonical-alias", s => Two($@"{s.Imports("Handle")}

def main() -> None:
    print({s.Ref("Handle")}(""42""))
"), null),
    };

    private static IReadOnlyList<SourceFile> Two(string main)
        => new[] { new SourceFile("main.spy", main), new SourceFile("lib.spy", LibSource) };

    private static IReadOnlyList<SourceFile> Three(string lib, string main)
        => new[]
        {
            new SourceFile("main.spy", main),
            new SourceFile("lib.spy", lib),
            new SourceFile("inner.spy", LibSource),
        };

    private static readonly string[] SourceSets = { "inSourceSet", "outsideSourceSet" };

    // --- The sweep -----------------------------------------------------------------------------

    [Fact]
    public void QualifiedAndBareSpellings_ProduceTheSameRepresentation()
    {
        var stopwatch = Stopwatch.StartNew();
        var allowlist = LoadAllowlist();
        var results = new List<CellResult>();

        foreach (var scenario in Scenarios)
        {
            var (qualified, bare) = scenario.Position == "declSiteAnnotation"
                ? (QualifiedInner, BareInner)
                : (Qualified, Bare);

            foreach (var sourceSet in SourceSets)
            {
                var key = $"{scenario.Position}::{scenario.Shape}::{sourceSet}";
                var q = Measure(key, scenario, qualified, sourceSet);
                var b = Measure(key, scenario, bare, sourceSet);
                results.Add(Compare(key, q, b));
            }
        }

        stopwatch.Stop();

        var divergent = results.Where(r => r.Divergences.Count > 0).ToList();
        var offenders = divergent.Where(r => !allowlist.Contains(r.Key)).ToList();
        var stale = results
            .Where(r => r.Divergences.Count == 0 && allowlist.Contains(r.Key))
            .Select(r => r.Key)
            .ToList();

        WriteReport(new
        {
            harness = "qualified-bare-spelling",
            contract = "a module qualifier changes lookup scope, never the produced representation "
                + "(Design Decision 3 of the import-fidelity batch; #1146 class)",
            scopeNotes = new[]
            {
                "Each cell is a PAIR of compilations of one program, differing only in spelling.",
                "Agreement is over diagnostic CODES (messages quote the source and legitimately differ), "
                + "the position's structural probe, and stdout.",
                "Execution runs for inSourceSet cells only: an outside-the-source-set module is not a "
                + "compilation unit, so emission fails on the missing namespace.",
                "declSiteAnnotation varies the spelling inside the IMPORTED module — the only place "
                + "ModuleLoader.ConvertTypeAnnotationToSemanticType (ModuleLoader.cs:723) is reachable.",
            },
            elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            totals = new
            {
                cells = results.Count,
                agreeing = results.Count - divergent.Count,
                divergent = divergent.Count,
                allowlistSize = allowlist.Count,
                nonAllowlisted = offenders.Count,
                staleAllowlistEntries = stale.Count,
            },
            cells = results.Select(r => r.ToReport(allowlist)),
        });

        foreach (var r in results)
            _output.WriteLine($"{(r.Divergences.Count == 0 ? "  ok" : "DIFF")}  {r.Key}");
        _output.WriteLine(
            $"cells: {results.Count}  divergent: {divergent.Count}  allowlist: {allowlist.Count}  "
            + $"elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s");

        stale.Should().BeEmpty(
            "an allowlist entry is deleted in the change that fixes its bug, or the ratchet stays "
            + "disarmed on a cell that has started working (docs/design/gap-discovery-contracts.md). "
            + "Delete these lines from Conformance/qualified-bare-allowlist.txt:\n  "
            + string.Join("\n  ", stale));

        offenders.Should().BeEmpty(
            "a module qualifier is a lookup instruction, not a different type. Where the two "
            + "spellings disagree, one of them is wrong — and history says it is the qualified one "
            + "(#1366, #1407, #1410, #1411). Fix the resolution, or file an issue and add the "
            + "printed cell key to Conformance/qualified-bare-allowlist.txt citing it.\n"
            + "Divergent cells:\n"
            + string.Join("\n", offenders.Select(o => o.Describe()))
            + "\nFull report: .claude/tmp/qualified-bare-conformance-report.json");
    }

    // --- Totality: position × shape coverage ----------------------------------------------------

    private static readonly string[] KnownPositions =
    {
        "annotation", "baseList", "typeTest", "typeTestCast", "typeTestExcept",
        "constraint", "typeAlias", "pattern", "declSiteAnnotation", "nested",
        "valuePattern", "value", "call",
    };

    private static readonly string[] KnownShapes =
    {
        "plain", "generic", "canonical-alias", "user-alias",
    };

    /// <summary>
    /// (position::shape) pairs that are structurally impossible or already covered by another
    /// position's alias-specific test. Each entry explains WHY the combination is absent.
    /// </summary>
    private static readonly Dictionary<string, string> NotApplicable = new(StringComparer.Ordinal)
    {
        // --- generic shape: impossible or out-of-scope ---
        ["typeTestExcept::generic"] =
            "test library has no generic exception type; Sharpy exceptions are non-parametric",
        ["nested::generic"] =
            "Registry is not generic; generic-outer + nested-type interaction is orthogonal to the qualified/bare contract",
        ["valuePattern::generic"] =
            "enums are not generic in Sharpy",

        // --- canonical-alias (Handle = int): a non-generic type alias ---
        // The typeAlias position tests alias resolution specifically; in other positions the
        // alias resolves transparently to its target type (int), and the qualified/bare path
        // is the same one the typeAlias position already exercises.
        ["annotation::canonical-alias"] =
            "alias annotation resolution tested by typeAlias::plain (Handle); same TypeResolver path",
        ["baseList::canonical-alias"] =
            "Handle resolves to int — cannot inherit from a primitive",
        ["typeTest::canonical-alias"] =
            "Handle resolves to int — not a valid isinstance target",
        ["typeTestCast::canonical-alias"] =
            "Handle resolves to int — not a valid as? target",
        ["typeTestExcept::canonical-alias"] =
            "Handle resolves to int — not an exception type",
        ["constraint::canonical-alias"] =
            "Handle resolves to int — type parameter bounds require class types",
        ["typeAlias::canonical-alias"] =
            "typeAlias::plain IS Handle (a non-generic alias) — the position already tests this shape",
        ["pattern::canonical-alias"] =
            "Handle resolves to int — not a valid class pattern",
        ["declSiteAnnotation::canonical-alias"] =
            "declSiteAnnotation tests the ModuleLoader extraction path, which is type-shape-agnostic; covered by plain/generic",
        ["nested::canonical-alias"] =
            "type aliases have no nested types",
        ["valuePattern::canonical-alias"] =
            "type aliases are not enums",
        ["value::canonical-alias"] =
            "Handle = int — primitive constructor references use PrimitiveConversionResolver, not the qualified/bare resolution path",
        // call::canonical-alias is a SCENARIO (was call::alias)

        // --- user-alias (Pair[T] = tuple[T, T]): a generic type alias ---
        ["annotation::user-alias"] =
            "alias annotation resolution tested by typeAlias::generic (Pair); same TypeResolver path",
        ["baseList::user-alias"] =
            "Pair resolves to tuple — cannot inherit from a structural type",
        ["typeTest::user-alias"] =
            "Pair resolves to tuple — not a valid isinstance target",
        ["typeTestCast::user-alias"] =
            "Pair resolves to tuple — not a valid as? target",
        ["typeTestExcept::user-alias"] =
            "Pair resolves to tuple — not an exception type",
        ["constraint::user-alias"] =
            "Pair resolves to tuple — type parameter bounds require class types",
        ["typeAlias::user-alias"] =
            "typeAlias::generic IS Pair (a generic alias) — the position already tests this shape",
        ["pattern::user-alias"] =
            "Pair resolves to tuple — not a valid class pattern",
        ["declSiteAnnotation::user-alias"] =
            "declSiteAnnotation tests the ModuleLoader extraction path, which is type-shape-agnostic; covered by plain/generic",
        ["nested::user-alias"] =
            "type aliases have no nested types",
        ["valuePattern::user-alias"] =
            "type aliases are not enums",
        ["value::user-alias"] =
            "Pair resolves to tuple — structural types have no constructor reference",
        ["call::user-alias"] =
            "Pair resolves to tuple — structural types are not directly callable",
    };

    [Fact]
    public void EveryPositionShapePair_IsCoveredOrDeclaredNA()
    {
        var scenarioKeys = Scenarios
            .Select(s => $"{s.Position}::{s.Shape}")
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();
        var covered = 0;
        var na = 0;

        foreach (var position in KnownPositions)
        {
            foreach (var shape in KnownShapes)
            {
                var key = $"{position}::{shape}";
                if (scenarioKeys.Contains(key))
                    covered++;
                else if (NotApplicable.ContainsKey(key))
                    na++;
                else
                    missing.Add(key);
            }
        }

        _output.WriteLine($"Positions: {KnownPositions.Length}  Shapes: {KnownShapes.Length}  "
            + $"Cartesian: {KnownPositions.Length * KnownShapes.Length}");
        _output.WriteLine($"Covered: {covered}  N/A: {na}  Missing: {missing.Count}");

        foreach (var (key, reason) in NotApplicable.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            _output.WriteLine($"  N/A  {key} — {reason}");

        Assert.True(missing.Count == 0,
            $"{missing.Count} (position × shape) pair(s) have no scenario and no N/A declaration — "
            + "add a scenario or declare N/A with a reason in NotApplicable:\n  "
            + string.Join("\n  ", missing));

        Assert.True(scenarioKeys.All(k =>
                KnownPositions.Any(p => k.StartsWith(p + "::", StringComparison.Ordinal))),
            "a scenario references a position not in KnownPositions — add it");
    }

    [Fact]
    public void ConversionResolverCallSites_RestrictedToKnownFiles()
    {
        var compilerDir = FindCompilerSourceDirectory();
        var resolverFile = Path.Combine(compilerDir, "Semantic", "PrimitiveConversionResolver.cs");

        var knownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(compilerDir, "Semantic", "TypeChecker.Expressions.cs"),
            Path.Combine(compilerDir, "Semantic", "TypeChecker.Expressions.Access.Calls.cs"),
        };

        var patterns = new[] { "IsPrimitiveConversion(", "PrimitiveConversionResolver.ResolveOverloads(" };
        var violations = new List<string>();

        foreach (var file in Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(file, resolverFile, StringComparison.OrdinalIgnoreCase))
                continue;

            var content = File.ReadAllText(file);
            foreach (var pattern in patterns)
            {
                if (content.Contains(pattern, StringComparison.Ordinal) && !knownFiles.Contains(file))
                    violations.Add($"{Path.GetFileName(file)}: references {pattern}");
            }
        }

        _output.WriteLine($"Scanned {Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories).Length} files; "
            + $"violations: {violations.Count}");

        Assert.True(violations.Count == 0,
            "PrimitiveConversionResolver call sites must be restricted to TypeChecker.Expressions.cs and "
            + "TypeChecker.Expressions.Access.Calls.cs — a new call site needs a qualified/bare scenario:\n  "
            + string.Join("\n  ", violations));
    }

    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "src", "Sharpy.Compiler");
    }

    // --- Measurement ---------------------------------------------------------------------------

    private sealed record Measurement(
        string Spelling,
        IReadOnlyList<string> Codes,
        string Probe,
        string Stdout,
        string Program);

    private Measurement Measure(string key, Scenario scenario, Spelling spelling, string sourceSet)
    {
        var files = scenario.Build(spelling);
        var root = "QualBare" + Sanitize(key) + spelling.Name;

        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace(root)
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");

        foreach (var file in files)
        {
            // "outsideSourceSet" drops every non-entry module beside the project file, outside the
            // src/**/*.spy glob: importable, never compiled.
            if (sourceSet == "outsideSourceSet" && file.Name != "main.spy")
                File.WriteAllText(Path.Combine(helper.ProjectDirectory, file.Name), file.Source);
            else
                helper.AddSourceFile(file.Name, file.Source);
        }

        helper.CreateProjectFile();
        var config = ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, root + ".spyproj"));

        // Assert the arrangement is the one the cell is named for, so a glob change cannot silently
        // turn every outsideSourceSet cell into a duplicate of its inSourceSet twin.
        var hasLib = config.SourceFiles.Any(f => Path.GetFileName(f) == "lib.spy");
        hasLib.Should().Be(sourceSet == "inSourceSet",
            "cell '{0}' is about lib.spy {1} a compilation unit", key,
            sourceSet == "inSourceSet" ? "being" : "NOT being");

        var analysis = new CompilerApi().AnalyzeProject(config);
        var codes = analysis.Diagnostics.GetAll()
            .Where(d => d.Severity is CompilerDiagnosticSeverity.Error or CompilerDiagnosticSeverity.Warning)
            .Select(d => $"{(d.IsError ? "E" : "W")}:{d.Code ?? "<none>"}")
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var probe = "n/a";
        if (scenario.Probe != null)
        {
            probe = TryEntryScope(analysis) is { } scope
                ? scenario.Probe(scope)
                : "<no entry scope>";
        }

        var stdout = "<not executed>";
        if (sourceSet == "inSourceSet" && analysis.Diagnostics.GetErrors().Count == 0)
        {
            var execution = helper.CompileAndExecute();
            stdout = execution.Success
                ? execution.StandardOutput
                : "<compile failed> " + string.Join("; ", execution.CompilationErrors);
        }

        return new Measurement(spelling.Name, codes, probe, stdout, files[0].Source);
    }

    private static CellResult Compare(string key, Measurement qualified, Measurement bare)
    {
        var divergences = new List<string>();

        if (!qualified.Codes.SequenceEqual(bare.Codes, StringComparer.Ordinal))
        {
            divergences.Add(
                $"diagnostics — qualified: [{string.Join(", ", qualified.Codes)}]  "
                + $"bare: [{string.Join(", ", bare.Codes)}]");
        }

        if (qualified.Probe != bare.Probe)
            divergences.Add($"representation — qualified: {qualified.Probe}  bare: {bare.Probe}");

        if (qualified.Stdout != bare.Stdout)
        {
            divergences.Add(
                $"stdout — qualified: {Escape(qualified.Stdout)}  bare: {Escape(bare.Stdout)}");
        }

        return new CellResult(key, qualified, bare, divergences);
    }

    private sealed record CellResult(
        string Key, Measurement Qualified, Measurement Bare, IReadOnlyList<string> Divergences)
    {
        public string Describe()
            => $"  {Key}\n" + string.Join("\n", Divergences.Select(d => "      " + d));

        public object ToReport(Allowlist allowlist) => new
        {
            key = Key,
            // The entry file of each spelling, for divergent cells only — so the report is enough
            // to reproduce a failure without re-deriving the program from the scenario table.
            programs = Divergences.Count == 0
                ? null
                : new { qualified = Qualified.Program, bare = Bare.Program },
            agrees = Divergences.Count == 0,
            allowlisted = allowlist.Contains(Key),
            divergences = Divergences,
            qualified = new { codes = Qualified.Codes, probe = Qualified.Probe, stdout = Qualified.Stdout },
            bare = new { codes = Bare.Codes, probe = Bare.Probe, stdout = Bare.Stdout },
        };
    }

    // --- Probes --------------------------------------------------------------------------------

    /// <summary>
    /// The first parameter type of a function reached through <c>ModuleSymbol.Exports</c> — i.e.
    /// the type <c>ModuleLoader.ConvertTypeAnnotationToSemanticType</c> built from the annotation
    /// the imported module itself wrote. This is the only reader of <c>ModuleLoader.cs:723</c>, the
    /// one construction site known to keep a DOTTED name where <c>TypeResolver</c> normalizes it,
    /// so the declSiteAnnotation cells assert on it rather than on diagnostics alone (which are
    /// empty on both sides and would make the cell pass for the wrong reason).
    /// </summary>
    private static string ModuleExportParameterTypeOf(Scope scope, string module, string function)
    {
        if (scope.Lookup(module, searchParent: false) is not ModuleSymbol m)
            return "<module unbound>";
        if (!m.Exports.TryGetValue(function, out var symbol) || symbol is not FunctionSymbol f)
            return "<export unresolved>";
        return f.Parameters.Count > 0 ? Describe(f.Parameters[0].Type) : "<no parameters>";
    }

    private static string ParameterTypeOf(Scope scope, string function)
        => scope.Lookup(function, searchParent: false) is FunctionSymbol f && f.Parameters.Count > 0
            ? Describe(f.Parameters[0].Type)
            : "<unresolved>";

    private static string BaseOf(Scope scope, string typeName)
    {
        if (scope.Lookup(typeName, searchParent: false) is not TypeSymbol t)
            return "<unresolved>";

        var args = t.BaseTypeRef?.TypeArgAnnotations ?? default;
        var argText = args.IsDefaultOrEmpty ? "" : "[" + string.Join(",", args.Select(a => a.Name)) + "]";
        return (t.BaseType?.Name ?? t.UnresolvedBaseName ?? "<none>") + argText;
    }

    /// <summary>
    /// A structural description of a resolved type: name and arguments, with no symbol identity.
    /// This is the "produced representation" the contract is about — a dotted name here means the
    /// qualifier survived into the type, which is exactly what must not happen.
    /// </summary>
    private static string Describe(SemanticType? type) => type switch
    {
        null => "<null>",
        BuiltinType b => b.Name,
        GenericType g => $"{g.Name}[{string.Join(",", g.TypeArguments.Select(Describe))}]",
        UserDefinedType u => u.Name,
        NullableType n => $"{Describe(n.UnderlyingType)}?",
        OptionalType o => $"maybe {Describe(o.UnderlyingType)}",
        TupleType t => $"({string.Join(",", t.ElementTypes.Select(Describe))})",
        TypeParameterType p => p.Name,
        VoidType => "None",
        UnknownType => "<unknown>",
        _ => type.GetType().Name,
    };

    private static Scope? TryEntryScope(ProjectAnalysisResult result)
    {
        var table = result.ProjectModel.GlobalSymbols;
        if (table == null)
            return null;

        var unit = result.ProjectModel.Units.Values
            .FirstOrDefault(u => Path.GetFileName(u.FilePath) == "main.spy");
        return unit == null ? null : table.GetModuleScope(unit.ModulePath);
    }

    private static string Sanitize(string key)
        => new string(key.Where(char.IsLetterOrDigit).ToArray());

    private static string Escape(string s) => "\"" + s.Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    // --- allowlist + report I/O ------------------------------------------------------------------

    private static readonly Lazy<string?> AllowlistPath = new(FindAllowlistPath);

    private static Allowlist LoadAllowlist()
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var path = AllowlistPath.Value;
        if (path == null || !File.Exists(path))
        {
            throw new InvalidOperationException(
                "Conformance/qualified-bare-allowlist.txt is missing. Its presence is what arms the "
                + "ratchet; deleting it would silently disarm the sweep rather than relax it.");
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length > 0)
                exact.Add(line);
        }

        return new Allowlist(exact);
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
                return Path.Combine(dir, "qualified-bare-allowlist.txt");
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private void WriteReport(object report)
    {
        var reportDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(QualifiedBareSpellingConformanceTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "qualified-bare-conformance-report.json");
        File.WriteAllText(reportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to: {reportPath}");
    }

    /// <summary>The reviewed conformance allowlist: exact <c>position::shape::sourceSet</c> keys.</summary>
    internal sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        public Allowlist(HashSet<string> exact) => _exact = exact;
        public int Count => _exact.Count;
        public bool Contains(string key) => _exact.Contains(key);
    }
}
