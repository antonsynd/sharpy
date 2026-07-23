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
            new[]
            {
                "__test_feature", "defer", "failable_cast", "matmul",
                "opt_comprehension_fusion", "opt_const_fold", "opt_stack_collections",
                "property_observers",
            },
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

    // ── Graduation (IsNoOp) marker (#1096) ──────────────────────────────

    [Fact]
    public void FeatureInfo_IsNoOp_DefaultsToFalse()
    {
        var info = new FeatureInfo("x", "desc", FeatureScope.Semantic);
        Assert.False(info.IsNoOp);
    }

    [Fact]
    public void FailableCast_IsMarkedNoOp_Graduated()
    {
        // #1096 graduated failable_cast: the gate is gone, but the name stays in KnownFeatures as a
        // no-op so existing --enable-feature / <Features> / from __future__ import sites keep compiling.
        var info = FeatureFlags.KnownFeatures["failable_cast"];
        Assert.True(info.IsNoOp);
        Assert.False(info.Hidden);
    }

    [Fact]
    public void NoOpFeature_StillValidates_EnableFeatureAccepted()
    {
        // `--enable-feature failable_cast` (or <Features>failable_cast</Features>) must still succeed
        // silently after graduation — a graduated flag is a known no-op, not an unknown name.
        Assert.True(FeatureFlags.TryValidate("failable_cast", out var error));
        Assert.Null(error);

        var flags = FeatureFlags.None.Enable("failable_cast");
        Assert.True(flags.IsEnabled("failable_cast"));
    }

    [Fact]
    public void KnownFeatureListMessage_AnnotatesNoOpFeatures()
    {
        // The user-visible listing marks graduated flags as safe to remove.
        var message = FeatureFlags.KnownFeatureListMessage();
        Assert.Contains("failable_cast (no-op", message);

        // Non-graduated features appear without the annotation.
        Assert.Contains("defer", message);
        Assert.DoesNotContain("defer (no-op", message);
    }
}
