using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Critical Rule 2, enforced by inversion: <b>CodeGen may name only the materialized-fact
/// carriers</b>, and every other type in the compiler's <c>Semantic</c> namespaces is denied
/// (#1475).
///
/// <para>
/// <b>Why inverted.</b> The predecessor guard (<see cref="EmitterBannedTokenScanTests"/>) named
/// five forbidden substrings. Naming what is forbidden means the guard can only fail for spellings
/// someone thought to list, and the violation Rule 2 actually describes — a type or lowering
/// decision taken emitter-side — carries none of them: <c>TypeSubstitution.Apply(...)</c> and
/// <c>GenericInstantiationWalker</c> are both <c>internal</c>, callable from CodeGen with no
/// diagnostic, and left the old scan green. Naming what is <em>permitted</em> flips the failure
/// mode: a new semantic type is denied by default the moment it is added, because the deny
/// universe is enumerated by reflection over the assembly rather than spelled in a list that can
/// go stale.
/// </para>
///
/// <para>
/// <b>The allowed set is derived structurally, not spelled.</b> Seeds are the carriers Rule 2
/// names: the standalone symbol records, <c>SemanticInfo</c>, <c>CodeGenInfo</c> and
/// <c>SemanticBinding</c>. From those it takes the transitive closure
/// of the semantic types appearing in their public signatures — so the node-keyed fact records
/// (<c>BinaryOpLowering</c>, <c>NarrowedReadLowering</c>, <c>TypeTestLowering</c>, …) and the
/// enums they carry join automatically. Adding a new fact record and reading it from CodeGen needs
/// no edit here; adding a new <em>decision-maker</em> and calling it from CodeGen fails this test.
/// </para>
///
/// <para>
/// <b>Catalogs are leaves.</b> Four read-only catalog types are allowed by explicit,
/// rationale-annotated entry (see <see cref="ReadOnlyCatalogs"/>) because the emitter legitimately
/// consults them, and they do <em>not</em> propagate their own signatures into the allowed set: a
/// catalog is a name lookup, not a carrier, so what it happens to return must not silently widen
/// the guard.
/// </para>
///
/// <para>
/// <b>Scope and residual.</b> The scan is syntactic (Roslyn identifier nodes, so comments and
/// <c>&lt;see cref&gt;</c> mentions are excluded by construction — they are trivia). It cannot see a
/// denied type reached through an instance member read whose qualifier is a local
/// (<c>ctx.Whatever</c>), because without a semantic model <c>x.Scope</c> and
/// <c>Semantic.Scope</c> are the same shape; namespace-qualified statics
/// (<c>Semantic.TypeSubstitution.Apply</c>) ARE checked, because the qualifier's last segment is a
/// known namespace segment. Every way of naming a denied type as a type — declaration, cast,
/// pattern, generic argument, static call — is covered.
/// </para>
///
/// <para>
/// <b>The <c>SemanticType</c> and <c>Symbol</c> hierarchies are inspection-denied (#2443).</b>
/// They were carrier hierarchy roots until P82; <see cref="CarrierHierarchyRoots"/> is now empty
/// and every type in the two hierarchies (by reflection, <c>IsAssignableFrom</c>) is denied in
/// CodeGen whatever the closure says, because <c>SemanticInfo</c>'s and <c>CodeGenInfo</c>'s
/// signatures would otherwise re-admit them. The emitter may hand a <c>SemanticType</c> to
/// <c>TypeSyntaxMapper</c> but may not inspect one outside it (#2443 contract item 1), so
/// <see cref="InspectionExemptFile"/> is exempt from the <c>type-name</c> kind. Every site that
/// still inspects is a row of <c>CodeGen/emitter-inspection-allowlist.txt</c>, four kinds
/// (<see cref="InspectionKinds"/>), counted per (file, kind, name) and ratcheted both ways by
/// <see cref="CodeGenInspectionSites_MatchTheAllowlist"/>; the file drains to empty.
/// </para>
///
/// <para>
/// <b>Mutation-tested.</b> See <see cref="SyntheticTypeSubstitutionCall_IsFlagged"/> and
/// <see cref="CarrierOnlySource_IsNotFlagged"/> for the executable pair, and the procedure
/// recorded on the former for the one-time real-file run.
/// </para>
/// </summary>
public class EmitterCarrierOnlyConformanceTests
{
    private readonly ITestOutputHelper _output;

    public EmitterCarrierOnlyConformanceTests(ITestOutputHelper output) => _output = output;

    /// <summary>The namespace root whose types CodeGen may not name unless they are carriers.</summary>
    private const string SemanticNamespaceRoot = "Sharpy.Compiler.Semantic";

    /// <summary>
    /// Read-only catalogs the emitter legitimately consults, each with the reason it is not a
    /// decision. Every entry is measured — a name here that CodeGen does not actually reference
    /// fails <see cref="ReadOnlyCatalogs_AreAllStillReferenced"/>, so this list drains like an
    /// allowlist rather than accumulating permissions nobody uses.
    /// </summary>
    private static readonly (string Type, string Rationale)[] ReadOnlyCatalogs =
    {
        ("SymbolTable",
            "the documented name-resolution strategy: 'Types → SymbolTable lookup' (CLAUDE.md, "
            + "Code Generation). A lookup by name returns a symbol whose facts were already decided."),
        ("BuiltinRegistry",
            "CodeGenContext's builtin catalog — asks whether a name is a builtin and what it maps "
            + "to; it decides nothing about the program being compiled."),
        ("DunderNames",
            "a catalog of dunder name string constants (__init__, __add__, …). Sharing the "
            + "constants with semantic analysis is the alternative to CodeGen spelling the same "
            + "strings itself, which is how the two sides drift apart."),
        ("ProtocolRegistry",
            "the protocol-interface catalog (ISized, IBoolConvertible, IReverseEnumerable) the "
            + "emitter reads when adding a synthesized interface to a base list; membership is "
            + "decided in semantic analysis and recorded, this only names the interfaces."),
    };

    /// <summary>
    /// Structural seeds: hierarchy roots whose every derived type is a carrier. EMPTY since P82
    /// (#2443): <c>SemanticType</c> and <c>Symbol</c> moved to
    /// <see cref="InspectionDeniedHierarchyRoots"/>. The standalone symbol records are not
    /// <c>Symbol</c> subclasses and stay in <see cref="CarrierSeeds"/>.
    /// </summary>
    private static readonly string[] CarrierHierarchyRoots = Array.Empty<string>();

