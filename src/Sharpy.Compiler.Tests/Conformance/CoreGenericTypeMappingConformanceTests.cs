extern alias SharpyRT;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
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
        // Reachable only as registered builtin types, so the CLR-return mapping is never consulted
        // for them: frozenset construction goes through CheckConstructorCall and frozendict has no
        // static factory at all. #1210 retired the one factory that made this reachable
        // (Builtins.FrozenSet<T>); if a future factory returns either type, this entry stops being
        // true and the MapGenericType arm has to be written.
        ["FrozenSet"] = "Registered builtin type; construction path never consults the CLR return mapping (#1210).",
        ["FrozenDict"] = "Registered builtin type; has no static factory, so no CLR return reaches the mapping (#1210).",

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
