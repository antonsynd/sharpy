extern alias SharpyRT;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Conformance guard over <see cref="ClrTypeBridge.MapClrTypeToSemanticType"/>: every public
/// generic type in <c>Sharpy.Core</c> must map to a usable <see cref="SemanticType"/>, or appear on
/// a justified allowlist (#1210).
///
/// <para>
/// <c>MapGenericType</c> is a hand-written whitelist of generic definitions whose tail is
/// <c>// Unknown generic type - fallback to object</c>. Any Sharpy.Core generic outside it silently
/// degrades to <c>object</c> the moment CLR discovery returns one — which is exactly how
/// <c>frozen_set(xs)</c> typed as <c>object</c> and had <c>len()</c>, iteration and membership all
/// rejected. The degradation is silent by construction: nothing fails until a user calls a method
/// on the result.
/// </para>
///
/// <para>
/// <b>Scope (deliberate):</b> this guard reports the class; it does not fix it. Entries here are
/// expected — several Sharpy.Core generics are internal plumbing that never surfaces as a value
/// type in user code. Per CLAUDE.md's gap-discovery contract each entry cites an issue and is
/// deleted when that issue is fixed, so the list must trend to empty rather than absorb new
/// degradations.
/// </para>
/// </summary>
public class CoreGenericTypeMappingConformanceTests : IDisposable
{
    /// <summary>
    /// Private cache directory for the <see cref="CachedModuleDiscovery"/> instances the provenance
    /// fact drives. Nothing is written to it — <c>ConvertTypeSignature</c> reads no index — but the
    /// cache constructor creates its directory, and pointing it at a temp path keeps this suite out
    /// of the shared <c>~/.sharpy/cache</c> that parallel runs contend on.
    /// </summary>
    private readonly string _cacheDirectory =
        Path.Combine(Path.GetTempPath(), "sharpy-test-cache", Guid.NewGuid().ToString());

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
                Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort; a leaked temp directory is not a test failure.
        }
    }

    /// <summary>
    /// Types whose <c>object</c> mapping is accepted for a stated reason. Key = type name without
    /// arity; value = the reason, which must cite an issue when it is a defect rather than a
    /// deliberate exclusion.
    /// </summary>
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // FrozenSet's entry was DRAINED by #1253, on exactly the condition the entry itself named:
        // "if a future factory returns either type, this entry stops being true and the
        // MapGenericType arm has to be written." Operator discovery turned out to consult the
        // CLR-return mapping too — Sharpy.FrozenSet<T>'s operator signatures could not map back to
        // `frozenset`, which is half of why frozenset refused every operator — so the arm is now
        // written and the mapping no longer degrades to object.
        //
        // FrozenDict's entry was DRAINED by the IOrderedEnumerable/ICollection/FrozenDict batch
        // (#1310): the SharpyFrozenDictFullName arm now maps Sharpy.FrozenDict<K,V> to frozendict[K,V],
        // so the mapping no longer degrades to object. Same-type frozendict | frozendict is the first
        // operator fixture.

        // IReverseEnumerable's entry was DRAINED by #1242: MapGenericType now maps Sharpy.Core's own
        // generic interfaces to a real GenericType with GenericDefinition set, so it no longer
        // degrades and the stale-entry test below would fail if the entry were kept.
        //
        // The investigation that entry promised, recorded here because its conclusion is the opposite
        // of what #1242 predicted: the explicit cast the emitter writes at reversed() call sites is
        // NOT a consequence of this degradation and did not become removable. It breaks a genuine
        // C#-level ambiguity (CS0121) between Builtins.Reversed<T>(IEnumerable<T>) and
        // Reversed<T>(IReverseEnumerable<T>) when a user class implements both — a fact about the
        // GENERATED code, which no CLR-to-semantic mapping change can affect. Verified by deleting
        // the cast and observing CS0121; see the comment at that call site.
        //
        // ISized and IBoolConvertible are non-generic, so they never reach this guard, which only
        // enumerates generic definitions. That is the answer #1242 asked for: not "they are fine",
        // but "this guard cannot speak to them".
    };

    [Fact]
    public void PublicCoreGenerics_MapToUsableSemanticTypes()
    {
        var bridge = new ClrTypeBridge();
        var degraded = new List<string>();

        foreach (var definition in PublicCoreGenericDefinitions())
        {
            if (!TryClose(definition, out var closed))
                continue; // constraints we cannot satisfy with a placeholder; not a mapping question

            var mapped = bridge.MapClrTypeToSemanticType(closed);
            if (!IsDegraded(mapped))
                continue;

            var name = NameWithoutArity(definition);
            if (Allowlist.ContainsKey(name))
                continue;

            degraded.Add($"{name}<…> -> {mapped.GetDisplayName()}  [{definition.FullName}]");
        }

        degraded.Should().BeEmpty(
            "a Sharpy.Core generic that maps to `object` degrades silently — nothing fails until a "
            + "user calls a method on the result, which is how #1210 surfaced. Add a MapGenericType "
            + "arm, or add the type to the Allowlist with a reason and an issue reference.\n"
            + "Degraded:\n" + string.Join("\n", degraded));
    }

    /// <summary>
    /// The allowlist must not rot: every entry has to name a type that still exists and still
    /// degrades. An entry that outlives its cause hides the next degradation behind the same name.
    /// </summary>
    [Fact]
    public void Allowlist_HasNoStaleEntries()
    {
        var stale = StaleMappingAllowlistEntries(Allowlist);

        stale.Should().BeEmpty(
            "each allowlist entry must name a Sharpy.Core generic that still maps to `object`; "
            + "delete entries whose type is gone or has gained a MapGenericType arm.\nStale:\n"
            + string.Join("\n", stale));
    }

    /// <summary>
    /// Positive control for the check above. <see cref="Allowlist"/> is empty — every entry was
    /// drained — so <see cref="Allowlist_HasNoStaleEntries"/> now iterates nothing and would pass
    /// against a staleness rule that had been deleted outright. This feeds the same rule a synthetic
    /// entry that IS stale and asserts it trips.
    /// </summary>
    /// <remarks>
    /// The synthetic key is a real Sharpy.Core generic that maps cleanly rather than a nonsense
    /// string, so the control exercises the actual distinction — "no longer degrades" vs "still
    /// degrades" — instead of merely "unknown name". There is deliberately no negative control
    /// alongside it: nothing in Sharpy.Core degrades any more (that is what
    /// <see cref="PublicCoreGenerics_MapToUsableSemanticTypes"/> asserts against an empty allowlist),
    /// so a still-degrading entry cannot be constructed from a real type today.
    /// </remarks>
    [Fact]
    public void AllowlistStalenessRule_TripsOnAStaleEntry()
    {
        var synthetic = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["List"] = "synthetic: Sharpy.List<T> has a MapGenericType arm and never degrades"
        };

        StaleMappingAllowlistEntries(synthetic).Should().ContainSingle()
            .Which.Should().Be(
                "List",
                "an entry naming a type that maps cleanly is by definition stale; if this does not "
                + "trip, Allowlist_HasNoStaleEntries is passing because it checks nothing");
    }

    /// <summary>
    /// The staleness rule itself, parameterized on the allowlist so the real (empty) one and a
    /// synthetic stale one run through identical code. An entry is stale when the type it names no
    /// longer maps to <c>object</c>.
    /// </summary>
    private static IReadOnlyList<string> StaleMappingAllowlistEntries(
        IReadOnlyDictionary<string, string> allowlist)
    {
        var bridge = new ClrTypeBridge();
        var stillDegrading = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in PublicCoreGenericDefinitions())
        {
            if (!TryClose(definition, out var closed))
                continue;

            if (IsDegraded(bridge.MapClrTypeToSemanticType(closed)))
                stillDegrading.Add(NameWithoutArity(definition));
        }

        return allowlist.Keys.Where(k => !stillDegrading.Contains(k)).ToList();
    }

    /// <summary>
    /// The second half of the same class of defect: a builtin type has to be registered in
    /// <b>three</b> places, and the guard above only covers one of them (#1253, umbrella #1145).
    /// </summary>
    /// <remarks>
    /// A builtin collection is registered in <c>BuiltinRegistry.RegisterType</c>, needs an entry in
    /// <c>CachedModuleDiscovery.SharpyToClrNameMap</c> whenever its Sharpy name differs from its CLR
    /// name, and needs an arm in <c>ClrTypeBridge.MapClrTypeToSemanticType</c>. Miss the second and
    /// discovery searches for a CLR type named <c>frozenset</c>, finds nothing, and the registered
    /// symbol gets an empty <c>OperatorMethods</c> — which is why <c>frozenset</c> refused every
    /// operator including <c>==</c> while its methods worked fine.
    /// <para>
    /// The check is "declares operators in CLR ⇒ has them after discovery", because that is the
    /// property the missing name-map entry actually destroys, and it cannot be satisfied by a
    /// name-map entry that points somewhere wrong.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Registered builtins known to resolve zero CLR operators, each against an open issue. Same
    /// drain-on-fix contract as <see cref="Allowlist"/>: delete the entry when the issue closes.
    /// </summary>
    private static readonly Dictionary<string, string> OperatorResolutionAllowlist = new(StringComparer.Ordinal)
    {
        // EMPTY, and both entries were drained rather than re-explained (#1310).
        //
        // frozendict's went with the name-map + bridge arm: discovery resolves its operators
        // through the frozendict → FrozenDict entry.
        //
        // Complex's turned out not to be a gap at all. It "declared 2 operators and resolved none"
        // because the guard counted every op_ method a type declares — and Complex's two are both
        // `implicit operator` conversions (op_Implicit), which ClrOperatorToDunder has no mapping
        // for and discovery never attempts. Counting only dunder-mappable operators drops Complex
        // legitimately; the measurement was wrong, not the pipeline.
    };

    /// <summary>
    /// The operator allowlist must not rot either — the check its sibling had and it did not
    /// (#1310). An entry that outlives its cause hides the next lost operator behind the same name,
    /// which is precisely how Complex's entry survived: nobody re-measured whether it was still a
    /// gap, and it never had been.
    /// </summary>
    [Fact]
    public void OperatorResolutionAllowlist_HasNoStaleEntries()
    {
        var stale = StaleOperatorAllowlistEntries(OperatorResolutionAllowlist);

        stale.Should().BeEmpty(
            "each operator-allowlist entry must name a registered builtin that STILL declares "
            + "dunder-mappable CLR operators and still resolves none; delete entries whose cause is "
            + "gone.\nStale:\n" + string.Join("\n", stale));
    }

    /// <summary>
    /// Positive control for the check above, for the same reason its sibling has one:
    /// <see cref="OperatorResolutionAllowlist"/> is empty, so the real check iterates nothing.
    /// </summary>
    /// <remarks>
    /// This codifies by test what #1310's commit message claimed had been verified by hand — the
    /// mutation that proves the guard can fail. Hand-verification leaves no artifact and does not
    /// survive the next refactor; that is precisely how Complex's entry outlived a cause it never
    /// had.
    /// </remarks>
    [Fact]
    public void OperatorResolutionStalenessRule_TripsOnAStaleEntry()
    {
        var synthetic = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [BuiltinNames.List] = "synthetic: list resolves its CLR operators, so it is not a gap"
        };

        StaleOperatorAllowlistEntries(synthetic).Should().ContainSingle()
            .Which.Should().Be(
                BuiltinNames.List,
                "an entry naming a builtin that resolves its operators is by definition stale; if "
                + "this does not trip, OperatorResolutionAllowlist_HasNoStaleEntries checks nothing");
    }

    /// <summary>
    /// The operator staleness rule, parameterized on the allowlist so the real (empty) one and a
    /// synthetic stale one run through identical code. An entry is stale when the builtin it names
    /// no longer both declares dunder-mappable CLR operators and resolves none of them.
    /// </summary>
    private static IReadOnlyList<string> StaleOperatorAllowlistEntries(
        IReadOnlyDictionary<string, string> allowlist)
    {
        var registry = new BuiltinRegistry();
        var stillUnresolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (sharpyName, symbol) in registry.RegisteredTypes)
        {
            var clrType = symbol.ClrType;
            if (clrType?.Namespace is not ClrTypeBridge.SpecialCases.SharpyNamespace)
                continue;

            if (DunderMappableOperatorCount(clrType) > 0 && symbol.OperatorMethods.Count == 0)
                stillUnresolved.Add(sharpyName);
        }

        return allowlist.Keys.Where(k => !stillUnresolved.Contains(k)).ToList();
    }

    /// <summary>
    /// Operators discovery can MAP to a dunder — the only ones that can be "lost". Conversion
    /// operators (op_Implicit / op_Explicit) have no dunder, so counting them made the guard report a
    /// gap for a type that had none (#1310). Shared by the staleness rule and the gap sweep so the
    /// two cannot drift into disagreeing about what counts.
    /// </summary>
    private static int DunderMappableOperatorCount(Type clrType)
        => clrType
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Count(m => m.IsSpecialName
                && m.Name.StartsWith("op_", StringComparison.Ordinal)
                && OverloadIndexBuilder.IsDunderMappableOperator(m.Name));

    [Fact]
    public void EveryRegisteredBuiltinTypeResolvesItsClrOperators()
    {
        var registry = new BuiltinRegistry();
        var gaps = new List<string>();

        foreach (var (sharpyName, symbol) in registry.RegisteredTypes.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var clrType = symbol.ClrType;
            if (clrType is null)
                continue;

            // Scope: Sharpy.Core's own types, which are the ones whose operators reach the
            // TypeChecker through CLR discovery. Primitives (int/float/decimal, backed by
            // System.Int32/Double/Decimal) are owned by PrimitiveCatalog and resolved by
            // TryInferBuiltinBinaryOp's numeric arm, so discovery resolving none of their 37
            // System.Decimal operators is correct rather than a gap — including them would make
            // this guard report noise it cannot act on.
            if (clrType.Namespace is not ClrTypeBridge.SpecialCases.SharpyNamespace)
                continue;

            // Only operators discovery can MAP to a dunder can be "lost" — see
            // DunderMappableOperatorCount and the allowlist note (#1310).
            var declared = DunderMappableOperatorCount(clrType);
            if (declared == 0)
                continue;

            if (symbol.OperatorMethods.Count == 0 && !OperatorResolutionAllowlist.ContainsKey(sharpyName))
            {
                gaps.Add($"{sharpyName} ({clrType.Name}): declares {declared} CLR operator(s), "
                    + "discovery resolved 0 — missing SharpyToClrNameMap entry?");
            }
        }

        gaps.Should().BeEmpty(
            "a registered builtin whose CLR type declares operators must resolve them; a missing "
            + "SharpyToClrNameMap entry silently yields an operator-less symbol.\nGaps:\n"
            + string.Join("\n", gaps));
    }

    /// <summary>
    /// The fourth fact: the compiler has <b>two</b> producers of
    /// <see cref="GenericType.ClrOriginTypeName"/>, and they must spell the same CLR definition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ClrTypeBridge"/> stamps <c>GetGenericTypeDefinition().FullName</c> directly.
    /// <c>CachedModuleDiscovery.ConvertTypeSignature</c> reconstructs the stamp from the cached
    /// <see cref="TypeSignature.ClrTypeName"/>, which <see cref="OverloadIndexBuilder"/> writes as the
    /// definition's <c>AssemblyQualifiedName</c>, by cutting at the first depth-0 comma
    /// (<c>ClrNameHelper.ToFullClrName</c>). Two independent derivations of one string: if they ever
    /// diverge, every warm-cache formal silently stops matching the assignability rule that reads the
    /// stamp, and nothing fails until a user's call to a .NET method is refused.
    /// </para>
    /// <para>
    /// #1294 fixed exactly that class by hand and left it untested — its preservation one-liner in
    /// <c>TypeSubstitution</c> could be reverted without failing a single test, which is the inert-fix
    /// failure mode that had already bitten the issue once. This fact is the defense: it drives both
    /// producers over the same CLR types and compares their output.
    /// </para>
    /// </remarks>
    [Fact]
    public void BothProvenanceProducers_SpellTheSameClrDefinition()
    {
        var bridge = new ClrTypeBridge();
        var builder = new OverloadIndexBuilder();
        var discovery = new CachedModuleDiscovery(new OverloadIndexCache(_cacheDirectory));

        var arms = ClrBridgeArmProbe.DiscoverCollapsingArms(bridge);

        // Positive control on the probe itself. Every assertion below is over `arms`, so an empty or
        // shrunken arm set would make the whole fact pass vacuously — the exact way a conformance
        // sweep dies quietly. The named arms must be FOUND by the probe, not assumed to be in it.
        var probed = arms
            .Select(a => a.Closed.GetGenericTypeDefinition().FullName!)
            .ToHashSet(StringComparer.Ordinal);
        ClrBridgeArmProbe.RequiredArms.Where(r => !probed.Contains(r)).Should().BeEmpty(
            "the probe below discovers the bridge's collapsing arms by mapping candidate CLR generics "
            + "rather than listing them, so it picks up arms added later for free — but a probe that "
            + "stops finding the arms we already know about is broken, not passing.\nProbed "
            + $"{arms.Count} arm(s).");

        var disagreements = new List<string>();

        foreach (var (closed, viaBridge) in arms)
        {
            var viaDiscovery = discovery.ConvertTypeSignature(builder.CreateTypeSignature(closed));

            if (viaDiscovery is not GenericType discoveryGeneric)
            {
                disagreements.Add(
                    $"{closed.GetGenericTypeDefinition().FullName}: bridge -> "
                    + $"{viaBridge.GetDisplayName()}, discovery -> {viaDiscovery.GetDisplayName()} "
                    + $"({viaDiscovery.GetType().Name}, which carries no provenance at all)");
                continue;
            }

            if (!string.Equals(discoveryGeneric.Name, viaBridge.Name, StringComparison.Ordinal))
            {
                disagreements.Add(
                    $"{closed.GetGenericTypeDefinition().FullName}: bridge names it "
                    + $"'{viaBridge.Name}', discovery names it '{discoveryGeneric.Name}'");
            }

            if (!string.Equals(
                    discoveryGeneric.ClrOriginTypeName, viaBridge.ClrOriginTypeName, StringComparison.Ordinal))
            {
                disagreements.Add(
                    $"{closed.GetGenericTypeDefinition().FullName}: bridge stamps "
                    + $"'{viaBridge.ClrOriginTypeName ?? "<null>"}', discovery stamps "
                    + $"'{discoveryGeneric.ClrOriginTypeName ?? "<null>"}'");
            }
        }

        disagreements.Should().BeEmpty(
            "ClrTypeBridge and CachedModuleDiscovery.ConvertTypeSignature both produce "
            + "GenericType.ClrOriginTypeName, and ClrTypeHelper.ClrOriginIsSatisfiedBy compares that "
            + "string against a CLR definition name. A formal that arrives from the warm cache spelled "
            + "differently from one the bridge just mapped stops satisfying the same calls, with no "
            + "diagnostic.\nDisagreements:\n" + string.Join("\n", disagreements));
    }

    /// <summary>
    /// The stamp has to survive <see cref="TypeSubstitution"/>, because a generic .NET formal only
    /// reaches assignability AFTER its type parameters are substituted at the call site.
    /// </summary>
    /// <remarks>
    /// Deliberately end-to-end over the real producers rather than a hand-built
    /// <see cref="GenericType"/>: <c>Sharpy.List&lt;T&gt;.Extend(IEnumerable&lt;T&gt;)</c> is exactly
    /// the shape at issue — an open-constructed CLR formal that collapses to <c>list[T]</c> stamped
    /// with <c>IEnumerable`1</c>. <c>TypeSubstitution.Apply</c> rebuilds every GenericType it touches,
    /// so a rebuild that forgets to copy the stamp erases provenance for every generic call site
    /// while leaving the type otherwise identical (#1294).
    /// </remarks>
    [Fact]
    public void Substitution_PreservesTheStampBothProducersAgreedOn()
    {
        var builder = new OverloadIndexBuilder();
        var discovery = new CachedModuleDiscovery(new OverloadIndexCache(_cacheDirectory));

        var openFormal = typeof(SharpyRT::Sharpy.List<>)
            .GetMethod(nameof(SharpyRT::Sharpy.List<int>.Extend))!
            .GetParameters()[0]
            .ParameterType;

        var formal = discovery
            .ConvertTypeSignature(builder.CreateTypeSignature(openFormal))
            .Should().BeOfType<GenericType>().Subject;

        // Precondition, asserted rather than assumed: if the formal were unstamped on arrival the
        // substitution assertion below would hold for the wrong reason.
        formal.ClrOriginTypeName.Should().Be(
            typeof(IEnumerable<>).FullName,
            "the discovery path must stamp the formal before substitution ever sees it");
        formal.TypeArguments.Should().ContainSingle().Which.Should().BeOfType<TypeParameterType>();

        var substituted = TypeSubstitution.Apply(
            formal,
            new Dictionary<string, SemanticType> { ["T"] = SemanticType.Int });

        var substitutedGeneric = substituted.Should().BeOfType<GenericType>().Subject;
        substitutedGeneric.TypeArguments.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
        substitutedGeneric.ClrOriginTypeName.Should().Be(
            formal.ClrOriginTypeName,
            "substitution rebuilds the GenericType; dropping the stamp there would make every "
            + "instantiated .NET formal indistinguishable from a list[T] the user wrote, which is the "
            + "refusal #1260/#1294 fixed");
    }

    private static bool IsDegraded(SemanticType mapped)
        => ReferenceEquals(mapped, SemanticType.Object) || mapped is UnknownType;

    private static IEnumerable<Type> PublicCoreGenericDefinitions()
        => typeof(SharpyRT::Sharpy.List<>).Assembly
            .GetExportedTypes()
            // Nested types (a collection's Enumerator struct) are implementation details of their
            // container and never surface as a Sharpy value, so they are out of scope rather than
            // allowlist noise.
            .Where(t => t.IsGenericTypeDefinition && !t.IsNested)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    /// <summary>
    /// Closes a generic definition with placeholder arguments. Returns false when no placeholder
    /// satisfies the constraints — that is a reflection limitation, not a mapping verdict.
    /// </summary>
    private static bool TryClose(Type definition, out Type closed)
        => ClrBridgeArmProbe.TryClose(definition, out closed);

    private static string NameWithoutArity(Type definition)
    {
        var name = definition.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }
}
