using System;
using System.Collections.Generic;
using FluentAssertions;
using Sharpy.Compiler.Utilities;
using Xunit;

namespace Sharpy.Compiler.Tests.Utilities;

/// <summary>
/// #1100: unit pins for <see cref="ScratchListPool{T}"/>'s free-list mechanics. The emitter relies
/// on three properties: a rent is always empty, nested/recursive rents never alias a live list
/// (emission recurses into nested suites and classes), and the <c>using</c>-scope return fires on
/// exceptions so pooling degrades gracefully under emitter faults. Recursion through the real
/// emitter is additionally exercised end-to-end by the
/// <c>nested_types/nested_control_flow_reentrancy</c> fixture.
/// </summary>
public class ScratchListPoolTests
{
    [Fact]
    public void Rent_ReturnsEmptyList_AndReusesClearedInstanceAfterReturn()
    {
        var pool = new ScratchListPool<int>();
        List<int> first;
        using (pool.Rent(out first))
        {
            first.Should().BeEmpty("a rented scratch list must always start empty");
            first.Add(1);
            first.Add(2);
        }

        using (pool.Rent(out var second))
        {
            second.Should().BeSameAs(first, "the free list should reuse the returned instance");
            second.Should().BeEmpty("returned lists are cleared before they re-enter the pool");
        }
    }

    [Fact]
    public void NestedRents_YieldDistinctInstances()
    {
        // Emission recurses (nested suites, nested classes): an inner rent while an outer list is
        // still live must pop a distinct instance — the free list only ever holds fully-returned
        // lists, so aliasing is impossible by construction. This pins that construction.
        var pool = new ScratchListPool<string>();
        using (pool.Rent(out var outer))
        {
            outer.Add("outer");
            using (pool.Rent(out var inner))
            {
                inner.Should().NotBeSameAs(outer, "a nested rent must never alias a live list");
                inner.Should().BeEmpty();
                inner.Add("inner");
            }

            outer.Should().ContainSingle().Which.Should().Be(
                "outer", "returning the inner list must not disturb the outer one");
        }
    }

    [Fact]
    public void ScopeDispose_OnException_ReturnsAndClearsList()
    {
        // The using-scope return runs in the implicit finally, so a throw mid-"emission" still
        // returns the list cleared — pooling degrades gracefully rather than corrupting state.
        var pool = new ScratchListPool<int>();
        List<int> rented = null!;
        try
        {
            using var scope = pool.Rent(out rented);
            rented.Add(42);
            throw new InvalidOperationException("mid-emission failure");
        }
        catch (InvalidOperationException)
        {
        }

        using (pool.Rent(out var next))
        {
            next.Should().BeSameAs(rented, "the list must return to the pool even on a throw");
            next.Should().BeEmpty("the exceptional return path must clear like the normal one");
        }
    }
}
