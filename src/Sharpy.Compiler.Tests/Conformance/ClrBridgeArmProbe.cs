extern alias SharpyRT;

using System;
using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Discovers <see cref="ClrTypeBridge"/>'s <em>name-collapsing arms</em> by probing the bridge
/// instead of listing them.
///
/// <para>
/// A hand-written arm list rots the moment someone adds an arm, and it rots silently: the new arm is
/// simply not covered, which is the parallel-site defect class these guards exist to close. It has
/// already happened — <c>ICollection&lt;T&gt;</c>, <c>Sharpy.FrozenDict&lt;K,V&gt;</c> and
/// <c>IOrderedEnumerable&lt;T&gt;</c> were added to the bridge and never joined the sibling
/// enumeration in <c>ClrOriginProvenanceTests</c>. Probing inverts that: an arm added tomorrow is
/// swept for free, and the hand-written enumerations become cross-checkable against this.
/// </para>
///
/// <para>
/// The definition of "collapsing arm" used here is the bridge's own observable behavior: a CLR
/// generic is an arm exactly when the bridge maps it to a <see cref="GenericType"/> that carries
/// <see cref="GenericType.ClrOriginTypeName"/>. Arms producing a distinct shape (TupleType,
/// FunctionType, TaskType, OptionalType, ResultType) lose no CLR identity and need no provenance.
/// </para>
/// </summary>
internal static class ClrBridgeArmProbe
{
    /// <summary>
    /// CLR generic definitions the probe must rediscover. This is not the arm list — the arm list is
    /// derived — it is the floor that keeps the derivation honest, so a probe that silently stops
    /// finding anything fails instead of passing over an empty set.
    /// </summary>
    internal static readonly string[] RequiredArms =
    {
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.IList`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IReadOnlyList`1",
        "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.IEnumerable`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.ISet`1",
        "System.Linq.IOrderedEnumerable`1",
        "Sharpy.List`1",
        "Sharpy.Dict`2",
        "Sharpy.Set`1",
        "Sharpy.FrozenSet`1",
        "Sharpy.FrozenDict`2",
    };

    /// <summary>
    /// The subset of <see cref="RequiredArms"/> that stamps provenance WITHOUT collapsing onto a
    /// Sharpy collection name. <c>IOrderedEnumerable&lt;T&gt;</c> maps to itself so that a slotted
    /// <c>then_by</c> still has a receiver <c>Enumerable.ThenBy</c> accepts (#1390) — a Sharpy list is
    /// not one, which is the single collapse the wrapper cannot honour.
    ///
    /// <para>
    /// Kept as a subtraction rather than a deletion from <see cref="RequiredArms"/>: the arm still
    /// exists and still carries a stamp, so every sweep over ALL arms must still find it. Only a sweep
    /// that has already filtered to collection names may subtract it, and having to name it here is
    /// what stops "the filter dropped it" from being indistinguishable from "the arm disappeared".
    /// </para>
    /// </summary>
    internal static readonly string[] RequiredNonCollectionArms =
    {
        "System.Linq.IOrderedEnumerable`1",
    };

    /// <summary>
    /// Every arm the bridge currently collapses, as (closed CLR type, mapped Sharpy type) pairs.
    /// </summary>
    internal static IReadOnlyList<(Type Closed, GenericType Mapped)> DiscoverCollapsingArms(ClrTypeBridge bridge)
    {
        var arms = new List<(Type, GenericType)>();

        foreach (var definition in CandidateGenericDefinitions())
        {
            if (!TryClose(definition, out var closed))
                continue;

            if (bridge.MapClrTypeToSemanticType(closed) is GenericType { ClrOriginTypeName: not null } mapped)
                arms.Add((closed, mapped));
        }

        return arms;
    }

    /// <summary>
    /// The universe the probe searches: the BCL collection/sequence namespaces the bridge's arms are
    /// written against, plus everything Sharpy.Core exports. Bounded on purpose — sweeping all of
    /// corelib would spend the time on types no arm could ever match.
    /// </summary>
    internal static IEnumerable<Type> CandidateGenericDefinitions()
    {
        var coreAssembly = typeof(SharpyRT::Sharpy.List<>).Assembly;
        var bclNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic",
            "System.Linq"
        };

        var assemblies = new[]
        {
            typeof(List<>).Assembly,
            typeof(HashSet<>).Assembly,
            typeof(IOrderedEnumerable<>).Assembly,
            coreAssembly
        }.Distinct();

        return assemblies
            .SelectMany(a => a.GetExportedTypes())
            // Open definitions are what the arms match on; nested types (a collection's Enumerator
            // struct) are implementation details of their container and never surface as a value.
            .Where(t => t.IsGenericTypeDefinition && !t.IsNested)
            .Where(t => t.Assembly == coreAssembly
                || (t.Namespace is not null && bclNamespaces.Contains(t.Namespace)))
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Closes a generic definition with placeholder arguments. Returns false when no placeholder
    /// satisfies the constraints — that is a reflection limitation, not a mapping verdict.
    /// </summary>
    internal static bool TryClose(Type definition, out Type closed)
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
            catch (Exception ex) when (ex is ArgumentException or TypeLoadException or NotSupportedException)
            {
                // Constraint violation, or a definition reflection refuses to close (ref struct,
                // pointer element). Both are reflection limitations — try the next placeholder.
            }
        }

        return false;
    }
}
