using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Totality guard for #1866: every CLR shape the compiler's <c>ClrTypeBridge</c> collapses onto a
/// Sharpy collection must have a wrapper constructor that accepts it, so the materialization the
/// store seam records (<c>new Sharpy.Dict&lt;K,V&gt;(value)</c>, <c>new Sharpy.List&lt;T&gt;(value)</c>,
/// <c>new Sharpy.Set&lt;T&gt;(value)</c>) actually compiles. Recorded is not applied (§4): the
/// materialization was verified at the diagnostic level while <c>Sharpy.Dict</c> had no constructor an
/// <c>IDictionary&lt;K,V&gt;</c> converts to — CS1503 behind SPY0908 at every dict destination.
///
/// <para>
/// The roster is the SAME nine collapse arms the compiler-side twin
/// (<c>CollapsedClrShapeBridgeRosterTests</c>) enumerates and asserts the bridge actually collapses;
/// this side asserts a constructor exists for each. A row without a constructor is red here; a new
/// collapse arm without a row is red on the compiler side.
/// </para>
/// </summary>
public class CollapsedClrShapeConstructionTests
{
    /// <summary>One collapsed CLR interface, the Sharpy wrapper it collapses onto, and the wrapper's
    /// open generic definition closed with the interface's own type arguments.</summary>
    public sealed record CollapseArm(string Wrapper, Type ClrShape, Type WrapperClosed);

    private static readonly CollapseArm[] Arms =
    {
        // Six shapes collapse onto list — the interface half plus the ReadOnlyCollection<T> class.
        new("list", typeof(IList<int>), typeof(List<int>)),
        new("list", typeof(ICollection<int>), typeof(List<int>)),
        new("list", typeof(IEnumerable<int>), typeof(List<int>)),
        new("list", typeof(IReadOnlyList<int>), typeof(List<int>)),
        new("list", typeof(IReadOnlyCollection<int>), typeof(List<int>)),
        new("list", typeof(ReadOnlyCollection<int>), typeof(List<int>)),

        // ISet<T> collapses onto set.
        new("set", typeof(ISet<int>), typeof(Set<int>)),

        // Two mapping shapes collapse onto dict — the #1866 rows.
        new("dict", typeof(IDictionary<string, int>), typeof(Dict<string, int>)),
        new("dict", typeof(IReadOnlyDictionary<string, int>), typeof(Dict<string, int>)),
    };

    [Fact]
    public void CollapsedClrShapesConstructTheWrapper()
    {
        var uncovered = new List<string>();

        foreach (var arm in Arms)
        {
            if (!ConstructibleFrom(arm.WrapperClosed, arm.ClrShape))
            {
                uncovered.Add(
                    $"{arm.Wrapper}: {arm.ClrShape.Name} has no {arm.WrapperClosed.Name} constructor "
                    + "parameter it is assignable to — the bridge collapses it but the wrapper cannot "
                    + "materialize it (#1866).");
            }
        }

        uncovered.Should().BeEmpty(
            "every CLR shape ClrTypeBridge collapses onto a Sharpy collection must have a wrapper "
            + "constructor that accepts it, or the recorded materialization emits code that does not "
            + "compile (§4, #1866).\nUncovered:\n" + string.Join("\n", uncovered));
    }

    /// <summary>Positive control: the wrapper roster and assignability check are wired up, so an
    /// interface that no constructor accepts is reported — proving the guard above can fail.</summary>
    [Fact]
    public void ConstructibilityRule_ReportsAnUnconstructibleShape()
    {
        // A Dict<string,int> has no constructor accepting a bare System.Uri; the rule must say so.
        ConstructibleFrom(typeof(Dict<string, int>), typeof(Uri)).Should().BeFalse(
            "if an arbitrary unrelated type reads as constructible, the assignability check is inert "
            + "and CollapsedClrShapesConstructTheWrapper passes vacuously");
    }

    private static bool ConstructibleFrom(Type wrapperClosed, Type clrShape)
        => wrapperClosed.GetConstructors().Any(ctor =>
        {
            var parameters = ctor.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(clrShape);
        });
}