    /// <summary>
    /// Hierarchies denied in CodeGen whatever the carrier closure says (#2443): every type
    /// assignable to one of these roots. Their remaining CodeGen mentions are rows of the
    /// inspection allowlist, not <see cref="Ratchet"/> entries.
    /// </summary>
    private static readonly string[] InspectionDeniedHierarchyRoots = { "SemanticType", "Symbol" };

    /// <summary>
    /// The one CodeGen file allowed to name the inspection-denied hierarchies (#2443 contract
    /// item 1: the emitter may pass a <c>SemanticType</c> to <c>TypeSyntaxMapper</c> but may not
    /// inspect one outside it). Exempt from the <c>type-name</c> kind only; its mention count is
    /// printed (<c>CARRIER-INSPECTION-EXEMPT</c>), not ratcheted.
    /// </summary>
    internal const string InspectionExemptFile = "TypeSyntaxMapper.cs";

    private static readonly string[] CarrierSeeds =
    {
        "SemanticInfo",
        "CodeGenInfo",
        // The third fact store CLAUDE.md's Key Data Structures names: "stores computed semantic
        // data (CodeGenInfo, variable types) separately from symbols, materialized at phase
        // boundaries". SemanticBinding.GetCodeGenInfo is the emitter's PREFERRED read path for a
        // materialized fact (RoslynEmitter.cs:1253 says so in as many words), which makes it a
        // carrier by the same argument as SemanticInfo, not a decision-maker.
        "SemanticBinding",
        "PropertySymbol",
        "EventSymbol",
        "ParameterSymbol",
    };

    /// <summary>
    /// Closure-denied types (outside the <see cref="InspectionDeniedHierarchyRoots"/> hierarchies)
    /// CodeGen still names, with the measured site count per file and the issue that
    /// removes them. Counted, not merely listed: a NEW reference to an already-ratcheted type in
    /// an already-ratcheted file raises the count and fails, so the ratchet cannot absorb fresh
    /// violations under an existing entry.
    ///
    /// <para>Drains on fix in both directions — <see cref="Ratchet_HasNoStaleEntries"/> fails on
    /// an entry whose real count has dropped, so the number must come down in the same commit and
    /// the entry must be deleted when it reaches zero. Every entry below is a real Critical Rule 2
    /// violation the first run of this guard found (2026-08-14, HEAD <c>807166ac0</c>), which the
    /// five-substring predecessor scan reported green.</para>
    /// </summary>
    private static readonly (string File, string Type, int Count, string Issue)[] Ratchet =
        Array.Empty<(string, string, int, string)>();

    // ---- the guard ---------------------------------------------------------------------------

    [Fact]
    public void CodeGenSources_NameOnlyMaterializedFactCarriers()
    {
        var universe = SemanticUniverse();
        var allowed = AllowedCarrierNames(universe);
        var hierarchy = InspectionDeniedNames(universe);
        var closureDenied = universe.Select(SimpleName)
            .Where(n => !allowed.Contains(n) && !hierarchy.Contains(n))
            .ToHashSet(StringComparer.Ordinal);
        var denied = DeniedNames(universe);

        // A deny universe that collapsed to nothing would pass for free — the failure mode a guard
        // must not have (the same shape as an empty-corpus sweep). The floor is on the CLOSURE's
        // share: the hierarchy deny set alone would clear it while the closure swallowed the rest.
        closureDenied.Should().HaveCountGreaterThan(50,
            "the closure-denied set is every non-carrier, non-hierarchy type in the Semantic "
            + "namespaces; if it shrank to a handful, the structural allow-list has swallowed the "
            + "assembly and this guard no longer guards anything");

        // Hierarchy mentions are the inspection allowlist's business
        // (CodeGenInspectionSites_MatchTheAllowlist); this guard ratchets the rest.
        var violations = ScanCodeGenSources(denied, NamespaceSegments(universe))
            .Where(v => !hierarchy.Contains(v.Type))
            .ToList();

        _output.WriteLine(
            $"Semantic universe: {universe.Count} types — {allowed.Count} allowed by the closure, "
            + $"{denied.Count} denied ({closureDenied.Count} closure-denied + {hierarchy.Count} "
            + $"inspection-denied hierarchy types). Closure violations: {violations.Count}.");
        foreach (var violation in violations)
            _output.WriteLine($"  {violation}");

        // Ratcheted (file, type) pairs are exempt only up to their measured count: the 3rd
        // TypeHierarchyService call in a file allowed 2 is a new violation, not a covered one.
        var unratcheted = violations
            .GroupBy(v => (v.File, v.Type))
            .SelectMany(group =>
            {
                var budget = Ratchet
                    .Where(r => string.Equals(r.File, group.Key.File, StringComparison.Ordinal)
                                && string.Equals(r.Type, group.Key.Type, StringComparison.Ordinal))
                    .Sum(r => r.Count);
                return group.Skip(budget);
            })
            .ToList();

        unratcheted.Should().BeEmpty(
            "code generation is a pure translator: it reads facts that semantic analysis already "
            + "decided and materialized (Critical Rule 2). Naming a Semantic type that is not a "
            + "materialized-fact carrier means the emitter is reaching for the machinery that MAKES "
            + "the decision — the shape #1039/#1041 removed and #1475 made mechanically visible. "
            + "The fix is to move the decision into semantic analysis and materialize it: onto "
            + "Symbol.CodeGenInfo (symbol-keyed, frozen at MaterializeCodeGenInfo) or into a "
            + "SemanticInfo dictionary (node-keyed — and add it to SemanticInfo.MergeFrom or its "
            + "entries vanish in the per-file → project merge). A new fact record needs no edit "
            + "here: it joins the allowed set through SemanticInfo's public signature. If a "
            + "violation cannot be fixed now, file an issue and add a Ratchet entry citing it.\n"
            + "Violations:\n  " + string.Join("\n  ", unratcheted));
    }

