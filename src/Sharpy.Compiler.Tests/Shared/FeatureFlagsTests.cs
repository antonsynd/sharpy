using System;
using System.Linq;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class FeatureFlagsTests
{
    [Fact]
    public void None_HasNoEnabledFeatures()
    {
        Assert.Empty(FeatureFlags.None.EnabledFeatures);
        Assert.False(FeatureFlags.None.IsEnabled("__test_feature"));
    }

    [Fact]
    public void Enable_MakesFeatureEnabled()
    {
        var flags = FeatureFlags.None.Enable("__test_feature");
        Assert.True(flags.IsEnabled("__test_feature"));
    }

    [Fact]
    public void Enable_IsImmutable_DoesNotMutateOriginal()
    {
        var original = FeatureFlags.None;
        var updated = original.Enable("__test_feature");

        Assert.False(original.IsEnabled("__test_feature"));
        Assert.True(updated.IsEnabled("__test_feature"));
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void Enable_WithSameFeatures_ReturnsSameInstance()
    {
        var flags = FeatureFlags.None.Enable("__test_feature");
        var again = flags.Enable("__test_feature");
        Assert.Same(flags, again);
    }

    [Fact]
    public void Enable_EnumerableOverload_AddsAll()
    {
        var flags = FeatureFlags.None.Enable(new[] { "__test_feature" });
        Assert.True(flags.IsEnabled("__test_feature"));
    }

    [Fact]
    public void EnabledFeatures_ReturnsOrdinallySortedNames()
    {
        var flags = FeatureFlags.None.Enable(new[] { "__test_feature" });
        Assert.Equal(new[] { "__test_feature" }, flags.EnabledFeatures.ToArray());
    }

    [Fact]
    public void KnownFeatures_ContainsExactlyTheRegisteredFeatures()
    {
        // Exact-set guard: adding a feature must be a deliberate act that updates this list
        // (and, per docs/design/feature-lifecycle.md, registers its gated constructs).
        Assert.Equal(
            new[] { "__test_feature", "defer", "failable_cast", "matmul", "property_observers" },
            FeatureFlags.KnownFeatures.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void TestFeature_IsSemanticScopedAndHidden()
    {
        var info = FeatureFlags.KnownFeatures["__test_feature"];
        Assert.Equal(FeatureScope.Semantic, info.Scope);
        Assert.True(info.Hidden);
    }

    [Fact]
    public void TryValidate_KnownFeature_Succeeds()
    {
        Assert.True(FeatureFlags.TryValidate("__test_feature", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_UnknownFeature_Fails()
    {
        Assert.False(FeatureFlags.TryValidate("not_a_feature", out var error));
        Assert.NotNull(error);
        Assert.Contains("not_a_feature", error);
    }

    [Fact]
    public void KnownFeatureListMessage_HidesHiddenFeatures()
    {
        // __test_feature is the only known feature and it is hidden, so the
        // user-visible list is empty.
        var message = FeatureFlags.KnownFeatureListMessage();
        Assert.DoesNotContain("__test_feature", message);
    }
}
