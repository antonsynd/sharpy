using System.Reflection;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The sized-protocol sweep (#1972): every public Sharpy.Core / Sharpy.Stdlib type that carries a
/// .NET collection count — <c>ICollection&lt;T&gt;</c>, <c>IReadOnlyCollection&lt;T&gt;</c> or the
/// non-generic <c>ICollection</c> — must also implement <see cref="ISized"/>, the dunder table's
/// spelling of <c>__len__</c>.
///
/// <para>
/// Why the two spellings must agree: <c>len(x)</c> admits a .NET collection count
/// (<c>ProtocolMembership.HasClrProtocol</c>), but the truth classifier
/// (<c>ClassifyClrTruthiness</c>) answers only from <see cref="ISized"/>/<see cref="IBoolConvertible"/>.
/// A Sharpy-owned collection with a count and no <see cref="ISized"/> therefore has a length yet
/// is refused at <c>if x:</c> (SPY0220 "has no falsy case") — <c>collections.deque</c> was exactly
/// that cell. The cure is at the CLR surface (rung 2), not in the checker; this sweep keeps the
/// class closed for every type the two assemblies will ever export.
/// </para>
///
/// <para>
/// The exemption roster is empty. A row may only be added with an issue number and a reason, and
/// it drains when the issue is fixed — the sweep's subject (a missing <see cref="ISized"/>) is
/// never a valid reason on its own.
/// </para>
/// </summary>
public class SizedProtocolSweepTests
{
    /// <summary>Exempt types (full name of the generic definition) → the issue that tracks them.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> Exemptions = new();

    private static readonly Assembly[] SweptAssemblies =
    {
        typeof(ISized).Assembly,     // Sharpy.Core
        typeof(Deque<>).Assembly,    // Sharpy.Stdlib
    };

    private static bool HasCollectionCount(Type type)
    {
        if (typeof(System.Collections.ICollection).IsAssignableFrom(type))
        {
            return true;
        }

        return type.GetInterfaces().Any(i =>
            i.IsGenericType
            && (i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ICollection<>)
                || i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IReadOnlyCollection<>)));
    }

    private static System.Collections.Generic.IReadOnlyList<Type> CountedTypes()
        => SweptAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsInterface && HasCollectionCount(t))
            .ToList();

    [Fact]
    public void EveryPublicCollectionWithACount_ImplementsISized()
    {
        var missing = CountedTypes()
            .Where(t => !typeof(ISized).IsAssignableFrom(t))
            .Select(t => t.FullName!)
            .Where(name => !Exemptions.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "Public collection type(s) expose a .NET collection count but not Sharpy.ISized, so "
            + "len(x) works while `if x:` is refused (SPY0220): " + string.Join(", ", missing)
            + ". Implement ISized (the Count property already satisfies it) — do not exempt.");
    }

    [Fact]
    public void Exemptions_AreLiveAndCiteAnIssue()
    {
        var counted = new System.Collections.Generic.HashSet<string>(CountedTypes().Select(t => t.FullName!));
        foreach (var (name, issue) in Exemptions)
        {
            Assert.Matches(@"^#\d+ — .+", issue);
            // A row for a type that no longer exists or no longer lacks ISized is stale: drain it.
            Assert.True(counted.Contains(name), $"stale exemption: {name} is not a counted collection");
            var type = SweptAssemblies.Select(a => a.GetType(name)).Single(t => t != null)!;
            Assert.False(typeof(ISized).IsAssignableFrom(type), $"stale exemption: {name} implements ISized ({issue})");
        }
    }

    [Fact]
    public void Sweep_SeesCollections_PositiveControl()
    {
        // The sweep is not vacuous: it reaches both assemblies and the known counted collections
        // (Core's list/dict/set and Stdlib's deque) are in its universe.
        var counted = CountedTypes().Select(t => t.FullName!).ToList();
        Assert.Contains(typeof(Sharpy.List<>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.Dict<,>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.Set<>).FullName!, counted);
        Assert.Contains(typeof(Deque<>).FullName!, counted);
    }
}
