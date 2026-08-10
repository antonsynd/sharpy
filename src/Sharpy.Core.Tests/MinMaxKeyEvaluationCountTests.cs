using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// How many times a keyed <c>min</c>/<c>max</c> evaluates its key (#1416).
///
/// <para>
/// The value these builtins return was already covered. The number of times they CALL the key was
/// not, and that is a semantic in its own right: a key function may log, count, memoise or do I/O,
/// so evaluating it more often than the program says is the same class of defect #1334 hunts in the
/// emitter — right answer, wrong effect count — just living in the runtime library, where no emitter
/// instrument can reach it.
/// </para>
///
/// <para>
/// The counts asserted here are CPython's, measured rather than assumed
/// (<c>python3</c>, appending to a list from the key): <c>min</c>/<c>max</c> with a key call it
/// exactly ONCE PER ELEMENT — 3 for a 3-element sequence, 1 for a single element, 0 for an empty
/// one — independent of ordering.
/// </para>
/// </summary>
public class MinMaxKeyEvaluationCountTests
{
    /// <summary>
    /// A key that counts how many times it is asked. A bare counter rather than a list of the
    /// elements seen: in this assembly <c>List&lt;T&gt;</c> is Sharpy's, whose <c>Count</c> is
    /// Python's <c>count(value)</c> METHOD, not a length property.
    /// </summary>
    private sealed class CountingKey
    {
        public int Count { get; private set; }

        public int Of(int value)
        {
            Count++;
            return -value;
        }
    }

    [Theory]
    // Ascending, descending and mixed, because the incumbent-recomputation bug's cost depends on
    // how often the incumbent changes: descending is its worst case for `min`, ascending for `max`.
    [InlineData(new[] { 3, 1, 2 }, 3)]
    [InlineData(new[] { 5, 4, 3, 2, 1 }, 5)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 5)]
    [InlineData(new[] { 7 }, 1)]
    public void Min_WithKey_CallsTheKeyOncePerElement(int[] values, int expectedCalls)
    {
        var key = new CountingKey();

        Builtins.Min(values, key.Of);

        key.Count.Should().Be(expectedCalls,
            "a keyed min evaluates each element's key exactly once, as CPython does — "
            + "recomputing the running incumbent's key costs 2(N-1) evaluations instead of N (#1416)");
    }

    [Theory]
    [InlineData(new[] { 3, 1, 2 }, 3)]
    [InlineData(new[] { 5, 4, 3, 2, 1 }, 5)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 5)]
    [InlineData(new[] { 7 }, 1)]
    public void Max_WithKey_CallsTheKeyOncePerElement(int[] values, int expectedCalls)
    {
        var key = new CountingKey();

        Builtins.Max(values, key.Of);

        key.Count.Should().Be(expectedCalls, "the max twin of #1416");
    }

    [Fact]
    public void Min_WithKeyAndDefault_CallsTheKeyOncePerElement()
    {
        // The `default` overloads carry their own copy of the loop, so they carry their own copy of
        // the defect; covering only the two-argument form would have left half of it in place.
        var key = new CountingKey();

        Builtins.Min(new[] { 5, 4, 3, 2, 1 }, key.Of, -1);

        key.Count.Should().Be(5);
    }

    [Fact]
    public void Max_WithKeyAndDefault_CallsTheKeyOncePerElement()
    {
        var key = new CountingKey();

        Builtins.Max(new[] { 1, 2, 3, 4, 5 }, key.Of, -1);

        key.Count.Should().Be(5);
    }

    [Fact]
    public void Min_WithKey_OnAnEmptySequence_NeverCallsTheKey()
    {
        var key = new CountingKey();

        Builtins.Min(new int[0], key.Of, -1);

        key.Count.Should().Be(0, "there is no element to take a key of");
    }

    [Fact]
    public void KeyedResultsAreUnchanged_TheCountIsTheOnlyThingUnderTest()
    {
        // The positive control. An implementation that stopped calling the key at all would satisfy
        // every count above; these pin that the ANSWERS still come from the key, not the elements.
        var key = new CountingKey();

        Builtins.Min(new[] { 3, 1, 2 }, key.Of).Should().Be(3, "min by -v is the largest v");
        Builtins.Max(new[] { 3, 1, 2 }, key.Of).Should().Be(1, "max by -v is the smallest v");
        Builtins.Min(new int[0], key.Of, -1).Should().Be(-1, "the default is returned when empty");
    }

    [Fact]
    public void NoKeyOverloads_AreUnaffected()
    {
        // The no-key path shares neither the loop nor the fix; asserted so a regression there cannot
        // hide behind the keyed cells above.
        Builtins.Min(new[] { 3, 1, 2 }).Should().Be(1);
        Builtins.Max(new[] { 3, 1, 2 }).Should().Be(3);
    }
}
