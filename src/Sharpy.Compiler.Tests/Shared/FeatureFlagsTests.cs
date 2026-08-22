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
                "__test_feature", "defer", "inplace_augassign", "matmul",
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
    public void FailableCast_IsRemovedFromKnownFeatures()
    {
        // #1096 graduated failable_cast to unconditional `as?`/`as!` syntax and left the name as a
        // no-op for one release; #1128 completed the two-step retirement by deleting the entry. The
        // flag is now an unknown name at every boundary — there is no syntax left for it to unlock.
        Assert.False(FeatureFlags.KnownFeatures.ContainsKey("failable_cast"));

        Assert.False(FeatureFlags.TryValidate("failable_cast", out var error));
        Assert.NotNull(error);
        Assert.Contains("failable_cast", error);
    }

    [Fact]
    public void DescribeFeatures_AnnotatesNoOpFeatures()
    {
        // The user-visible listing marks graduated flags as safe to remove. No registered feature is
        // mid-graduation right now (failable_cast was the last, removed in #1128), so the annotation
        // is exercised against a synthetic set to keep the machinery covered for the next graduation.
        var message = FeatureFlags.DescribeFeatures(new[]
        {
            new FeatureInfo("graduated_thing", "desc", FeatureScope.Parser, IsNoOp: true),
            new FeatureInfo("live_thing", "desc", FeatureScope.Parser),
            new FeatureInfo("__hidden_thing", "desc", FeatureScope.Semantic, Hidden: true, IsNoOp: true),
        });

        Assert.Contains("graduated_thing (no-op — graduated, safe to remove)", message);
        Assert.Contains("live_thing", message);
        Assert.DoesNotContain("live_thing (no-op", message);

        // Hidden features stay hidden even when they are no-ops.
        Assert.DoesNotContain("__hidden_thing", message);
    }
}
