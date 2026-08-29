using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance mirror for the import boundary: <b>a declaration and an import of that same
/// declaration carry every symbol fact identically.</b>
///
/// <para>
/// This generalizes <c>StubClassificationTableTests</c> — which pins one formula, abstractness —
/// to the whole fact surface. The batch that produced it (#1363, #1365, #1366, #1403, #1407) found
/// the same defect shape six times over: <c>NameResolver</c> stamps a fact on a declaration,
/// <c>ModuleLoader</c> re-extracts the same declaration for importers, and the re-extraction is a
/// SEPARATE, THINNER object. Each dropped fact was a user-visible defect on the import path alone —
/// hover with no docs, SPY0466 that never fired, overload dedup comparing a null key,
/// <c>Outer.Inner</c> unresolvable through an import.
/// </para>
///
/// <para><b>Three views, so a divergence is attributable.</b> Each cell holds
/// <c>NameResolver</c>'s own symbol against BOTH import spellings of it:
/// <c>from lib import X</c> and <c>lib.X</c>. A fact missing from both was never extracted
/// (<c>Semantic/ModuleLoader.cs</c>); one missing from the from-import alone was extracted and then
/// dropped by <c>ImportResolver</c>'s re-export clone. Splitting them is also the batch's Design
/// Decision 3 in symbol form — the two spellings must name the same thing — and it is what turned
/// the first run's 145 divergent cells into four filed defects instead of one undifferentiated pile.</para>
///
/// <para><b>Which arrangement.</b> Since #1366/#1407 a module that IS a compilation unit exports the
/// compilation's own symbols, so on that path a type import is identity and cannot drift. The
/// extraction survives for the module that is <em>imported but not compiled</em> — the arrangement
/// <see cref="ImportedStubClassificationReachabilityTests"/> documents — and that is what the fact
/// cells measure. Three controls keep those verdicts from becoming trivial:
/// <see cref="InSourceSetImport_IsTheDeclarationItself"/> asserts the in-source-set identity still
/// holds for types, and since #1491 it holds for functions too — both for the binding
/// (<see cref="InSourceSetFromImportOfAFunction_BindsTheProjectsOwnSymbol"/>) and for the overload
/// list beside it (<see cref="InSourceSetImportOfAnOverloadedFunction_BindsTheProjectsOwnOverloads"/>),
/// which was the second dictionary #1365 measured the extraction through.</para>
///
/// <para><b>Two guards, not one.</b> A fact table that nobody maintains rots into a subset of the
/// real surface, and then the mirror is green because it stopped looking.
/// <see cref="EverySymbolProperty_IsClassified"/> reflects over <see cref="Symbol"/> and its
/// subclasses and fails on any property that is neither a mirrored fact nor an explicitly-reasoned
/// non-fact — so <b>adding a property to Symbol reddens this file until its import behaviour is
/// stated</b>.</para>
///
/// <para><b>The cache is the second reader of this surface.</b> Per the Phase 8 contract, "serializable
/// fact" is defined against <see cref="SymbolSerializer"/>, which already enumerates what a symbol
/// <em>is</em> for the incremental cache. If the two enumerations disagree, the mirror passes while a
/// warm build silently drops facts — the #1309 shape.
/// <see cref="FactTable_AndSymbolSerializer_AgreeOnWhatASymbolIs"/> round-trips every fact through the
/// real serializer and holds it against a declared, issue-cited status, so a divergence has to be
/// written down rather than discovered later.</para>
///
/// <para>Naming and location follow <see cref="ModuleExportsMirrorConformanceTests"/>; the technique
/// deliberately does not — that one is a Roslyn source scan (ownership + no-out-of-seam-mutation),
/// this one is reflection over the type model plus behaviour through a real compilation.</para>
/// </summary>
public class SymbolFactMirrorConformanceTests
{
    private readonly ITestOutputHelper _output;

    public SymbolFactMirrorConformanceTests(ITestOutputHelper output) => _output = output;

    // --- The specimen module -------------------------------------------------------------------
    //
    // One module carrying a non-default value for every fact in the table. A fact whose only
    // specimen leaves it at its default is measured vacuously, so
    // EveryFact_IsExercisedByAtLeastOneSpecimen asserts each fact is really exercised by one.

    private const string LibSource = @"@deprecated(""use Widget2"")
@must_use
class Widget:
    """"""Widget docs.""""""

    @final
    label: str
    count: int = 0

    def __init__(self, label: str) -> None:
        self.label = label

    @deprecated(""use render2"")
    @must_use
    def render(self, `width`: int, scale: str) -> str:
        """"""Render docs.""""""
        return self.label

    class Inner:
        """"""Inner docs.""""""

        tag: int

        def __init__(self, tag: int) -> None:
            self.tag = tag


interface Comparable[T]:
    """"""Comparable docs.""""""

    def compare_to(self, other: T) -> int:
        ...


class Repo(Comparable[int]):
    """"""Repo docs.""""""

    def compare_to(self, other: int) -> int:
        return 0


class Shape:
    sides: int

    def __init__(self, sides: int) -> None:
        self.sides = sides


class Circle(Shape):
    """"""Circle docs.""""""

    def __init__(self) -> None:
        super().__init__(0)


@abstract
class Drawable:
    """"""Drawable docs.""""""

    def draw(self) -> str:
        ...


@deprecated(""use parse2"")
@must_use
def parse(text: str, limit: int) -> int:
    """"""Parse docs.""""""
    return limit


def identity[T](value: T) -> T:
    """"""Identity docs.""""""
    return value


delegate Notify() -> None


enum Level:
    """"""Level docs.""""""

    LOW = ""low""
    HIGH = ""high""


@dataclass
class Point:
    """"""Point docs.""""""

    x: int
    y: int

    def __init__(self, x: int, y: int) -> None:
        self.x = x
        self.y = y


class Registry:
    """"""Registry docs.""""""

    @static
    total: int = 0

    event `changed`: Notify

    property get `size`(self) -> int:
        return 0

    @static
    def reset() -> None:
        pass

    @virtual
    def describe(self) -> str:
        return ""registry""

    @private
    def secret(self) -> int:
        return 1

    def `class`(self) -> int:
        return 2


class SubRegistry(Registry):
    """"""SubRegistry docs.""""""

    @override
    def describe(self) -> str:
        return ""sub""


type Handle = int
type Pair[T] = tuple[T, T]

const LIMIT: int = 200
";

    /// <summary>
    /// The importing half. Every specimen root is named so the outside-the-source-set arrangement
    /// binds an extraction for it; <c>main()</c> exists because the project is an exe.
    /// </summary>
    private const string MainSource = @"import lib
from lib import Widget
from lib import Comparable
from lib import Repo
from lib import Shape
from lib import Circle
from lib import Drawable
from lib import parse
from lib import identity
from lib import Level
from lib import Point
from lib import Registry
from lib import SubRegistry
from lib import Handle
from lib import Pair
from lib import LIMIT


def main() -> None:
    print(""ok"")
";

    // --- Specimens -----------------------------------------------------------------------------

