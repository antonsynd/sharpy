extern alias SharpyRT;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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
public class CoreGenericTypeMappingConformanceTests
{
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
        var bridge = new ClrTypeBridge();
        var stillDegrading = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in PublicCoreGenericDefinitions())
        {
            if (!TryClose(definition, out var closed))
                continue;

            if (IsDegraded(bridge.MapClrTypeToSemanticType(closed)))
                stillDegrading.Add(NameWithoutArity(definition));
        }

        var stale = Allowlist.Keys.Where(k => !stillDegrading.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "each allowlist entry must name a Sharpy.Core generic that still maps to `object`; "
            + "delete entries whose type is gone or has gained a MapGenericType arm.\nStale:\n"
            + string.Join("\n", stale));
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
        var registry = new BuiltinRegistry();
        var stillUnresolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (sharpyName, symbol) in registry.RegisteredTypes)
        {
            var clrType = symbol.ClrType;
            if (clrType?.Namespace is not ClrTypeBridge.SpecialCases.SharpyNamespace)
                continue;

            var declared = clrType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Count(m => m.IsSpecialName
                    && m.Name.StartsWith("op_", StringComparison.Ordinal)
                    && global::Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder.IsDunderMappableOperator(m.Name));

            if (declared > 0 && symbol.OperatorMethods.Count == 0)
                stillUnresolved.Add(sharpyName);
        }

        var stale = OperatorResolutionAllowlist.Keys.Where(k => !stillUnresolved.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "each operator-allowlist entry must name a registered builtin that STILL declares "
            + "dunder-mappable CLR operators and still resolves none; delete entries whose cause is "
            + "gone.\nStale:\n" + string.Join("\n", stale));
    }

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

            // Only operators discovery can MAP to a dunder can be "lost". Conversion operators
            // (op_Implicit / op_Explicit) have no dunder, so counting them made the guard report a
            // gap for a type that had none — see the allowlist note (#1310).
            var declared = clrType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Count(m => m.IsSpecialName
                    && m.Name.StartsWith("op_", StringComparison.Ordinal)
                    && global::Sharpy.Compiler.Discovery.Caching.OverloadIndexBuilder.IsDunderMappableOperator(m.Name));
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
    {
        closed = null!;
        var arity = definition.GetGenericArguments().Length;
        foreach (var placeholder in new[] { typeof(int), typeof(string), typeof(object) })
        {
            try
            {
                closed = definition.MakeGenericType(Enumerable.Repeat(placeholder, arity).ToArray());
                return true;
            }
            catch (ArgumentException)
            {
                // constraint violation — try the next placeholder
            }
        }

        return false;
    }

    private static string NameWithoutArity(Type definition)
    {
        var name = definition.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }
}