    /// <summary>
    /// The named threat (#1475's owner ruling): the two decision-makers that motivated the
    /// inversion must be DENIED, not swept into the allowed closure by some signature path. If
    /// either drifts into the allowed set the guard still passes on clean sources while having
    /// stopped guarding the thing it was built for.
    /// </summary>
    [Fact]
    public void DenyUniverse_ContainsTheDecisionMakers()
    {
        var universe = SemanticUniverse();
        var allowed = AllowedCarrierNames(universe);
        var universeNames = universe.Select(SimpleName).ToHashSet(StringComparer.Ordinal);

        string[] mustBeDenied =
        {
            // The owner's named mutation subjects.
            "TypeSubstitution", "GenericInstantiationWalker",
            // The decision-makers Rule 2 exists to keep out of CodeGen.
            "TypeChecker", "TypeResolver", "NameResolver", "TypeInferenceService",
            "GenericTypeInferenceService", "TypeHierarchyService", "CodeGenInfoComputer",
            "SynthesisAnalyzer", "ImportResolver", "InheritanceResolver", "ExecutionOrderAnalyzer",
        };

        foreach (var name in mustBeDenied)
        {
            universeNames.Should().Contain(name,
                $"{name} is expected in the Semantic namespaces; if it moved or was renamed, this "
                + "guard is naming a type that no longer exists and must be updated");
            allowed.Should().NotContain(name,
                $"{name} decides things — it must stay in the deny universe. Finding it ALLOWED "
                + "means the structural closure reached it through some carrier's public "
                + "signature, which widens the allow-list far past materialized facts.");
        }
    }

    [Fact]
    public void ReadOnlyCatalogs_AreAllStillReferenced()
    {
        var universe = SemanticUniverse();
        var universeNames = universe.Select(SimpleName).ToHashSet(StringComparer.Ordinal);
        var referenced = ReferencedSemanticNames(universeNames, NamespaceSegments(universe));

        foreach (var (type, _) in ReadOnlyCatalogs)
        {
            universeNames.Should().Contain(type,
                $"catalog entry '{type}' names a type that is no longer in the Semantic namespaces");
            referenced.Should().Contain(type,
                $"catalog entry '{type}' is a permission CodeGen no longer uses — delete the entry "
                + "rather than leaving a standing exemption nothing needs (allowlists drain)");
        }
    }

    [Fact]
    public void Ratchet_HasNoStaleEntries()
    {
        var universe = SemanticUniverse();
        var denied = DeniedNames(universe);
        var violations = ScanCodeGenSources(denied, NamespaceSegments(universe));

        foreach (var (file, type, count, issue) in Ratchet)
        {
            var actual = violations.Count(v =>
                string.Equals(v.File, file, StringComparison.Ordinal)
                && string.Equals(v.Type, type, StringComparison.Ordinal));

            actual.Should().BeGreaterThanOrEqualTo(count,
                $"ratchet entry {file} — {type} ({issue}) allows {count} references but only "
                + $"{actual} remain: lower the number, or delete the entry when it reaches zero, "
                + "in the same commit that removed them (allowlists drain on fix)");
        }
    }

    // ---- the CLR-name visibility seam (#2320) ------------------------------------------------

    /// <summary>The namespace root of the CLR-discovery layer, whose visibility seam CodeGen may not name.</summary>
    private const string DiscoveryNamespaceRoot = "Sharpy.Compiler.Discovery";

    /// <summary>
    /// The emitter performs no reflection (Critical Rule 2), so it may not name the CLR-name
    /// visibility seam: <c>AssemblyVisibility</c> — the ONE place a CLR name is resolved against
    /// {framework} ∪ {the registry's references} (#2250) — or any discovery type whose own
    /// surface carries it (a resolver handed the visibility to answer from). The deny set is
    /// derived by reflection, so a resolver added tomorrow is denied the day it takes the seam.
    ///
    /// <para>The <see cref="CodeGenSources_NameOnlyMaterializedFactCarriers"/> deny universe is
    /// the <c>Semantic</c> namespaces, so it never saw this: <c>IsClrTypeGenericInNamespace</c>
    /// (<c>RoslynEmitter.CompilationUnit.cs</c>) asked <c>AssemblyVisibility.FrameworkOnly</c>
    /// whether a <c>using</c> alias named an open generic, under a guard admitting any
    /// <c>System.*</c> module — a referenced package in such a namespace was visible to the
    /// checker and invisible there. The answer is now the imported symbol's
    /// <c>CodeGenInfo.IsOpenGenericDefinition</c> (#2320).</para>
    /// </summary>
    [Fact]
    public void CodeGenSources_NameNoClrVisibilitySeam()
    {
        var seam = VisibilitySeamNames();
        seam.Should().Contain("AssemblyVisibility",
            "the visibility seam is the type the deny set is anchored on; if it was renamed or moved "
            + "out of the Discovery namespace this guard is derived from a type that no longer exists");

        var violations = ScanCodeGenSources(seam, DiscoveryNamespaceSegments());
        foreach (var violation in violations)
            _output.WriteLine($"  {violation}");

        violations.Should().BeEmpty(
            "code generation performs no reflection: which CLR names are visible to this compilation "
            + "is decided against the registry's visibility in discovery/semantic analysis and "
            + "materialized for the emitter (CodeGenInfo / SemanticInfo). Seam types: "
            + string.Join(", ", seam.OrderBy(n => n, StringComparer.Ordinal))
            + "\nViolations:\n  " + string.Join("\n  ", violations));
    }

    /// <summary>
    /// Positive and negative controls for <see cref="CodeGenSources_NameNoClrVisibilitySeam"/>:
    /// the deleted emitter read, in every spelling it can take, is flagged; the name catalog
    /// CodeGen legitimately consults (<c>ClrTypeBridge</c>, which answers from a static table)
    /// is not in the seam set and is not flagged.
    /// </summary>
    [Fact]
    public void SyntheticVisibilitySeamRead_IsFlagged()
    {
        var seam = VisibilitySeamNames();
        var segments = DiscoveryNamespaceSegments();

        var spellings = new (string Label, string Source)[]
        {
            ("the deleted #2320 read, namespace-qualified",
                "class C { bool M(string n) { var framework = Discovery.AssemblyVisibility.FrameworkOnly; return framework.ResolveType(n) != null; } }"),
            ("unqualified static read",
                "class C { object M() => AssemblyVisibility.FrameworkOnly; }"),
            ("fully qualified declaration",
                "class C { private Sharpy.Compiler.Discovery.AssemblyVisibility _v; }"),
        };
        foreach (var (label, source) in spellings)
        {
            ScanSource("Mutation.cs", source, seam, segments).Should().NotBeEmpty(
                $"a {label} is the emitter asking reflection a semantic question");
        }

        seam.Should().NotContain("ClrTypeBridge",
            "ClrTypeBridge's surface takes no visibility — it is the Sharpy-name → C#-name catalog "
            + "TypeSyntaxMapper reads, not a resolver");
        ScanSource("Control.cs",
                "class C { string? M(string n) => ClrTypeBridge.TryGetCSharpTypeName(n); }", seam, segments)
            .Should().BeEmpty("a catalog lookup is not a visibility read");
    }