    /// <summary>
    /// The declarations compared, keyed by role. Every specimen comes from a named lookup rather
    /// than from a traversal of "whatever the scope holds", so one that stops being reachable is
    /// visible rather than quietly shrinking the matrix.
    ///
    /// <para>Roots are REQUIRED: a root that does not resolve means the arrangement never bound the
    /// module, and every cell would be vacuous. Members are OPTIONAL and come back null when absent,
    /// because "the import dropped Widget's nested type" is a divergence this mirror exists to
    /// report — throwing on it would turn a finding into a broken test.</para>
    /// </summary>
    private static Dictionary<string, Symbol?> Specimens(string side, Func<string, Symbol?> lookup)
    {
        var widget = Require<TypeSymbol>(side, lookup, "Widget");
        var comparable = Require<TypeSymbol>(side, lookup, "Comparable");
        var repo = Require<TypeSymbol>(side, lookup, "Repo");
        var circle = Require<TypeSymbol>(side, lookup, "Circle");
        var drawable = Require<TypeSymbol>(side, lookup, "Drawable");
        var registry = Require<TypeSymbol>(side, lookup, "Registry");
        var subRegistry = Require<TypeSymbol>(side, lookup, "SubRegistry");
        var level = Require<TypeSymbol>(side, lookup, "Level");
        var point = Require<TypeSymbol>(side, lookup, "Point");

        return new Dictionary<string, Symbol?>(StringComparer.Ordinal)
        {
            ["class"] = widget,
            ["interface"] = comparable,
            ["implementer"] = repo,
            ["subclass"] = circle,
            ["abstractClass"] = drawable,
            ["memberOwner"] = registry,
            ["derivedClass"] = subRegistry,
            ["stringEnum"] = level,
            ["dataclass"] = point,
            ["nestedClass"] = Member(widget.NestedTypes, "Inner"),
            ["method"] = Member(widget.Methods, "render"),
            ["interfaceMethod"] = Member(comparable.Methods, "compare_to"),
            ["abstractMethod"] = Member(drawable.Methods, "draw"),
            ["staticMethod"] = Member(registry.Methods, "reset"),
            ["virtualMethod"] = Member(registry.Methods, "describe"),
            ["overrideMethod"] = Member(subRegistry.Methods, "describe"),
            ["privateMethod"] = Member(registry.Methods, "secret"),
            ["escapedMethod"] = Member(registry.Methods, "class"),
            ["genericFunction"] = lookup("identity") as FunctionSymbol,
            ["constructor"] = widget.Constructors.FirstOrDefault(),
            ["finalField"] = Member(widget.Fields, "label"),
            ["defaultedField"] = Member(widget.Fields, "count"),
            ["staticField"] = Member(registry.Fields, "total"),
            ["enumMember"] = Member(level.Fields, "LOW"),
            ["function"] = lookup("parse") as FunctionSymbol,
            ["typeAlias"] = lookup("Handle") as TypeAliasSymbol,
            ["genericAlias"] = lookup("Pair") as TypeAliasSymbol,
            ["constVar"] = lookup("LIMIT") as VariableSymbol,
        };
    }

    private static T Require<T>(string side, Func<string, Symbol?> lookup, string name) where T : Symbol
    {
        var symbol = lookup(name)
            ?? throw new InvalidOperationException(
                $"arrangement failed [{side}]: '{name}' does not resolve — the specimen module "
                + "never bound it, so every cell for this role would be vacuous");
        return symbol as T
            ?? throw new InvalidOperationException(
                $"arrangement failed [{side}]: '{name}' resolved to {symbol.GetType().Name}, not "
                + typeof(T).Name);
    }

    private static T? Member<T>(IEnumerable<T> members, string name) where T : Symbol
        => members.FirstOrDefault(m => m.Name == name);

    // --- The fact table ------------------------------------------------------------------------

    /// <summary>What a fact's value does when it goes through <see cref="SymbolSerializer"/>.</summary>
    private enum CacheStatus
    {
        /// <summary>The serializer writes and restores it; a warm build sees the cold build's value.</summary>
        RoundTrips,

        /// <summary>The serializer does not carry it. Every entry cites the issue that will drain it.</summary>
        Dropped,
    }

    private sealed record Fact(
        string Name,
        Type Owner,
        string Property,
        Func<Symbol, object?> Read,
        CacheStatus Cache,
        string CacheNote);

