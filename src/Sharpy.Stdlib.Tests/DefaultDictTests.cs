using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional DefaultDict tests not covered by CollectionsModuleTests.cs.
/// </summary>
public class DefaultDict_Tests
{
    #region Get Without Default

    [Fact]
    public void DefaultDict_Get_NoDefault_ReturnsDefaultT()
    {
        // Get() without default returns default(TValue), does NOT call the factory
        var dd = new Sharpy.DefaultDict<string, int>(() => 42);

        // Get (not indexer) should return 0 (default(int)) not 42 (factory value)
        dd.Get("missing").Should().Be(0);
        dd.ContainsKey("missing").Should().BeFalse(); // key not created
    }

    [Fact]
    public void DefaultDict_Get_ExistingKey_NoDefault_ReturnsValue()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["x"] = 99;

        dd.Get("x").Should().Be(99);
    }

    #endregion

    #region Contains Alias

    [Fact]
    public void DefaultDict_Contains_ExistingKey_ReturnsTrue()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;

        dd.Contains("a").Should().BeTrue();
    }

    [Fact]
    public void DefaultDict_Contains_MissingKey_ReturnsFalse()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);

        dd.Contains("missing").Should().BeFalse();
    }

    [Fact]
    public void DefaultDict_Contains_AfterAutoCreate_ReturnsTrue()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        _ = dd["key"]; // auto-creates via indexer

        dd.Contains("key").Should().BeTrue();
    }

    #endregion

    #region Factory Called For Each Missing Key

    [Fact]
    public void DefaultDict_Factory_CalledForEachMissingKey()
    {
        int callCount = 0;
        var dd = new Sharpy.DefaultDict<string, int>(() => { callCount++; return callCount; });

        _ = dd["a"];
        _ = dd["b"];
        _ = dd["c"];

        callCount.Should().Be(3);
        dd["a"].Should().Be(1);
        dd["b"].Should().Be(2);
        dd["c"].Should().Be(3);
    }

    [Fact]
    public void DefaultDict_Factory_NotCalledForExistingKey()
    {
        int callCount = 0;
        var dd = new Sharpy.DefaultDict<string, int>(() => { callCount++; return 0; });
        dd["a"] = 42;

        // Accessing existing key should NOT call factory
        _ = dd["a"];
        _ = dd["a"];

        callCount.Should().Be(0);
    }

    #endregion

    #region Keys and Values Enumeration

    [Fact]
    public void DefaultDict_Keys_EnumerationOrder_ConsistentWithInsertion()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["z"] = 1;
        dd["a"] = 2;
        dd["m"] = 3;

        var keys = new List<string>(dd.Keys);
        keys.Should().Contain("z");
        keys.Should().Contain("a");
        keys.Should().Contain("m");
        keys.Should().HaveCount(3);
    }

    [Fact]
    public void DefaultDict_Values_EnumerationIncludesAllValues()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 10;
        dd["b"] = 20;
        dd["c"] = 30;

        var values = new List<int>(dd.Values);
        values.Should().Contain(10);
        values.Should().Contain(20);
        values.Should().Contain(30);
        values.Should().HaveCount(3);
    }

    #endregion

    #region List Factory with Complex Accumulation

    [Fact]
    public void DefaultDict_ListFactory_MultipleMissingKeys_EachGetOwnList()
    {
        var dd = new Sharpy.DefaultDict<string, List<int>>(() => new List<int>());

        dd["x"].Add(1);
        dd["y"].Add(2);
        dd["x"].Add(3);

        dd["x"].Should().Equal(1, 3);
        dd["y"].Should().Equal(2);
    }

    #endregion

    #region PopItem Default Behavior

    [Fact]
    public void DefaultDict_PopItem_DefaultIsFirstNotLast()
    {
        var dd = new Sharpy.DefaultDict<string, int>(() => 0);
        dd["a"] = 1;
        dd["b"] = 2;

        // Default last=false means FIFO (first inserted)
        var pair = dd.PopItem();

        pair.Should().Be(("a", 1));
    }

    #endregion

    #region Update from DefaultDict

    [Fact]
    public void DefaultDict_Update_FromDefaultDict_UsesIDictionary()
    {
        var dd1 = new Sharpy.DefaultDict<string, int>(() => 0);
        dd1["a"] = 1;
        dd1["b"] = 2;

        var dd2 = new Sharpy.DefaultDict<string, int>(() => 0);
        dd2["b"] = 99;
        dd2["c"] = 3;

        // Update dd1 from the dictionary view of dd2
        dd1.Update(dd2.ToDictionary());

        dd1["a"].Should().Be(1);
        dd1["b"].Should().Be(99);
        dd1["c"].Should().Be(3);
    }

    #endregion

    #region SetDefault with Factory

    [Fact]
    public void DefaultDict_SetDefault_MissingKey_DoesNotCallFactory()
    {
        int factoryCalls = 0;
        var dd = new Sharpy.DefaultDict<string, int>(() => { factoryCalls++; return 99; });

        // SetDefault uses the provided default value, not the factory
        dd.SetDefault("key", 5);

        factoryCalls.Should().Be(0);
        dd["key"].Should().Be(5);
    }

    #endregion
}