    /// <summary>
    /// <c>AssemblyVisibility</c> and every type in the Discovery namespaces whose non-private
    /// signature (property, field, method, constructor) carries it.
    /// </summary>
    private static HashSet<string> VisibilitySeamNames()
    {
        var discovery = DiscoveryUniverse();
        var visibility = discovery.FirstOrDefault(t => t.Name == "AssemblyVisibility");
        visibility.Should().NotBeNull("AssemblyVisibility is expected in the Discovery namespaces");

        return discovery
            .Where(t => t == visibility || SignatureTypes(t).Contains(visibility!))
            .Select(SimpleName)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<Type> DiscoveryUniverse()
    {
        var assembly = typeof(SemanticInfo).Assembly;
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types
            .Where(t => t is not null)
            .Select(t => t!)
            .Where(t => t.Namespace is { } ns
                        && (ns == DiscoveryNamespaceRoot
                            || ns.StartsWith(DiscoveryNamespaceRoot + ".", StringComparison.Ordinal)))
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal))
            .ToList();
    }

    private static HashSet<string> DiscoveryNamespaceSegments()
        => DiscoveryUniverse()
            .Select(t => t.Namespace!)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(ns => ns.Split('.'))
            .ToHashSet(StringComparer.Ordinal);

    // ---- executable mutation cells (#1475, Task 3) --------------------------------------------

    /// <summary>
    /// Positive mutation cell: the owner's named violation — a <c>TypeSubstitution.Apply(...)</c>
    /// call from CodeGen — must be flagged, in every spelling it can take.
    ///
    /// <para><b>Real-file mutation, run once at implementation time (2026-08-14, HEAD
    /// <c>807166ac0</c>).</b> Procedure: insert into <c>GenerateExpression</c> in
    /// <c>RoslynEmitter.Expressions.cs</c>, immediately after the recorder hook,
    /// <code>
    /// var mutationProbe = TypeSubstitution.Apply(
    ///     _context.SemanticInfo?.GetExpressionType(expr) ?? new Semantic.UnknownType(),
    ///     new Dictionary&lt;string, Semantic.SemanticType&gt;());
    /// _ = mutationProbe;
    /// </code>
    /// (it must compile — this guard scans source, but the suite has to build to run), then
    /// <c>.claude/scripts/dotnet-serialized test … --filter "FullyQualifiedName~EmitterCarrierOnly
    /// ConformanceTests|FullyQualifiedName~EmitterBannedTokenScanTests"</c>.</para>
    ///
    /// <para><b>Observed:</b> <c>CodeGenSources_NameOnlyMaterializedFactCarriers</c> FAILED —
    /// <c>RoslynEmitter.Expressions.cs:27 — TypeSubstitution — var mutationProbe =
    /// TypeSubstitution.Apply(</c> — while <c>CodeGenSources_ContainNoBannedTokenSubstrings</c>,
    /// the five-substring predecessor, passed GREEN on the same tree (7 passed / 1 failed of 8).
    /// That single run is the whole of #1475: the guard Critical Rule 2 named could not fail for
    /// the violation Rule 2 describes, and this one does. Mutation reverted; <c>git diff</c> on the
    /// file is empty.</para>
    /// </summary>
    [Fact]
    public void SyntheticTypeSubstitutionCall_IsFlagged()
    {
        var universe = SemanticUniverse();
        var denied = DeniedNames(universe);
        var segments = NamespaceSegments(universe);

        var spellings = new (string Label, string Source)[]
        {
            ("unqualified static call",
                "class C { void M(SemanticType t) { var r = TypeSubstitution.Apply(t, _map); } }"),
            ("namespace-qualified static call",
                "class C { void M(SemanticType t) { var r = Semantic.TypeSubstitution.Apply(t, _map); } }"),
            ("declaration",
                "class C { private GenericInstantiationWalker _walker; }"),
            ("generic argument",
                "class C { void M() { var xs = new List<TypeSubstitution>(); } }"),
            ("cast",
                "class C { void M(object o) { var t = (TypeChecker)o; } }"),
        };

        foreach (var (label, source) in spellings)
        {
            var violations = ScanSource("Mutation.cs", source, denied, segments);
            violations.Should().NotBeEmpty(
                $"a {label} naming a decision-maker is exactly what this guard exists to catch; "
                + "finding nothing means the scan has stopped seeing that syntactic position");
        }
    }

    /// <summary>
    /// Negative control (verify-the-instrument): source that names only carriers must NOT be
    /// flagged, and neither must a denied type mentioned only in a comment or a
    /// <c>&lt;see cref&gt;</c>. A guard that flags everything is as useless as one that flags
    /// nothing, and the comment case is the one the predecessor scan had to hand-strip.
    /// </summary>
    [Fact]
    public void CarrierOnlySource_IsNotFlagged()
    {
        var universe = SemanticUniverse();
        var denied = DeniedNames(universe);
        var segments = NamespaceSegments(universe);

        var clean = new (string Label, string Source)[]
        {
            // SemanticType and Symbol are inspection-denied since #2443, so the control names only
            // the carriers that remain; the expression type is passed on, never named.
            ("carrier reads",
                @"class C {
                    ExpressionSyntax M(Expression e, SemanticInfo info, PropertySymbol s) {
                        var t = info.GetExpressionType(e);
                        CodeGenInfo? cg = s.CodeGenInfo;
                        NarrowedReadLowering? n = info.GetNarrowedReadLowering(e);
                        return Build(t, cg, n);
                    }
                }"),
            ("line comment naming a decision-maker",
                "class C { void M() { // TypeSubstitution.Apply used to be called here (#1475)\n } }"),
            ("doc comment naming a decision-maker",
                "/// <summary>Historically re-derived via <see cref=\"TypeSubstitution\"/>.</summary>\n"
                + "class C { void M() { } }"),
            ("instance member read that happens to share a denied type's name",
                "class C { void M(object ctx) { var s = ctx.Scope; } }"),
        };

        foreach (var (label, source) in clean)
        {
            var violations = ScanSource("Control.cs", source, denied, segments);
            violations.Should().BeEmpty(
                $"{label} is not a Rule-2 violation; flagging it would make the guard "
                + "unusable and push people to weaken it.\nFlagged:\n  "
                + string.Join("\n  ", violations));
        }
    }

    // ---- the inspection allowlist (#2443) ------------------------------------------------------

    /// <summary>The allowlist file, next to this test in <c>src/Sharpy.Compiler.Tests/CodeGen/</c>.</summary>
    internal const string InspectionAllowlistFileName = "emitter-inspection-allowlist.txt";

    private const string KindTypeName = "type-name";
    private const string KindTypeRead = "type-read";
    private const string KindNameLookup = "name-lookup";
    private const string KindDeclarationLine = "declaration-line";

    /// <summary>
    /// The four ways CodeGen still inspects a <c>SemanticType</c> or <c>Symbol</c> instead of
    /// reading a materialized fact (#2443 contract item 6):
    /// <list type="bullet">
    /// <item><c>type-name</c> — an inspection-denied hierarchy type named in a CodeGen file
    /// (declaration, cast, pattern, generic argument, static call), per (file, type), by the
    /// <see cref="ScanSource"/> identifier scan;</item>
    /// <item><c>type-read</c> — a <c>GetExpressionType(</c> / <c>GetExpressionSemanticType(</c>
    /// invocation, per (file, accessor), by Roslyn invocation nodes;</item>
    /// <item><c>name-lookup</c> — a lookup by <c>.Name ==</c> in a lambda predicate
    /// (<c>predicate</c>, the W2.1 census's <c>(\w+) =&gt;\s*\1\.Name ==</c> 2-line window and
    /// its exclusions, every match in the window) or a union-case <c>.Name == "Some"/"Ok"/"Err"/"None"</c> compare
    /// (<c>union-case-compare</c>), per (file, shape), by comment-aware line scan;</item>
    /// <item><c>declaration-line</c> — a <c>DeclarationLine</c> reference, per file, by Roslyn
    /// identifier nodes.</item>
    /// </list>
    /// </summary>
    private static readonly string[] InspectionKinds =
        { KindTypeName, KindTypeRead, KindNameLookup, KindDeclarationLine };

    internal readonly record struct InspectionSite(string File, int Line, string Kind, string Name, string Source)
    {
        public override string ToString() => $"{File}:{Line} — {Kind} {Name} — {Source}";
    }

    internal readonly record struct InspectionRow(
        int LineNumber, string File, string Kind, string Name, int Count, string Cite, string Note);

    /// <summary>
    /// The inspection allowlist as a ratchet that drains both ways: every (file, kind, name) with
    /// sites needs a row whose count equals the measured site count — more sites is a NEW
    /// inspection (fail), fewer is a STALE row (fail: lower it, or delete the row at zero, in the
    /// commit that removed the sites). A row whose file no longer exists fails; every row cites
    /// an issue (<c>cite=#N</c>). The per-kind totals are printed as
    /// <c>CARRIER-INSPECTION-CENSUS</c> lines so a recap can quote them.
    /// </summary>
    [Fact]
    public void CodeGenInspectionSites_MatchTheAllowlist()
    {
        var universe = SemanticUniverse();
        var hierarchy = InspectionDeniedNames(universe);
        var segments = NamespaceSegments(universe);
        var directory = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();

        var sites = new List<InspectionSite>();
        var exemptMentions = 0;
        foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            var fileSites = ScanInspectionSites(fileName, File.ReadAllText(path), hierarchy, segments);
            exemptMentions += fileSites.Count(s => IsInspectionExempt(s.File, s.Kind));
            sites.AddRange(fileSites.Where(s => !IsInspectionExempt(s.File, s.Kind)));
        }

        foreach (var kind in InspectionKinds)
        {
            var ofKind = sites.Where(s => s.Kind == kind).ToList();
            _output.WriteLine(
                $"CARRIER-INSPECTION-CENSUS kind={kind} sites={ofKind.Count} "
                + $"files={ofKind.Select(s => s.File).Distinct(StringComparer.Ordinal).Count()}");
        }

        _output.WriteLine(
            $"CARRIER-INSPECTION-EXEMPT file={InspectionExemptFile} kind={KindTypeName} mentions={exemptMentions}");

        var measured = sites
            .GroupBy(s => (s.File, s.Kind, s.Name))
            .ToDictionary(g => g.Key, g => g.ToList());
        var rows = LoadInspectionAllowlist(out var problems);

        foreach (var row in rows)
        {
            if (!File.Exists(Path.Combine(directory, row.File)))
            {
                problems.Add($"line {row.LineNumber}: {row.File} no longer exists in CodeGen/ — delete the row");
                continue;
            }

            var actual = measured.TryGetValue((row.File, row.Kind, row.Name), out var found) ? found.Count : 0;
            if (actual > row.Count)
            {
                problems.Add(
                    $"line {row.LineNumber}: {row.File} | {row.Kind} | {row.Name} allows {row.Count} but "
                    + $"{actual} sites exist — a NEW inspection; read a materialized fact instead "
                    + $"({row.Cite}):\n    " + string.Join("\n    ", found!));
            }
            else if (actual < row.Count)
            {
                problems.Add(
                    $"line {row.LineNumber}: {row.File} | {row.Kind} | {row.Name} allows {row.Count} but "
                    + $"only {actual} remain — STALE: lower the count (delete the row at 0) in the "
                    + "commit that removed them (allowlists drain on fix)");
            }
        }

        var listed = rows.Select(r => (r.File, r.Kind, r.Name)).ToHashSet();
        foreach (var (key, group) in measured.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
        {
            if (!listed.Contains(key))
            {
                problems.Add(
                    $"{key.File} | {key.Kind} | {key.Name}: {group.Count} sites without a row — the "
                    + "emitter may not inspect a SemanticType/Symbol (#2443); materialize the fact:\n    "
                    + string.Join("\n    ", group));
            }
        }

        problems.Should().BeEmpty(
            "every remaining CodeGen inspection of a SemanticType/Symbol is a counted row of "
            + $"{InspectionAllowlistFileName} citing its class issue, and the file drains both ways.\n"
            + string.Join("\n", problems)
            + "\n\nMeasured rows (counts only — keep each row's note):\n"
            + string.Join("\n", measured
                .OrderBy(kv => kv.Key.File, StringComparer.Ordinal)
                .ThenBy(kv => Array.IndexOf(InspectionKinds, kv.Key.Kind))
                .ThenBy(kv => kv.Key.Name, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key.File} | {kv.Key.Kind} | {kv.Key.Name} | {kv.Value.Count} | cite=#2443 |")));
    }

    /// <summary>
    /// M-d, executable: an in-memory source that names a <c>SemanticType</c> subtype is flagged as
    /// a <c>type-name</c> inspection; one that only hands the recorded type to
    /// <c>TypeSyntaxMapper</c> is not (#2443 contract item 1).
    /// </summary>
    [Fact]
    public void SyntheticHierarchyInspection_IsFlagged_MapperHandOffIsNot()
    {
        var universe = SemanticUniverse();
        var hierarchy = InspectionDeniedNames(universe);
        var segments = NamespaceSegments(universe);

        hierarchy.Should().Contain(new[] { "SemanticType", "Symbol", "OptionalType", "GenericType", "UnknownType" },
            "the inspection-denied set is every type assignable to SemanticType or Symbol");
        hierarchy.Should().NotContain(new[] { "PropertySymbol", "SemanticInfo", "CodeGenInfo" },
            "standalone symbol records and the fact stores are carriers, not hierarchy members");

        var flagged = ScanInspectionSites("Mutation.cs",
            "class C { bool M(Expression e) => _info.GetExpressionType(e) is OptionalType; }",
            hierarchy, segments);
        flagged.Should().Contain(s => s.Kind == KindTypeName && s.Name == "OptionalType",
            "an `is OptionalType` test is the emitter inspecting a SemanticType");

        var handOff = ScanInspectionSites("Control.cs",
            "class C { TypeSyntax M(PropertySymbol p) => _typeMapper.MapSemanticType(p.CodeGenInfo.Type); }",
            hierarchy, segments);
        handOff.Should().BeEmpty(
            "passing a recorded type to TypeSyntaxMapper is the permitted shape, not an inspection.\nFlagged:\n  "
            + string.Join("\n  ", handOff));
    }

    /// <summary>
    /// Positive and negative controls for the three line/invocation kinds, so a scanner that
    /// stopped seeing a shape fails here before the allowlist silently goes empty.
    /// </summary>
    [Fact]
    public void SyntheticInspectionKinds_AreMeasured()
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        var source = string.Join("\n",
            "class C {",
            "  void M(Expression e, TypeSymbolLike t, Func f) {",
            "    var a = _info.GetExpressionType(e);",
            "    var b = GetExpressionSemanticType(e);",
            "    var m = t.Methods.FirstOrDefault(m => m.Name == f.Name && m.DeclarationLine == f.LineStart);",
            "    if (unionCase?.Name == \"Some\") { }",
            "    // var c = GetExpressionType(e); x.Where(m => m.Name == y); DeclarationLine",
            "    var d = decl.Decorators.Any(d => d.Name == \"dataclass\");",
            "  }",
            "}");
        var sites = ScanInspectionSites("Synthetic.cs", source, empty, empty);

        sites.Where(s => s.Kind == KindTypeRead).Select(s => s.Name).Should().BeEquivalentTo(
            new[] { "GetExpressionType", "GetExpressionSemanticType" }, "one site per accessor call, none in comments");
        sites.Where(s => s.Kind == KindNameLookup).Select(s => s.Name).Should().BeEquivalentTo(
            new[] { "predicate", "union-case-compare" },
            "a lambda Name predicate over a member collection and a union-case compare; the decorator "
            + "lookup and the comment are not counted");
        sites.Count(s => s.Kind == KindDeclarationLine).Should().Be(1, "one DeclarationLine reference outside comments");
    }

    /// <summary>True when <paramref name="kind"/> is not counted in <paramref name="file"/>.</summary>
    private static bool IsInspectionExempt(string file, string kind)
        => kind == KindTypeName && string.Equals(file, InspectionExemptFile, StringComparison.Ordinal);

    private static readonly Regex PredicateLookup = new(@"\b(\w+) =>\s*\1\.Name ==", RegexOptions.CultureInvariant);
    private static readonly Regex AstDeclarationContext =
        new(@"OfType<(Parser\.Ast\.)?VariableDeclaration>", RegexOptions.CultureInvariant);
    private static readonly Regex NonSymbolContext =
        new(@"OfType<|Decorators|KeywordArguments|\.Body\b|body\.", RegexOptions.CultureInvariant);
    private static readonly Regex UnionCaseCompare = new(
        @"\b\w+\??\.Name == (""(Some|Ok|Err|None)""|WellKnownCaseNames\.\w+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Every inspection site of one source, all four kinds, unfiltered by the exemption.
    /// <c>name-lookup</c> ports the W2.1 census generator (<c>w23.py</c>) —
    /// lines whose trimmed text starts with <c>//</c> or <c>*</c> are skipped, the window is the
    /// previous and current trimmed lines, a <c>VariableDeclaration</c> lookup counts (AST) and a
    /// decorator / keyword-argument / body lookup does not — so its counts are the census's.
    /// </summary>
    internal static List<InspectionSite> ScanInspectionSites(
        string fileName, string source, IReadOnlySet<string> hierarchy, IReadOnlySet<string> namespaceSegments)
    {
        var lines = source.Split('\n');
        string Text(int line) => line - 1 < lines.Length ? lines[line - 1].Trim() : string.Empty;
        int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        var sites = ScanSource(fileName, source, hierarchy, namespaceSegments)
            .Select(v => new InspectionSite(v.File, v.Line, KindTypeName, v.Type, v.Source))
            .ToList();

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var callee = call.Expression switch
            {
                SimpleNameSyntax simple => simple.Identifier.Text,
                MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
                _ => null,
            };
            if (callee is "GetExpressionType" or "GetExpressionSemanticType")
                sites.Add(new InspectionSite(fileName, LineOf(call), KindTypeRead, callee, Text(LineOf(call))));
        }

        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>()
                     .Where(n => n.Identifier.Text == "DeclarationLine"))
        {
            sites.Add(new InspectionSite(fileName, LineOf(name), KindDeclarationLine, "DeclarationLine", Text(LineOf(name))));
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
                continue;
            var code = line.Contains('"', StringComparison.Ordinal)
                ? line
                : line.Split("//", 2)[0];

            var window = string.Join(" ", lines[Math.Max(0, i - 1)..(i + 1)].Select(l => l.Trim()));
            // Every predicate in the window, not only the first (the census took the first, which
            // hides a line whose previous line also holds a predicate).
            var onThisLine = PredicateLookup.Matches(window)
                .Any(m => Regex.IsMatch(line, @"\b" + Regex.Escape(m.Groups[1].Value) + @"\.Name =="));
            if (onThisLine)
            {
                var context = string.Join(" ", lines[Math.Max(0, i - 2)..(i + 1)]);
                if (AstDeclarationContext.IsMatch(context) || !NonSymbolContext.IsMatch(context))
                    sites.Add(new InspectionSite(fileName, i + 1, KindNameLookup, "predicate", trimmed));
            }

            for (var k = UnionCaseCompare.Matches(code).Count; k > 0; k--)
                sites.Add(new InspectionSite(fileName, i + 1, KindNameLookup, "union-case-compare", trimmed));
        }

        return sites;
    }

    /// <summary>
    /// Parses <see cref="InspectionAllowlistFileName"/>: <c>#</c> lines and blank lines are
    /// comments; a row is <c>file | kind | name | count | cite=#N | note</c>. Malformed rows,
    /// unknown kinds, non-positive counts, a missing cite and duplicate keys are problems.
    /// </summary>
    private static List<InspectionRow> LoadInspectionAllowlist(out List<string> problems)
    {
        problems = new List<string>();
        var path = FindInspectionAllowlistPath();
        File.Exists(path).Should().BeTrue(
            $"CodeGen/{InspectionAllowlistFileName} is expected at {path}; its presence arms the ratchet");

        var rows = new List<InspectionRow>();
        var seen = new HashSet<(string, string, string)>();
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var text = lines[i].Trim();
            if (text.Length == 0 || text.StartsWith('#'))
                continue;

            var fields = text.Split('|').Select(f => f.Trim()).ToArray();
            if (fields.Length != 6)
            {
                problems.Add($"line {i + 1}: expected `file | kind | name | count | cite=#N | note`, got `{text}`");
                continue;
            }

            if (!InspectionKinds.Contains(fields[1]))
                problems.Add($"line {i + 1}: unknown kind `{fields[1]}` (kinds: {string.Join(", ", InspectionKinds)})");
            if (!int.TryParse(fields[3], out var count) || count <= 0)
                problems.Add($"line {i + 1}: count `{fields[3]}` must be a positive integer (delete a row at 0)");
            if (!Regex.IsMatch(fields[4], @"^cite=#\d+$"))
                problems.Add($"line {i + 1}: `{fields[4]}` must be `cite=#N` naming the class issue");
            if (!seen.Add((fields[0], fields[1], fields[2])))
                problems.Add($"line {i + 1}: duplicate row for {fields[0]} | {fields[1]} | {fields[2]}");

            rows.Add(new InspectionRow(i + 1, fields[0], fields[1], fields[2], count, fields[4], fields[5]));
        }

        return rows;
    }

    private static string FindInspectionAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "CodeGen", InspectionAllowlistFileName);
            if (File.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "CodeGen", InspectionAllowlistFileName));
    }

    // ---- the deny universe -------------------------------------------------------------------

    /// <summary>
    /// Every type declared in the compiler assembly's <c>Semantic</c> namespaces, enumerated by
    /// reflection. Enumerating rather than spelling is the point: a semantic type added tomorrow is
    /// in the universe tomorrow, with no guard edit and no way to forget.
    /// </summary>
    private static IReadOnlyList<Type> SemanticUniverse()
    {
        var assembly = typeof(SemanticInfo).Assembly;
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types
            .Where(t => t is not null)
            .Select(t => t!)
            .Where(t => t.Namespace is { } ns
                        && (ns == SemanticNamespaceRoot
                            || ns.StartsWith(SemanticNamespaceRoot + ".", StringComparison.Ordinal)))
            // Compiler-generated closures, iterator state machines and anonymous types are not
            // names anybody can write in source.
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// The deny set: every type outside the carrier closure, plus every type in the
    /// inspection-denied hierarchies whether or not the closure reached it.
    /// </summary>
    private static HashSet<string> DeniedNames(IReadOnlyList<Type> universe)
    {
        var allowed = AllowedCarrierNames(universe);
        var denied = universe.Select(SimpleName).Where(n => !allowed.Contains(n)).ToHashSet(StringComparer.Ordinal);
        denied.UnionWith(InspectionDeniedNames(universe));
        return denied;
    }

    /// <summary>
    /// Every type assignable to an <see cref="InspectionDeniedHierarchyRoots"/> root, enumerated by
    /// reflection — a <c>SemanticType</c> added tomorrow is inspection-denied tomorrow.
    /// </summary>
    private static HashSet<string> InspectionDeniedNames(IReadOnlyList<Type> universe)
    {
        var byName = universe.ToLookup(SimpleName, StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in InspectionDeniedHierarchyRoots)
        {
            var rootType = byName[root].FirstOrDefault();
            rootType.Should().NotBeNull(
                $"'{root}' is an inspection-denied hierarchy root (#2443); if it was renamed or moved "
                + "out of the Semantic namespaces this guard is derived from a type that no longer exists");
            names.UnionWith(universe.Where(t => rootType!.IsAssignableFrom(t)).Select(SimpleName));
        }

        return names;
    }

    /// <summary>
    /// The namespace path segments of the deny universe (<c>Sharpy</c>, <c>Compiler</c>,
    /// <c>Semantic</c>, <c>Registry</c>, …). A dotted qualifier ending in one of these is a
    /// namespace, so what follows it is a TYPE reference and gets checked; any other qualifier is
    /// a value and what follows it is a member read.
    /// </summary>
    private static HashSet<string> NamespaceSegments(IReadOnlyList<Type> universe)
        => universe
            .Select(t => t.Namespace!)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(ns => ns.Split('.'))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The source-writable name of a type: <c>List`1</c> is written <c>List</c>.</summary>
    private static string SimpleName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick >= 0 ? name[..tick] : name;
    }

    // ---- the structural allow-list -----------------------------------------------------------

    /// <summary>
    /// The materialized-fact carriers, derived from the type system rather than spelled: the
    /// <see cref="CarrierHierarchyRoots"/> hierarchies (none since #2443), the standalone
    /// symbol records, <c>SemanticInfo</c>, <c>CodeGenInfo</c> and <c>SemanticBinding</c> — closed transitively over the
    /// semantic types their public signatures expose, so every node-keyed fact record and the
    /// enums it carries are allowed the moment they are reachable from a carrier. The catalog
    /// entries are added as LEAVES: allowed themselves, but not expanded, because a catalog's
    /// return types are not thereby carriers.
    /// </summary>
    private static HashSet<string> AllowedCarrierNames(IReadOnlyList<Type> universe)
    {
        var byName = universe.ToLookup(SimpleName, StringComparer.Ordinal);
        var inUniverse = new HashSet<Type>(universe);
        var catalogNames = ReadOnlyCatalogs.Select(c => c.Type).ToHashSet(StringComparer.Ordinal);

        var seeds = new List<Type>();
        foreach (var root in CarrierHierarchyRoots)
        {
            var rootType = byName[root].FirstOrDefault();
            rootType.Should().NotBeNull(
                $"'{root}' is a carrier hierarchy root Critical Rule 2 names; if it was renamed or "
                + "moved out of the Semantic namespaces this guard is derived from a type that no "
                + "longer exists");
            seeds.AddRange(universe.Where(t => rootType!.IsAssignableFrom(t)));
        }

        foreach (var seed in CarrierSeeds.Concat(catalogNames))
        {
            var type = byName[seed].FirstOrDefault();
            type.Should().NotBeNull($"carrier/catalog '{seed}' is expected in the Semantic namespaces");
            seeds.Add(type!);
        }

        var allowed = new HashSet<Type>();
        var queue = new Queue<Type>(seeds);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!allowed.Add(type))
                continue;

            // Catalogs are leaves: allowed, but their signatures do not widen the allow-list.
            if (catalogNames.Contains(SimpleName(type)))
                continue;

            foreach (var referenced in SignatureTypes(type))
            {
                if (inUniverse.Contains(referenced) && !allowed.Contains(referenced))
                    queue.Enqueue(referenced);
            }
        }

        return allowed.Select(SimpleName).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The types a carrier exposes on its own surface: property, field, method and constructor
    /// signatures, implemented interfaces and nested types. Public and internal members both count
    /// — CodeGen lives in the same assembly, so internal IS the surface it can read.
    /// </summary>
    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var property in type.GetProperties(Flags).Where(p => !IsPrivate(p)))
        {
            foreach (var unwrapped in Unwrap(property.PropertyType))
                yield return unwrapped;
        }

        foreach (var field in type.GetFields(Flags).Where(f => !f.IsPrivate))
        {
            foreach (var unwrapped in Unwrap(field.FieldType))
                yield return unwrapped;
        }

        foreach (var method in type.GetMethods(Flags).Where(m => !m.IsPrivate))
        {
            foreach (var unwrapped in Unwrap(method.ReturnType))
                yield return unwrapped;
            foreach (var parameter in method.GetParameters())
            {
                foreach (var unwrapped in Unwrap(parameter.ParameterType))
                    yield return unwrapped;
            }
        }

        foreach (var constructor in type.GetConstructors(Flags).Where(c => !c.IsPrivate))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var unwrapped in Unwrap(parameter.ParameterType))
                    yield return unwrapped;
            }
        }

        foreach (var iface in type.GetInterfaces())
        {
            foreach (var unwrapped in Unwrap(iface))
                yield return unwrapped;
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!nested.IsNestedPrivate)
                yield return nested;
        }
    }

    private static bool IsPrivate(PropertyInfo property)
        => (property.GetMethod?.IsPrivate ?? true) && (property.SetMethod?.IsPrivate ?? true);

    /// <summary>Peels arrays, by-ref/pointer wrappers and generic arguments off a signature type.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            var element = type.GetElementType();
            if (element is not null)
            {
                foreach (var unwrapped in Unwrap(element))
                    yield return unwrapped;
            }

            yield break;
        }

        yield return type;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var unwrapped in Unwrap(argument))
                    yield return unwrapped;
            }
        }
    }

    // ---- the scan ----------------------------------------------------------------------------

    internal readonly record struct Violation(string File, int Line, string Type, string Source)
    {
        public override string ToString() => $"{File}:{Line} — {Type} — {Source}";
    }

    private static List<Violation> ScanCodeGenSources(
        IReadOnlySet<string> denied, IReadOnlySet<string> namespaceSegments)
    {
        var directory = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        Directory.Exists(directory).Should().BeTrue(
            $"CodeGen source directory should exist at {directory}");

        var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("Should find CodeGen source files");

        return files
            .OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(f => ScanSource(Path.GetFileName(f), File.ReadAllText(f), denied, namespaceSegments))
            .ToList();
    }

    /// <summary>
    /// Every semantic type name CodeGen references — the measurement the catalog entries are
    /// justified against.
    /// </summary>
    private static HashSet<string> ReferencedSemanticNames(
        IReadOnlySet<string> universeNames, IReadOnlySet<string> namespaceSegments)
        => ScanCodeGenSources(universeNames, namespaceSegments)
            .Select(v => v.Type)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Flags every identifier NODE naming a denied type. Nodes, not tokens: declaration names are
    /// raw tokens (so a local called <c>scope</c> is not a reference) and comments are trivia (so a
    /// doc-comment <c>&lt;see cref&gt;</c> is not one either) — both fall out of the syntax model
    /// instead of needing the predecessor scan's hand-rolled comment stripping.
    ///
    /// <para><b>Mutation-tested (2026-08-14, HEAD <c>e351d2832</c>).</b> Forcing
    /// <see cref="IsInstanceMemberRead"/> to <c>return false</c> — over-flagging every member read
    /// — turns <see cref="CarrierOnlySource_IsNotFlagged"/> RED while the other five stay green,
    /// so the negative control is a live wire and not a vacuous pass. Reverted. Note what else
    /// that run established: the whole-corpus sweep stayed GREEN under the mutation, which
    /// measures that no CodeGen source today reaches a denied type through a value-qualified
    /// member read — the one shape this scan's documented residual cannot see.</para>
    /// </summary>
    internal static List<Violation> ScanSource(
        string fileName, string source, IReadOnlySet<string> denied, IReadOnlySet<string> namespaceSegments)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var lines = source.Split('\n');
        var violations = new List<Violation>();

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var identifier = name.Identifier.Text;
            if (!denied.Contains(identifier))
                continue;
            if (IsInstanceMemberRead(name, namespaceSegments))
                continue;

            var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var text = line - 1 < lines.Length ? lines[line - 1].Trim() : string.Empty;
            violations.Add(new Violation(fileName, line, identifier, text));
        }

        return violations;
    }

    /// <summary>
    /// True when this name is the member half of a member access whose qualifier is a VALUE
    /// (<c>ctx.Scope</c>) rather than a namespace (<c>Semantic.TypeSubstitution</c>). Without a
    /// semantic model the two are the same shape, and the qualifier's last segment is what
    /// separates them: it is a namespace path segment in exactly the second case.
    /// </summary>
    private static bool IsInstanceMemberRead(SimpleNameSyntax name, IReadOnlySet<string> namespaceSegments)
    {
        if (name.Parent is not MemberAccessExpressionSyntax access || access.Name != name)
            return false;

        var lastSegment = access.Expression switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.Text,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
            _ => null,
        };

        return lastSegment is null || !namespaceSegments.Contains(lastSegment);
    }
}