    /// <summary>
    /// Every fact an imported symbol must carry, keyed by the property it reads. Reference-valued
    /// facts are read as a structural projection (names, not instances) because the two sides are
    /// necessarily different objects — identity is what the in-source-set control asserts, not this.
    /// </summary>
    private static readonly Fact[] Facts =
    {
        // ---- Symbol ----
        F<Symbol>("Name", s => s.Name, CacheStatus.RoundTrips, "CachedSymbol.Name"),
        F<Symbol>("Kind", s => s.Kind.ToString(), CacheStatus.RoundTrips, "CachedSymbol.Kind"),
        F<Symbol>("IsNameBacktickEscaped", s => s.IsNameBacktickEscaped, CacheStatus.RoundTrips,
            "CachedSymbol.IsNameBacktickEscaped"),
        F<Symbol>("AccessLevel", s => s.AccessLevel.ToString(), CacheStatus.RoundTrips,
            "CachedSymbol.AccessLevel"),
        F<Symbol>("NameDeclarationLine", s => s.NameDeclarationLine, CacheStatus.RoundTrips,
            "CachedSymbol.NameDeclarationLine"),
        F<Symbol>("NameDeclarationColumn", s => s.NameDeclarationColumn, CacheStatus.RoundTrips,
            "CachedSymbol.NameDeclarationColumn"),
        // #1454 — the recorded name EXTENT, not just its start. An import that drops it restores a
        // symbol whose extent silently falls back to the Name.Length derivation the recording
        // exists to retire, so an escaped name would size differently on the import path.
        F<Symbol>("NameDeclarationColumnEnd", s => s.NameDeclarationColumnEnd, CacheStatus.RoundTrips,
            "CachedSymbol.NameDeclarationColumnEnd (#1454)"),
        F<Symbol>("DeclarationSpan", s => s.DeclarationSpan is { } span
                ? $"{span.Start}+{span.Length}" : null, CacheStatus.RoundTrips,
            "CachedSymbol.DeclarationSpanStart/Length"),
        F<Symbol>("Documentation", s => s.Documentation, CacheStatus.RoundTrips,
            "CachedSymbol.Documentation"),
        // Compared as a FILE NAME: the two arrangements necessarily put lib.spy in different temp
        // directories, but "which file declared this" is what LSP go-to-definition, rename and
        // document-highlight all resolve through, so a null here is a feature that does not work.
        F<Symbol>("DeclaringFilePath", s => FileNameOf(s.DeclaringFilePath), CacheStatus.RoundTrips,
            "CachedSymbol.FilePath"),
        F<Symbol>("DeprecationMessage", s => s.DeprecationMessage, CacheStatus.RoundTrips,
            "CachedSymbol.DeprecationMessage — carried on every symbol kind (#1444)"),
        F<Symbol>("ExplicitAccessLevel", s => s.ExplicitAccessLevel?.ToString(), CacheStatus.RoundTrips,
            "CachedSymbol.ExplicitAccessLevel — null when the declaration stated none, which is a "
            + "different fact from any particular level (#1444)"),

        // ---- TypeSymbol ----
        F<TypeSymbol>("TypeKind", t => t.TypeKind.ToString(), CacheStatus.RoundTrips,
            "CachedSymbol.TypeKind"),
        F<TypeSymbol>("IsAbstract", t => t.IsAbstract, CacheStatus.RoundTrips, "CachedSymbol.IsAbstract"),
        F<TypeSymbol>("IsStringEnum", t => t.IsStringEnum, CacheStatus.RoundTrips,
            "CachedSymbol.IsStringEnum"),
        F<TypeSymbol>("DefiningFilePath", t => FileNameOf(t.DefiningFilePath), CacheStatus.RoundTrips,
            "CachedSymbol.FilePath"),
        F<TypeSymbol>("IsMustUse", t => t.IsMustUse, CacheStatus.RoundTrips,
            "CachedSymbol.IsMustUse (#1444)"),
        F<TypeSymbol>("TypeParameters", t => Join(t.TypeParameters.Select(p => p.Name)),
            CacheStatus.RoundTrips, "CachedSymbol.TypeParameters"),
        F<TypeSymbol>("Fields", t => Join(t.Fields.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal)),
            CacheStatus.RoundTrips, "CachedSymbol.Fields"),
        F<TypeSymbol>("Methods", t => Join(t.Methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal)),
            CacheStatus.RoundTrips, "CachedSymbol.Methods"),
        F<TypeSymbol>("Constructors", t => t.Constructors.Count.ToString(CultureInfo.InvariantCulture),
            CacheStatus.RoundTrips, "CachedSymbol.Constructors"),
        F<TypeSymbol>("NestedTypes", t => Join(t.NestedTypes.Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal)),
            CacheStatus.RoundTrips, "CachedSymbol.NestedTypes"),
        // #1455 — the projection includes each property's escape flag: PropertySymbol is a standalone
        // record, so its IsNameBacktickEscaped must survive extraction (else a `Zed` property's
        // spelling is lost on the import path). The `Registry` specimen carries an escaped property.
        F<TypeSymbol>("Properties",
            t => Join(t.Properties.Select(p => $"{p.Name}{(p.IsNameBacktickEscaped ? ":esc" : "")}").OrderBy(n => n, StringComparer.Ordinal)),
            CacheStatus.RoundTrips, "CachedSymbol.TypeProperties — the escape flag travels with each "
            + "entry, so #1455's spelling survives the cache as well as the import path (#1444)"),
        // #1455 — the projection includes each event's escape flag: EventSymbol is a standalone
        // record, so its IsNameBacktickEscaped must survive extraction. `Registry` carries an escaped
        // event.
        F<TypeSymbol>("Events",
            t => Join(t.Events.Select(e => $"{e.Name}{(e.IsNameBacktickEscaped ? ":esc" : "")}").OrderBy(n => n, StringComparer.Ordinal)),
            CacheStatus.RoundTrips, "CachedSymbol.Events — escape flag included, as for properties (#1444)"),
        F<TypeSymbol>("DeclaringType", t => t.DeclaringType?.Name, CacheStatus.RoundTrips,
            "re-linked from the NestedTypes nesting on restore"),
        F<TypeSymbol>("IsDataclass", t => t.IsDataclass, CacheStatus.RoundTrips,
            "CachedSymbol.IsDataclass (#1444)"),

        // ---- FunctionSymbol ----
        // #1455 — the projection includes each parameter's escape flag: ParameterSymbol is a
        // standalone record, so its IsNameBacktickEscaped must survive extraction (else a `Zed`
        // parameter's spelling is lost across forwarders on the import path) and the incremental
        // cache (CachedParameter.IsNameBacktickEscaped). The `render` specimen carries an escaped
        // parameter so this is exercised non-vacuously.
        F<FunctionSymbol>("Parameters",
            f => Join(f.Parameters.Select(p => $"{p.Name}:{Describe(p.Type)}{(p.IsNameBacktickEscaped ? ":esc" : "")}")),
            CacheStatus.RoundTrips, "CachedSymbol.Parameters"),
        F<FunctionSymbol>("ReturnType", f => Describe(f.ReturnType), CacheStatus.RoundTrips,
            "CachedSymbol.ReturnTypeId"),
        F<FunctionSymbol>("IsStatic", f => f.IsStatic, CacheStatus.RoundTrips, "CachedSymbol.IsStatic"),
        F<FunctionSymbol>("IsAbstract", f => f.IsAbstract, CacheStatus.RoundTrips, "CachedSymbol.IsAbstract"),
        F<FunctionSymbol>("IsVirtual", f => f.IsVirtual, CacheStatus.RoundTrips, "CachedSymbol.IsVirtual"),
        F<FunctionSymbol>("IsOverride", f => f.IsOverride, CacheStatus.RoundTrips, "CachedSymbol.IsOverride"),
        F<FunctionSymbol>("TypeParameters", f => Join(f.TypeParameters.Select(p => p.Name)),
            CacheStatus.RoundTrips, "CachedSymbol.TypeParameters"),
        F<FunctionSymbol>("SignatureKey", f => f.SignatureKey, CacheStatus.RoundTrips,
            "CachedSymbol.SignatureKey — restored rather than recomputed, so overload dedup compares "
            + "two keys made the same way (#1444)"),
        F<FunctionSymbol>("IsMustUse", f => f.IsMustUse, CacheStatus.RoundTrips,
            "CachedSymbol.IsMustUse (#1444)"),

        // ---- VariableSymbol ----
        F<VariableSymbol>("Type", v => Describe(v.Type), CacheStatus.RoundTrips, "CachedSymbol.TypeId"),
        F<VariableSymbol>("IsConstant", v => v.IsConstant, CacheStatus.RoundTrips,
            "CachedSymbol.Properties[IsConstant]"),
        F<VariableSymbol>("HasDefaultValue", v => v.HasDefaultValue, CacheStatus.RoundTrips,
            "CachedSymbol.Properties[HasDefaultValue]"),
        F<VariableSymbol>("IsStatic", v => v.IsStatic, CacheStatus.RoundTrips,
            "CachedSymbol.Properties[IsStatic] — the bag carries eight field flags now (#1444)"),
        F<VariableSymbol>("IsFinal", v => v.IsFinal, CacheStatus.RoundTrips,
            "CachedSymbol.Properties[IsFinal] (#1444)"),
        F<VariableSymbol>("ConstantValue", v => v.ConstantValue?.ToString(),
            CacheStatus.RoundTrips,
            "CachedSymbol.ConstantValue — BigInteger? serialized as invariant-culture string (#1460)"),

        // ---- TypeAliasSymbol ----
        F<TypeAliasSymbol>("TypeAnnotation", a => a.TypeAnnotation?.Name, CacheStatus.Dropped,
            "deliberate — SymbolSerializer states the alias target is reconstructed from source on "
            + "reparse rather than cached (SymbolSerializer.cs:266)"),
        F<TypeAliasSymbol>("TypeParameters", a => Join(a.TypeParameters.Select(p => p.Name)),
            CacheStatus.Dropped, "deliberate — see TypeAnnotation"),
    };

    private static Fact F<T>(string property, Func<T, object?> read, CacheStatus cache, string note)
        where T : Symbol
        => new($"{typeof(T).Name}.{property}", typeof(T), property, s => read((T)s), cache, note);

    /// <summary>
    /// Properties on <see cref="Symbol"/> and its subclasses that are deliberately NOT mirrored
    /// facts, each with the reason. The census fails on anything neither here nor in
    /// <see cref="Facts"/>, so a new property cannot be added without answering "does an import
    /// carry this?".
    /// </summary>
    private static readonly Dictionary<string, string> NotAFact = new(StringComparer.Ordinal)
    {
        // Provenance: these name the VIEWER's relationship to the declaration, so they are
        // supposed to read differently from the declaring module and from an importing one.
        ["Symbol.IsReExport"] = "provenance of the BINDING — true by construction for anything "
            + "reached through an import, false on the declaration; the difference is the fact",
        ["Symbol.DeclarationLine"] = "provenance of the BINDING — CreateReExportSymbol deliberately "
            + "restamps it to the import statement's position, which is where a diagnostic about "
            + "the import belongs. The name position, which Symbol.EffectiveNameLine prefers and "
            + "every LSP text edit reads, is NameDeclarationLine — and that IS mirrored",
        ["Symbol.DeclarationColumn"] = "see Symbol.DeclarationLine",
        ["Symbol.IsErrorRecovery"] = "produced only by import ERROR recovery, which by construction "
            + "has no declaration on the other side to mirror",
        ["Symbol.OriginalModule"] = "provenance — meaningful only for a re-export, and it names the "
            + "module the re-export came FROM, which the declaration itself has no notion of",
        ["TypeSymbol.DefiningModule"] = "provenance — null for a type in the current module and the "
            + "module name for an imported one; that difference IS the fact, not a violation",
        ["Symbol.BuiltinAliasOf"] = "provenance of the BINDING — set only on the clone a "
            + "`from builtins import len as blen` binds, to say what that spelling DISPATCHES as "
            + "(#1383). It is null by construction on every declaration, because the thing it "
            + "points at lives in BuiltinRegistry rather than in any .spy module. Nor can it reach "
            + "an importer: an aliased builtin binding is file-local, measured — a second module "
            + "spelling `from lib import blen` is refused with SPY0301 'Module lib has no exported "
            + "symbol blen', so there is no imported view to mirror",
        ["Symbol.OriginSymbol"] = "provenance of the BINDING — set by CreateReExportSymbol and "
            + "CloneSymbolWithName to trace back to the original declaration for identity-based "
            + "shadow dispatch (#1525). Null by construction on every declaration; set only on "
            + "import-boundary clones, which are file-local bindings",

        // Later-phase products, not declaration facts. An extraction is produced in Pass 1.5 and
        // nothing runs these passes over it, by design.
        ["TypeSymbol.BaseType"] = "materialized by InheritanceResolver, which runs over whichever "
            + "object the compilation reaches; compared structurally by the BaseType cells in "
            + "CrossModuleInheritanceTests and pinned by the #1366/#1407 fixtures",
        ["TypeSymbol.BaseTypeRef"] = "see TypeSymbol.BaseType",
        ["TypeSymbol.Interfaces"] = "see TypeSymbol.BaseType; the written arguments are pinned by "
            + "the #1403 cells in InheritanceResolverTests and ModuleLoaderTests",
        ["TypeSymbol.UnresolvedBaseName"] = "the INPUT to inheritance resolution, present only on "
            + "the side that has not resolved it yet — an extraction has it, the declaration does not",
        ["TypeSymbol.UnresolvedBaseTypeArgs"] = "see TypeSymbol.UnresolvedBaseName",
        ["TypeSymbol.UnresolvedInterfaces"] = "see TypeSymbol.UnresolvedBaseName",
        ["TypeSymbol.DataclassInfo"] = "set by the TypeChecker's dataclass pass, which runs for the "
            + "declaring compilation only",
        ["TypeSymbol.DataclassFields"] = "see TypeSymbol.DataclassInfo",
        ["FunctionSymbol.IsGenerator"] = "set by TypeChecker.Definitions, not by name resolution, so "
            + "neither side of this comparison has run the pass that computes it",
        ["FunctionSymbol.IsAsync"] = "see FunctionSymbol.IsGenerator",
        ["FunctionSymbol.IsCached"] = "see FunctionSymbol.IsGenerator",
        ["FunctionSymbol.CacheMaxSize"] = "see FunctionSymbol.IsGenerator",
        ["TypeSymbol.IsSourceGenerator"] = "detected during inheritance resolution, which runs over "
            + "whichever object the compilation reaches — see TypeSymbol.BaseType",
        ["TypeSymbol.UnionCases"] = "a union is not an export shape: ModuleLoader's top-level "
            + "switch has no UnionDef arm at all, so there is no imported union to compare against",
        ["VariableSymbol.IsParameter"] = "true only for a function-scope parameter binding, which "
            + "is never a module export or a type member",
        ["VariableSymbol.ParameterModifier"] = "see VariableSymbol.IsParameter — a parameter's "
            + "ref/out/in modifier, always None on an exported variable or a type's field",
        ["VariableSymbol.HasPropertyGetter"] = "merged onto the symbol during name resolution of a "
            + "module-level `property get`; module-level properties are not an export shape",
        ["VariableSymbol.HasPropertySetter"] = "see VariableSymbol.HasPropertyGetter",
        ["VariableSymbol.IsModuleProperty"] = "see VariableSymbol.HasPropertyGetter",

        // Derived — no independent value to drop.
        ["Symbol.EffectiveNameLine"] = "derived from NameDeclarationLine ?? DeclarationLine, both mirrored",
        ["Symbol.EffectiveNameColumn"] = "derived from NameDeclarationColumn ?? DeclarationColumn, both mirrored",
        ["Symbol.EffectiveNameColumnEnd"] = "derived from NameDeclarationColumnEnd, which is mirrored, "
            + "falling back to EffectiveNameColumn + Name.Length + the backtick pair for symbols with "
            + "no parsed node — every input to that fallback is itself mirrored (#1454)",
        ["TypeSymbol.IsGeneric"] = "derived from TypeParameters.Count, which is mirrored",
        ["FunctionSymbol.IsGeneric"] = "derived from TypeParameters.Count, which is mirrored",

        // CLR interop: never produced by the .spy extraction path this mirror measures.
        ["TypeSymbol.ClrType"] = "CLR interop — set by assembly discovery, never by .spy extraction",
        ["FunctionSymbol.ClrMethod"] = "CLR interop — see TypeSymbol.ClrType",
        ["FunctionSymbol.ClrMethodName"] = "CLR interop — see TypeSymbol.ClrType",
        ["VariableSymbol.ClrFieldName"] = "CLR interop — set by assembly discovery (#1540), never by .spy extraction",
        ["TypeSymbol.ClrArityGroup"] = "CLR interop — set by GetNamespaceTypes (#1613) to carry the multi-arity "
            + "name group (EventHandler / EventHandler`1 / EventHandler`2), never by .spy extraction",

        // Overload/dunder indexes: derived views over Methods, which is mirrored.
        ["TypeSymbol.OperatorMethods"] = "an index over Methods, rebuilt by "
            + "TypeSymbol.BuildMethodOverloads rather than carried",
        ["TypeSymbol.ProtocolMethods"] = "see TypeSymbol.OperatorMethods",
        ["TypeSymbol.MethodOverloads"] = "see TypeSymbol.OperatorMethods",

        ["TypeAliasSymbol.FunctionType"] = "the alias target's function shape; travels with "
            + "TypeAnnotation, which is mirrored",
    };

    /// <summary>
    /// Symbol subclasses the mirror does not enumerate, with the reason. Keeps the census honest
    /// about its own scope rather than silently covering three of six subclasses.
    /// </summary>
    private static readonly Dictionary<Type, string> OutOfScopeSubclasses = new()
    {
        [typeof(ModuleSymbol)] = "the import boundary itself, not something carried across it — its "
            + "Exports seam is guarded by ModuleExportsMirrorConformanceTests",
        [typeof(TypeParameterSymbol)] = "never an export; a type parameter is reached through its "
            + "declaring symbol's TypeParameters, which IS mirrored",
    };

    /// <summary>
    /// Cells known to disagree, each citing the issue that will drain it — the ratchet ledger, in
    /// code rather than in a file because the keys are only meaningful against the fact table above.
    /// An entry here is a recorded defect, not a waiver: the mirror still measures the cell and
    /// fails if a listed cell has started AGREEING (drain on fix, per
    /// <c>docs/design/gap-discovery-contracts.md</c>), so deleting the line is part of landing the
    /// fix rather than a follow-up nobody schedules.
    ///
    /// <para>The mirror's first measured run recorded 145 cells across four defects. All four are
    /// gone and their 145 cells drained with the fixes:</para>
    /// <list type="bullet">
    ///   <item><description><b>#1440</b> (23 cells, drained) — <c>CreateReExportedTypeSymbol</c> was a
    ///     hand-built <see cref="TypeSymbol"/> clone where the function arm beside it used a
    ///     <c>with</c> expression, so every fact it did not restate was lost — <c>NestedTypes</c>
    ///     included, silently un-doing #1363 on the from-import spelling. Both arms are
    ///     <c>with</c> expressions now, so the drop is unrepresentable rather than fixed.</description></item>
    ///   <item><description><b>#1441</b> (114 cells, drained) — <c>ModuleLoader</c>'s extraction
    ///     stamped no <c>DeclarationSpan</c> and no file path on a member, dropped a method's
    ///     backtick escape and explicit access level, and typed <c>self</c> as Unknown. It now reads
    ///     the declaring pass's own classification helpers.</description></item>
    ///   <item><description><b>#1442</b> (6 cells, drained) — the extraction computed neither
    ///     <c>IsStringEnum</c> nor <c>IsDataclass</c>, so an imported string enum was checked as an
    ///     int enum and an imported <c>@dataclass</c> arrived without its synthesized members. Both
    ///     now come from the predicate/authority the declaring pass uses.</description></item>
    ///   <item><description><b>#1443</b> (2 cells, drained) — inverted: <c>@must_use</c> on a METHOD
    ///     was honoured by the extraction and ignored by <c>NameResolver</c>, so it never warned
    ///     same-file. The defect was on the declaration side; <c>ResolveMethodDeclaration</c> now
    ///     stamps <c>IsMustUse</c> like its function twin, so the two views agree.</description></item>
    /// </list>
    /// </summary>
    private static readonly Dictionary<string, string> KnownDivergences = new(StringComparer.Ordinal)
    {
        // Empty: every recorded divergence has drained on its fix. A new entry here is a recorded
        // defect awaiting a fix, citing its issue; it must be deleted in the change that fixes it.
    };

    // --- The mirror ----------------------------------------------------------------------------

    /// <summary>
    /// The mirror proper. Every (specimen, fact) cell must read the same value from the declaration
    /// and from the import. Runs as one test over the whole matrix rather than a Theory per cell so
    /// the failure text is the complete divergence list — a fact dropped from the extraction usually
    /// drops it for several roles at once, and one cell per line hides that shape.
    /// </summary>
    [Fact]
    public void FactMirror_AgreesForEveryCell()
    {
        var views = Measure();

        var divergences = new List<string>();
        var stale = new List<string>();
        var cellCount = 0;

        foreach (var via in new[] { "fromImport", "qualifiedImport" })
        {
            var imported = via == "fromImport" ? views.FromImport : views.QualifiedImport;

            foreach (var (role, declaredSymbol) in views.Declared.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var importedSymbol = imported[role];

                // A specimen present on one side and absent on the other is the coarsest divergence
                // there is: the import dropped a whole declaration. Recorded on EVERY pass, not only
                // when one side is missing — an entry the ratchet can never see agree is an entry the
                // ratchet can never call stale, and `nestedClass::<present>::fromImport` sat in
                // KnownDivergences un-drainable for exactly that reason once #1440 cured it.
                cellCount++;
                Record($"{role}::<present>::{via}",
                    declaredSymbol != null ? "present" : "ABSENT",
                    importedSymbol != null ? "present" : "ABSENT");

                if (declaredSymbol == null || importedSymbol == null)
                    continue;

                foreach (var fact in Facts.Where(f => f.Owner.IsInstanceOfType(declaredSymbol)))
                {
                    cellCount++;
                    Record($"{role}::{fact.Name}::{via}",
                        Render(fact.Read(declaredSymbol)),
                        Render(fact.Read(importedSymbol)));
                }
            }
        }

        void Record(string key, string declared, string imported)
        {
            if (declared == imported)
            {
                if (KnownDivergences.ContainsKey(key))
                    stale.Add($"{key} — now agrees on {declared}; delete the KnownDivergences entry");
                return;
            }

            if (KnownDivergences.ContainsKey(key))
                return;

            divergences.Add($"{key}\n      declared: {declared}\n      imported: {imported}");
        }

        _output.WriteLine(
            $"cells: {cellCount}  roles: {views.Declared.Count}  facts: {Facts.Length}  "
            + $"views: 2  knownDivergences: {KnownDivergences.Count}");

        stale.Should().BeEmpty(
            "a KnownDivergences entry is a recorded defect that must be deleted in the change that "
            + "fixes it, or the ratchet stays disarmed on a cell that has started working "
            + "(docs/design/gap-discovery-contracts.md).\nStale:\n  " + string.Join("\n  ", stale));

        divergences.Should().BeEmpty(
            "an imported symbol IS the declaration, seen from another module. A fact the import path "
            + "drops is a defect on that path alone — that is how #1363, #1365, #1403 and #1366 each "
            + "arrived. A cell that fails for `qualifiedImport` was never extracted (fix "
            + "Semantic/ModuleLoader.cs); one that fails for `fromImport` only was extracted and then "
            + "dropped by the re-export clone (fix Semantic/ImportResolver.Symbols.cs). Otherwise "
            + "file an issue and add the printed cell key to KnownDivergences citing it.\n"
            + "Divergent cells:\n  " + string.Join("\n  ", divergences));
    }

    /// <summary>
    /// The mirror's own control. On the in-source-set path the two symbols must be ONE object
    /// (#1366/#1407), which is a strictly stronger statement than every fact agreeing — and which
    /// tells a reader that the comparison above is measuring the extraction path deliberately, not
    /// by accident.
    /// </summary>
    [Fact]
    public void InSourceSetImport_IsTheDeclarationItself()
    {
        var result = Arrange.LibInSourceSet(_output, LibSource, MainSource);
        var declared = Arrange.ScopeOf(result, "lib.spy");
        var importing = Arrange.ScopeOf(result, "main.spy");

        foreach (var name in new[] { "Widget", "Comparable", "Repo", "Shape", "Circle", "Drawable" })
        {
            importing.Lookup(name, searchParent: false).Should().BeSameAs(
                declared.Lookup(name, searchParent: false),
                "a module that is a compilation unit exports THIS compilation's symbols, so "
                + "'{0}' names one object on both sides (#1366/#1407). If this ever stops holding, "
                + "the fact-by-fact comparison becomes the only guard and its 'agrees' verdicts "
                + "would be about two objects again", name);
        }
    }

    /// <summary>
    /// Functions join types on the identity path (#1491). Two seams had to hold for this: the
    /// binding must be the compilation's own symbol (<c>ResolveImportSymbol</c> already preferred
    /// it) AND the type checker must write the checked return type ONTO that symbol rather than
    /// into a <c>with</c>-clone — the clone replaced <c>lib</c>'s binding in Phase 5, after
    /// <c>main</c>'s import had bound the original in Phase 4, so both sides held the compilation's
    /// own symbol and they were still two objects.
    ///
    /// <para>Mutation test for the second seam: restore the clone in
    /// <c>TypeChecker.Definitions.UpdateFunctionSymbol</c> (<c>functionSymbol with { ReturnType }</c>
    /// plus <c>SymbolTable.UpdateSymbol</c>) and this reddens.</para>
    ///
    /// <para>The overload channel is the other half and is not visible from here — a scope holds
    /// only the first overload under the name — so it is pinned by
    /// <see cref="InSourceSetImportOfAnOverloadedFunction_BindsTheProjectsOwnOverloads"/>.</para>
    /// </summary>
    [Fact]
    public void InSourceSetFromImportOfAFunction_BindsTheProjectsOwnSymbol()
    {
        var result = Arrange.LibInSourceSet(_output, LibSource, MainSource);
        var declared = Arrange.ScopeOf(result, "lib.spy").Lookup("parse", searchParent: false);
        var bound = Arrange.ScopeOf(result, "main.spy").Lookup("parse", searchParent: false);

        declared.Should().BeOfType<FunctionSymbol>("the arrangement must have declared 'parse'");
        bound.Should().BeOfType<FunctionSymbol>("`from lib import parse` must bind something");
        bound.Should().BeSameAs(
            declared,
            "a module that is a compilation unit exports THIS compilation's symbols, and a function "
            + "is no exception: 'parse' names one object on both sides, so no fact about it can "
            + "diverge across the import (#1491, the function half of #1366/#1407)");
    }

    /// <summary>
    /// The overload channel's identity. <c>ModuleSymbol.FunctionOverloads</c> and the scope's
    /// overload table are a SECOND dictionary carrying the same declarations, and re-pointing the
    /// single-symbol channel left them holding <c>ModuleLoader</c>'s extraction — which is what
    /// every ranked call to an imported overloaded function resolved against (#1491).
    ///
    /// <para>Mutation test: re-point either registration site back at
    /// <c>moduleInfo.FunctionOverloads</c> and this reddens.</para>
    /// </summary>
    [Fact]
    public void InSourceSetImportOfAnOverloadedFunction_BindsTheProjectsOwnOverloads()
    {
        const string overloadedLib = @"def widen(value: int) -> int:
    return value


def widen(value: str) -> str:
    return value
";
        const string overloadedMain = @"import lib
from lib import widen


def main() -> None:
    print(widen(1))
";

        var result = Arrange.LibInSourceSet(_output, overloadedLib, overloadedMain);
        var declaredScope = Arrange.ScopeOf(result, "lib.spy");
        var declared = declaredScope.LookupFunctionOverloads("widen", searchParent: false)
            ?? throw new InvalidOperationException(
                "arrangement failed: lib.spy declares two 'widen' overloads, so its module scope "
                + "must hold an overload list — without one this test measures nothing");
        declared.Should().HaveCount(2, "the arrangement declares two overloads");

        var importedScope = Arrange.ScopeOf(result, "main.spy");
        var fromImport = importedScope.LookupFunctionOverloads("widen", searchParent: false)
            ?? throw new InvalidOperationException(
                "arrangement failed: `from lib import widen` registered no overload list");
        fromImport.Should().Equal(declared,
            "the from-imported overload set IS the declaration's, element for element — Symbol "
            + "compares by reference, so this is an identity assertion (#1491)");

        var module = importedScope.Lookup("lib", searchParent: false) as ModuleSymbol
            ?? throw new InvalidOperationException("arrangement failed: `import lib` bound no module");
        module.FunctionOverloads.TryGetValue("widen", out var qualified).Should().BeTrue(
            "the qualified spelling resolves `lib.widen(...)` through ModuleSymbol.FunctionOverloads");
        qualified.Should().Equal(declared,
            "the qualified spelling's overload dictionary is the same second channel and gets the "
            + "same ownership rule (#1491)");
    }

    /// <summary>
    /// Anti-vacuity. A boolean fact whose every specimen leaves it <c>false</c>, or a string fact
    /// every specimen leaves null, is a cell that agrees for the same reason an empty comparison
    /// agrees. Each fact must be exercised by at least one specimen.
    /// </summary>
    [Fact]
    public void EveryFact_IsExercisedByAtLeastOneSpecimen()
    {
        var declared = Measure().Declared;

        var unexercised = new List<string>();
        foreach (var fact in Facts)
        {
            var applicable = declared.Values
                .Where(s => s != null && fact.Owner.IsInstanceOfType(s))
                .Select(s => s!)
                .ToList();
            if (applicable.Count == 0)
            {
                unexercised.Add($"{fact.Name} — no specimen is a {fact.Owner.Name}");
                continue;
            }

            if (!applicable.Any(s => IsNonDefault(fact.Read(s))))
                unexercised.Add($"{fact.Name} — every specimen reads the default ({Render(fact.Read(applicable[0]))})");
        }

        unexercised.Should().BeEmpty(
            "a fact no specimen carries a real value for is measured vacuously: the mirror would "
            + "stay green with the fact deleted from the extraction entirely. Extend LibSource so "
            + "some declaration exercises it.\nUnexercised:\n  " + string.Join("\n  ", unexercised));
    }

    /// <summary>
    /// The Phase 8 constraint that decides whether this guard is real: "serializable fact" is
    /// defined against <see cref="SymbolSerializer"/>, so the mirror and the incremental cache
    /// cannot disagree about what a symbol IS. Measured by round-tripping the real declaration
    /// symbols through the real serializer, not by reading its source.
    ///
    /// <para>A fact declared <see cref="CacheStatus.RoundTrips"/> that does not survive is a live
    /// cache bug; a fact declared <see cref="CacheStatus.Dropped"/> that DOES survive means the gap
    /// was closed and the note must go. Both fail.</para>
    /// </summary>
    [Fact]
    public void FactTable_AndSymbolSerializer_AgreeOnWhatASymbolIs()
    {
        var declared = Measure().Declared;

        var wrong = new List<string>();
        foreach (var (role, symbol) in declared.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (symbol == null || !CanSerialize(symbol))
                continue;

            var restored = RoundTrip(symbol);

            foreach (var fact in Facts.Where(f => f.Owner.IsInstanceOfType(symbol)))
            {
                var before = Render(fact.Read(symbol));
                // Only a fact the specimen actually carries can tell the two statuses apart.
                if (!IsNonDefault(fact.Read(symbol)))
                    continue;

                var after = Render(fact.Read(restored));
                var survived = before == after;
                var expected = fact.Cache == CacheStatus.RoundTrips;

                if (survived == expected)
                    continue;

                wrong.Add(survived
                    ? $"{role}::{fact.Name} — declared Dropped ({fact.CacheNote}) but the value "
                      + $"survived the round trip as {after}; delete the note"
                    : $"{role}::{fact.Name} — declared RoundTrips ({fact.CacheNote}) but "
                      + $"'{before}' came back as '{after}'");
            }
        }

        wrong.Should().BeEmpty(
            "the mirror's fact surface and SymbolSerializer's must describe the same symbol. Where "
            + "they differ the mirror passes while a warm --incremental build silently drops the "
            + "fact — the #1309 shape. Either thread the fact through Project/SymbolSerializer.cs, "
            + "or record it as Dropped with the issue that will fix it.\nDisagreements:\n  "
            + string.Join("\n  ", wrong));
    }

    /// <summary>
    /// The completeness guard. Every public instance property on <see cref="Symbol"/> and the
    /// subclasses in scope is either a mirrored fact or an explicitly-reasoned non-fact — so adding
    /// a property to the symbol model reddens this file until someone states whether an import
    /// carries it. This is the mechanism that keeps the table from rotting into a subset of the real
    /// surface while still reporting green.
    /// </summary>
    [Fact]
    public void EverySymbolProperty_IsClassified()
    {
        var covered = new HashSet<string>(Facts.Select(f => f.Name), StringComparer.Ordinal);
        var unclassified = new List<string>();
        var stale = new List<string>();

        var inScope = new[]
        {
            typeof(Symbol), typeof(TypeSymbol), typeof(FunctionSymbol),
            typeof(VariableSymbol), typeof(TypeAliasSymbol),
        };

        var declaredEverywhere = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in inScope)
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                // Records synthesize EqualityContract; it is not part of the model.
                if (property.Name == "EqualityContract")
                    continue;

                var key = $"{type.Name}.{property.Name}";
                declaredEverywhere.Add(key);
                if (covered.Contains(key) || NotAFact.ContainsKey(key))
                    continue;

                unclassified.Add($"{key} ({property.PropertyType.Name})");
            }
        }

        foreach (var key in NotAFact.Keys.Concat(covered))
        {
            if (!declaredEverywhere.Contains(key))
                stale.Add(key);
        }

        // The subclasses the census does not walk must say so, so "in scope" is a decision rather
        // than an omission.
        var allSubclasses = typeof(Symbol).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Symbol)) && !t.IsAbstract)
            .ToList();
        var unaccounted = allSubclasses
            .Where(t => !inScope.Contains(t) && !OutOfScopeSubclasses.ContainsKey(t))
            .Select(t => t.Name)
            .ToList();

        unaccounted.Should().BeEmpty(
            "a new Symbol subclass is either mirrored or explicitly out of scope with a reason — "
            + "silently unwalked is how a whole category stops being measured. Add it to inScope or "
            + "to OutOfScopeSubclasses.\nUnaccounted:\n  " + string.Join("\n  ", unaccounted));

        stale.Should().BeEmpty(
            "a classified property that no longer exists means the table is describing a symbol "
            + "model that has moved on. Delete the entry.\nStale:\n  " + string.Join("\n  ", stale));

        unclassified.Should().BeEmpty(
            "every fact on a Symbol either travels across the import boundary or has a reason it "
            + "does not. An unclassified property is a fact nobody has decided about — which is "
            + "exactly how Documentation, SignatureKey, DeprecationMessage, IsMustUse and the field "
            + "flags all reached #1365 unnoticed. Add it to Facts, or to NotAFact with a "
            + "reason.\nUnclassified:\n  " + string.Join("\n  ", unclassified));
    }

    // --- CodeGenInfo member census --------------------------------------------------------------
    //
    // Every CodeGenInfo property is either serialized (RoundTrips) or rationale'd as same-file-only
    // with cited read sites. A new property reddens this census until its cache behaviour is stated.

    /// <summary>
    /// Classification of every <see cref="CodeGenInfo"/> property. "RoundTrips" means the property
    /// survives <see cref="SymbolSerializer.Serialize"/>/<c>Deserialize</c>; a rationale string
    /// means the property is same-file-only and cites the emitter read site.
    /// </summary>
    private static readonly Dictionary<string, string> CodeGenInfoFacts = new(StringComparer.Ordinal)
    {
        // --- RoundTrips: carried through CachedCodeGenInfo ---
        ["CSharpName"] = "RoundTrips",
        ["OriginalName"] = "RoundTrips",
        ["Version"] = "RoundTrips",
        ["IsModuleLevel"] = "RoundTrips",
        ["IsConstant"] = "RoundTrips",
        ["HasExecutionOrderIssues"] = "RoundTrips",
        ["IsStringEnum"] = "RoundTrips",
        ["ImportKind"] = "RoundTrips",
        ["OriginalImportName"] = "RoundTrips",
        ["ClrMethodName"] = "RoundTrips",
        ["StripsOverrideKeyword"] = "RoundTrips",
        ["ImplementsInterfaceMethod"] = "RoundTrips",

        // --- Same-file-only: read by the emitter for the file that declares the symbol ---
        ["IsCompileTimeConstant"] = "same-file-only — read at RoslynEmitter.Statements.Assignments.cs module-level const path (#1460)",
        ["OverridesClrBaseMember"] = "same-file-only — read at RoslynEmitter.ClassMembers.Methods.cs:155",
        ["ForwardingConstructors"] = "same-file-only — read at RoslynEmitter.ClassMembers.Constructors.cs:391",
        ["SelfInterfaceBridges"] = "same-file-only — read at RoslynEmitter.TypeDeclarations.cs:840",
        ["SynthesizedInterfaces"] = "same-file-only — read at RoslynEmitter.TypeDeclarations.cs (CollectSynthesizedInterfaces)",
    };

    /// <summary>
    /// Completeness guard: every public instance property on <see cref="CodeGenInfo"/> (except the
    /// record-synthesized <c>EqualityContract</c>) must appear in <see cref="CodeGenInfoFacts"/>.
    /// Adding a property to CodeGenInfo reddens this test until its serialization behaviour is stated.
    /// </summary>
    [Fact]
    public void EveryCodeGenInfoProperty_IsClassified()
    {
        var reflected = typeof(CodeGenInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToList();

        var unclassified = reflected
            .Where(name => !CodeGenInfoFacts.ContainsKey(name))
            .ToList();

        var stale = CodeGenInfoFacts.Keys
            .Where(name => !reflected.Contains(name))
            .ToList();

        stale.Should().BeEmpty(
            "a CodeGenInfoFacts entry for a property that no longer exists is stale — delete it.\n"
            + "Stale:\n  " + string.Join("\n  ", stale));

        unclassified.Should().BeEmpty(
            "every CodeGenInfo property must be classified as RoundTrips (carried through "
            + "CachedCodeGenInfo) or rationale'd as same-file-only with the emitter read site. "
            + "An unclassified property is a serialization decision nobody made — the #1309 shape "
            + "where a warm build silently drops a fact.\nUnclassified:\n  "
            + string.Join("\n  ", unclassified));
    }

    /// <summary>
    /// For properties declared as "RoundTrips", verify they survive a real serialize/deserialize
    /// cycle. Constructs a <see cref="CodeGenInfo"/> with non-default values for every RoundTrips
    /// property, attaches it to a symbol, round-trips through <see cref="SymbolSerializer"/>, and
    /// checks each value came back. A property the serializer silently drops fails here.
    /// </summary>
    [Fact]
    public void RoundTripsEntries_SurviveSerializerRoundTrip()
    {
        var original = new CodeGenInfo
        {
            CSharpName = "TestName",
            OriginalName = "test_name",
            Version = 3,
            IsModuleLevel = true,
            IsConstant = true,
            HasExecutionOrderIssues = true,
            IsStringEnum = true,
            ImportKind = ImportKind.FromImportWithAlias,
            OriginalImportName = "original_import",
            ClrMethodName = "ClrMethod",
            OverridesClrBaseMember = true,
            ForwardingConstructors = new List<FunctionSymbol>(),
            SelfInterfaceBridges = new List<SelfInterfaceBridgeSpec>(),
        };

        var symbol = new FunctionSymbol
        {
            Name = "probe",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void,
        };

        var binding = new SemanticBinding();
        binding.SetCodeGenInfo(symbol, original);

        var cached = SymbolSerializer.Serialize(symbol, "/test/probe.spy", binding);
        var restored = (FunctionSymbol)SymbolSerializer.Deserialize(
            cached, new Dictionary<string, Symbol>(StringComparer.Ordinal), binding: binding);

        var rt = binding.GetCodeGenInfo(restored);
        rt.Should().NotBeNull("the serializer must preserve CodeGenInfo via the binding");
        var cgi = rt!;

        var failures = new List<string>();
        void Check(string prop, object? expected, object? actual)
        {
            if (!CodeGenInfoFacts.TryGetValue(prop, out var classification))
                return;
            if (classification == "RoundTrips" && !Equals(expected, actual))
                failures.Add($"{prop}: expected {Render(expected)} but got {Render(actual)}");
        }

        Check("CSharpName", original.CSharpName, cgi.CSharpName);
        Check("OriginalName", original.OriginalName, cgi.OriginalName);
        Check("Version", original.Version, cgi.Version);
        Check("IsModuleLevel", original.IsModuleLevel, cgi.IsModuleLevel);
        Check("IsConstant", original.IsConstant, cgi.IsConstant);
        Check("HasExecutionOrderIssues", original.HasExecutionOrderIssues, cgi.HasExecutionOrderIssues);
        Check("IsStringEnum", original.IsStringEnum, cgi.IsStringEnum);
        Check("ImportKind", original.ImportKind, cgi.ImportKind);
        Check("OriginalImportName", original.OriginalImportName, cgi.OriginalImportName);
        Check("ClrMethodName", original.ClrMethodName, cgi.ClrMethodName);

        failures.Should().BeEmpty(
            "a property declared as RoundTrips must survive SymbolSerializer. If the serializer no "
            + "longer writes it, either thread it through or reclassify it as same-file-only with "
            + "a rationale.\nFailed round-trips:\n  " + string.Join("\n  ", failures));

        cgi.OverridesClrBaseMember.Should().BeFalse(
            "OverridesClrBaseMember is same-file-only and must NOT survive the round trip");
        cgi.ForwardingConstructors.Should().BeNull(
            "ForwardingConstructors is same-file-only and must NOT survive the round trip");
        cgi.SelfInterfaceBridges.Should().BeNull(
            "SelfInterfaceBridges is same-file-only and must NOT survive the round trip");
    }

    // --- Measurement ---------------------------------------------------------------------------

    /// <summary>
    /// The three views of one declaration this mirror holds against each other.
    /// </summary>
    /// <param name="Declared">
    /// <c>NameResolver</c>'s symbol for <c>lib.spy</c>'s own file — the authority.
    /// </param>
    /// <param name="FromImport">
    /// What <c>from lib import X</c> binds in the importing file's scope. Outside the source set
    /// this is <c>ImportResolver.CreateReExportSymbol</c>'s output, which for a
    /// <see cref="TypeSymbol"/> is a hand-built clone rather than a <c>with</c>-expression.
    /// </param>
    /// <param name="QualifiedImport">
    /// What <c>lib.X</c> reads: <c>ModuleSymbol.Exports</c>, which outside the source set holds
    /// <c>ModuleLoader</c>'s extraction unaltered. Splitting the two import spellings is what makes
    /// a divergence attributable — a fact present here and absent from <c>FromImport</c> was
    /// dropped by the re-export clone, one absent from both was never extracted. It is also the
    /// batch's Design Decision 3 in symbol form: the two spellings must name the same thing.
    /// </param>
    private sealed record Views(
        Dictionary<string, Symbol?> Declared,
        Dictionary<string, Symbol?> FromImport,
        Dictionary<string, Symbol?> QualifiedImport);

    private Views Measure()
    {
        var inSet = Arrange.LibInSourceSet(_output, LibSource, MainSource);
        var declaredScope = Arrange.ScopeOf(inSet, "lib.spy");
        var declared = Specimens("declared", name => declaredScope.Lookup(name, searchParent: false));

        var outside = Arrange.LibOutsideSourceSet(_output, LibSource, MainSource);
        var importingScope = Arrange.ScopeOf(outside, "main.spy");
        var fromImport = Specimens("fromImport", name => importingScope.Lookup(name, searchParent: false));

        var module = importingScope.Lookup("lib", searchParent: false) as ModuleSymbol
            ?? throw new InvalidOperationException(
                "arrangement failed: `import lib` did not bind a ModuleSymbol, so the qualified "
                + "view would be empty and every attribution below would collapse to one column");
        var qualified = Specimens(
            "qualifiedImport",
            name => module.Exports.TryGetValue(name, out var s) ? s : null);

        return new Views(declared, fromImport, qualified);
    }

    // --- Serializer round trip -----------------------------------------------------------------

    private static bool CanSerialize(Symbol symbol)
        => symbol is TypeSymbol or FunctionSymbol or VariableSymbol or ModuleSymbol
            or TypeAliasSymbol or TypeParameterSymbol;

    /// <summary>
    /// Serializes a symbol and deserializes it back, resolving cross-references exactly as
    /// <c>SymbolCache</c> does on a warm build.
    /// </summary>
    private static Symbol RoundTrip(Symbol symbol)
    {
        const string path = "/project/lib.spy";

        // A nested type is never serialized on its own: the cache reaches it through its enclosing
        // type's NestedTypes and re-links DeclaringType on restore. Round-tripping it standalone
        // would report a DeclaringType loss that the real cache does not have.
        var subject = symbol is TypeSymbol { DeclaringType: { } enclosing } ? enclosing : symbol;

        var cached = SymbolSerializer.Serialize(subject, path);
        var registry = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        var restored = SymbolSerializer.Deserialize(cached, registry);
        registry[cached.Id] = restored;
        SymbolSerializer.ResolveReferences(new[] { cached }, registry);

        if (!ReferenceEquals(subject, symbol))
        {
            return ((TypeSymbol)restored).NestedTypes.FirstOrDefault(n => n.Name == symbol.Name)
                ?? throw new InvalidOperationException(
                    $"round trip lost the nested type '{symbol.Name}' entirely, so no fact about it "
                    + "can be compared");
        }

        return restored;
    }

    // --- Rendering -----------------------------------------------------------------------------

    private static string Render(object? value) => value switch
    {
        null => "<null>",
        bool b => b ? "true" : "false",
        string s => $"\"{s}\"",
        _ => value.ToString() ?? "<null>",
    };

    private static bool IsNonDefault(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        int i => i != 0,
        _ => true,
    };

    private static string Join(IEnumerable<string> parts) => string.Join("|", parts);

    /// <summary>
    /// The file name of a path, or null when there is none. Null rather than a "&lt;null&gt;"
    /// sentinel so the anti-vacuity and round-trip checks see an absent value as absent.
    /// </summary>
    private static string? FileNameOf(string? path)
        => string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);

    /// <summary>
    /// A structural description of a <see cref="SemanticType"/>: kind, name and arguments, with no
    /// symbol identity. The two sides resolve the same annotation through different code paths, so
    /// comparing instances would measure the paths rather than the type they produced.
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
        FunctionType f => $"({string.Join(",", f.ParameterTypes.Select(Describe))})->{Describe(f.ReturnType)}",
        TypeParameterType p => p.Name,
        ResultType r => $"{Describe(r.OkType)}!{Describe(r.ErrorType)}",
        VoidType => "None",
        UnknownType => "<unknown>",
        _ => type.GetType().Name,
    };

    // --- Arrangements --------------------------------------------------------------------------

    /// <summary>
    /// The two compilation shapes this mirror compares. Both assert their own defining property, so
    /// a setup that silently became the other one fails here rather than producing a green matrix
    /// about the wrong path.
    /// </summary>
    private static class Arrange
    {
        /// <summary>
        /// <c>lib.spy</c> and <c>main.spy</c> are both compilation units — the ordinary shape, where
        /// the import resolves to the compilation's own symbols.
        /// </summary>
        public static ProjectAnalysisResult LibInSourceSet(
            ITestOutputHelper output, string libSource, string mainSource)
        {
            using var helper = new Helpers.ProjectCompilationHelper(output)
                .WithRootNamespace("SymbolFactMirrorInSet")
                .WithOutputType("exe")
                .WithEntryPoint("main.spy");

            helper.AddSourceFile("lib.spy", libSource);
            helper.AddSourceFile("main.spy", mainSource);
            helper.CreateProjectFile();

            var config = ProjectFileParser.Load(
                Path.Combine(helper.ProjectDirectory, "SymbolFactMirrorInSet.spyproj"));
            config.SourceFiles.Should().Contain(
                f => Path.GetFileName(f) == "lib.spy",
                "the control is about lib.spy BEING a compilation unit");

            return Analyze(output, config);
        }

        /// <summary>
        /// <c>lib.spy</c> sits beside the project file, outside <c>src/**/*.spy</c>: importable
        /// (the project directory is a module search path) but never a compilation unit, so nothing
        /// name-resolves it and the importer gets <c>ModuleLoader</c>'s extraction. This is the only
        /// shape in which the extraction is what the user sees — see
        /// <see cref="ImportedStubClassificationReachabilityTests"/>.
        /// </summary>
        public static ProjectAnalysisResult LibOutsideSourceSet(
            ITestOutputHelper output, string libSource, string mainSource)
        {
            using var helper = new Helpers.ProjectCompilationHelper(output)
                .WithRootNamespace("SymbolFactMirrorOutside")
                .WithOutputType("exe")
                .WithEntryPoint("main.spy");

            helper.AddSourceFile("main.spy", mainSource);
            File.WriteAllText(Path.Combine(helper.ProjectDirectory, "lib.spy"), libSource);
            helper.CreateProjectFile();

            var config = ProjectFileParser.Load(
                Path.Combine(helper.ProjectDirectory, "SymbolFactMirrorOutside.spyproj"));
            config.SourceFiles.Should().NotContain(
                f => Path.GetFileName(f) == "lib.spy",
                "the mirror measures the extraction; if lib.spy became a source file the imported "
                + "side would be the declaration itself and every cell would agree trivially");

            return Analyze(output, config);
        }

        private static ProjectAnalysisResult Analyze(
            ITestOutputHelper output, ProjectConfig config)
        {
            var result = new CompilerApi().AnalyzeProject(config);
            foreach (var diag in result.Diagnostics.GetAll())
                output.WriteLine($"  {diag.Code} {diag.Message}");
            return result;
        }

        public static Scope ScopeOf(ProjectAnalysisResult result, string fileName)
        {
            var table = result.ProjectModel.GlobalSymbols
                ?? throw new InvalidOperationException("arrangement failed: analysis produced no symbol table");
            var unit = result.ProjectModel.Units.Values
                .SingleOrDefault(u => Path.GetFileName(u.FilePath) == fileName)
                ?? throw new InvalidOperationException(
                    $"arrangement failed: '{fileName}' is not a compilation unit — units were "
                    + string.Join(", ", result.ProjectModel.Units.Values.Select(u => Path.GetFileName(u.FilePath))));

            return table.GetModuleScope(unit.ModulePath)
                ?? throw new InvalidOperationException(
                    $"arrangement failed: no module scope for '{unit.ModulePath}'");
        }
    }
}
