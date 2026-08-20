using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// names: the <c>SemanticType</c> hierarchy, the <c>Symbol</c> hierarchy, the standalone symbol
/// records, <c>SemanticInfo</c> and <c>CodeGenInfo</c>. From those it takes the transitive closure
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
    /// Structural seeds: the materialized-fact carriers Critical Rule 2 names. Hierarchy roots are
    /// expanded to every derived type; the standalone symbol records are not <c>Symbol</c>
    /// subclasses and so are listed.
    /// </summary>
    private static readonly string[] CarrierHierarchyRoots = { "SemanticType", "Symbol" };

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
    /// Denied types CodeGen still names, with the measured site count per file and the issue that
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
    {
        ("RoslynEmitter.TypeDeclarations.cs", "SynthesisAnalyzer", 1, "#1521"),
        ("RoslynEmitter.TypeDeclarations.cs", "SynthesizedInterfaceInfo", 1, "#1521"),
    };

    // ---- the guard ---------------------------------------------------------------------------

    [Fact]
    public void CodeGenSources_NameOnlyMaterializedFactCarriers()
    {
        var universe = SemanticUniverse();
        var allowed = AllowedCarrierNames(universe);
        var denied = universe.Select(SimpleName).Where(n => !allowed.Contains(n)).ToHashSet(StringComparer.Ordinal);

        // A deny universe that collapsed to nothing would pass for free — the failure mode a guard
        // must not have (the same shape as an empty-corpus sweep).
        denied.Should().HaveCountGreaterThan(50,
            "the deny universe is every non-carrier type in the Semantic namespaces; if it shrank "
            + "to a handful, the structural allow-list has swallowed the assembly and this guard "
            + "no longer guards anything");

        var violations = ScanCodeGenSources(denied, NamespaceSegments(universe));

        _output.WriteLine(
            $"Semantic universe: {universe.Count} types — {allowed.Count} allowed carriers, "
            + $"{denied.Count} denied. Violations: {violations.Count}.");
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
        var allowed = AllowedCarrierNames(universe);
        var denied = universe.Select(SimpleName).Where(n => !allowed.Contains(n)).ToHashSet(StringComparer.Ordinal);
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
            ("carrier reads",
                @"class C {
                    ExpressionSyntax M(Expression e, SemanticInfo info, Symbol s) {
                        SemanticType? t = info.GetExpressionType(e);
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

    private static HashSet<string> DeniedNames(IReadOnlyList<Type> universe)
    {
        var allowed = AllowedCarrierNames(universe);
        return universe.Select(SimpleName).Where(n => !allowed.Contains(n)).ToHashSet(StringComparer.Ordinal);
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
    /// <c>SemanticType</c> and <c>Symbol</c> hierarchies (every derived type), the standalone
    /// symbol records, <c>SemanticInfo</c> and <c>CodeGenInfo</c> — closed transitively over the
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
