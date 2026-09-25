using System.Reflection;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The collection repr sweep (#1995): every public Sharpy.Core / Sharpy.Stdlib type that carries a
/// .NET collection count must declare its own <see cref="object.ToString"/> — the dunder table's
/// spelling of <c>__str__</c>/<c>__repr__</c>.
///
/// <para>
/// Why: <c>Builtins.Str</c>/<c>Builtins.Repr</c> render any enumerable that does not render itself
/// as a bare list (<c>SequenceFormatting.TryFormatPlainClrSequence</c>). That fallback exists for
/// LINQ nodes typed <c>list[T]</c> (#1453); for a Sharpy-owned collection it prints the wrong
/// Python type — <c>deque([1, 2])</c> printed <c>[1, 2]</c> and <c>d.keys()</c> printed
/// <c>['a']</c> instead of <c>dict_keys(['a'])</c>. The predicate is the fallback's own
/// (<c>Builtins.RendersItself</c>: a <c>ToString</c> declared below <see cref="object"/>), so a type the sweep
/// accepts is exactly a type the fallback leaves alone. The roster is
/// <see cref="SizedProtocolSweepTests.CountedTypes"/>, shared so the two sweeps cannot drift.
/// Whether each override prints the RIGHT text is the executing column's job
/// (<see cref="CollectionReprMatrixTests"/>).
/// </para>
///
/// <para>
/// The exemption roster is empty. A row may only be added with an issue number and a reason, and
/// it drains when the issue is fixed — a missing <c>ToString</c> is never a reason on its own.
/// </para>
/// </summary>
public class CollectionReprSweepTests
{
    /// <summary>Exempt types (full name of the generic definition) → the issue that tracks them.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> Exemptions = new();

    // The fallback's own predicate (SequenceFormatting.cs), not a copy: a ToString declared by the
    // type or a base other than System.Object.
    private static bool RendersItself(Type type) => Sharpy.Builtins.RendersItself(type);

    [Fact]
    public void EveryPublicCollectionWithACount_RendersItself()
    {
        var missing = SizedProtocolSweepTests.CountedTypes()
            .Where(t => !RendersItself(t))
            .Select(t => t.FullName!)
            .Where(name => !Exemptions.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "Public collection type(s) expose a .NET collection count but do not override ToString, "
            + "so str()/repr()/print render them as a bare list instead of their Python repr: "
            + string.Join(", ", missing)
            + ". Override ToString with the python3-verified repr — do not exempt.");
    }

    [Fact]
    public void Exemptions_AreLiveAndCiteAnIssue()
    {
        var counted = new System.Collections.Generic.HashSet<string>(
            SizedProtocolSweepTests.CountedTypes().Select(t => t.FullName!));
        foreach (var (name, issue) in Exemptions)
        {
            Assert.Matches(@"^#\d+ — .+", issue);
            // A row for a type that no longer exists or already renders itself is stale: drain it.
            Assert.True(counted.Contains(name), $"stale exemption: {name} is not a counted collection");
            var type = SizedProtocolSweepTests.SweptAssemblies.Select(a => a.GetType(name)).Single(t => t != null)!;
            Assert.False(RendersItself(type), $"stale exemption: {name} overrides ToString ({issue})");
        }
    }

    [Fact]
    public void Sweep_SeesTheReprCells_PositiveControl()
    {
        // The sweep is not vacuous: the types #1995 named are in its universe, and the predicate
        // tells a self-rendering type (List) from one that is not (a bare IReadOnlyCollection).
        var counted = SizedProtocolSweepTests.CountedTypes().Select(t => t.FullName!).ToList();
        Assert.Contains(typeof(Deque<>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.DictKeyView<,>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.DictValuesView<,>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.DictItemsView<,>).FullName!, counted);
        Assert.Contains(typeof(Sharpy.Set<>).FullName!, counted);

        Assert.True(RendersItself(typeof(Sharpy.List<>)));
        Assert.False(RendersItself(typeof(NoRepr)));
    }

    /// <summary>
    /// The <c>IRepr</c> roster (#2005, #2043): every public Sharpy.Core / Sharpy.Stdlib type that says
    /// its python repr differs from its str. The roster is a literal, so a new implementer is a
    /// deliberate addition with an executing repr cell (<c>OptionalReprMatrixTests</c>,
    /// <c>DatetimeStrReprMatrixTests</c>, <c>TemplateSurfaceMatrixTests</c>) — and a type dropped
    /// from the channel is red here, not a quietly different repr.
    /// </summary>
    private static readonly string[] ReprRoster =
    {
        "Sharpy.Date", "Sharpy.DateTime", "Sharpy.Interpolation", "Sharpy.Optional`1",
        "Sharpy.Template", "Sharpy.Time", "Sharpy.Timedelta", "Sharpy.Timezone",
    };

    [Fact]
    public void EveryIReprImplementer_IsOnTheRoster()
    {
        var implementers = SizedProtocolSweepTests.SweptAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsInterface && typeof(Sharpy.IRepr).IsAssignableFrom(
                t.IsGenericTypeDefinition ? t.MakeGenericType(t.GetGenericArguments().Select(_ => typeof(int)).ToArray()) : t))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(ReprRoster, implementers);
    }

    [Fact]
    public void IRepr_ReachesRepr_BeforeTheSequenceFallback_PositiveControl()
    {
        // A Template is enumerable: without the IRepr arm ahead of the sequence arm its repr would
        // be the bare list of its parts. Its str renders; its repr is PEP 750's spelling.
        var template = new Sharpy.Template(new[] { "a", "" }, new[] { new Sharpy.Interpolation(5, "x", "") });
        Assert.Equal("a5", Sharpy.Builtins.Str(template));
        Assert.Equal("Template(strings=('a', ''), interpolations=(Interpolation(5, 'x', None, ''),))",
            Sharpy.Builtins.Repr(template));
        Assert.Equal("Some(1)", Sharpy.Builtins.Repr(Sharpy.Optional<int>.Some(1)));
    }

    private sealed class NoRepr : System.Collections.Generic.IReadOnlyCollection<int>
    {
        public int Count => 0;
        public System.Collections.Generic.IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
