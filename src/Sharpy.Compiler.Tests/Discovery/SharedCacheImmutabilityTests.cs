using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Guards that shared reflection caches in ClrTypeHelper and ClrExtensionMethodResolver
/// hand out immutable views — callers cannot mutate shared state (#1672 H1.4).
/// </summary>
public class SharedCacheImmutabilityTests
{
    [Fact]
    public void ResolveGenericInstanceMethods_ReturnsImmutableArray()
    {
        var result = ClrTypeHelper.ResolveGenericInstanceMethods(typeof(List<int>), "convert_all");

        Assert.IsType<ImmutableArray<MethodInfo>>(result);
    }

    [Fact]
    public void GetMemberNameSurface_ReturnsFrozenSet()
    {
        var result = ClrTypeHelper.GetMemberNameSurface(typeof(string));

        Assert.NotNull(result);
        Assert.IsAssignableFrom<FrozenSet<string>>(result);
        Assert.False(result is HashSet<string>,
            "GetMemberNameSurface must not return a mutable HashSet.");
    }

    [Fact]
    public void GetExtensionMethodNames_ReturnsFrozenSet()
    {
        var result = ClrTypeHelper.GetExtensionMethodNames(typeof(Enumerable).Assembly);

        Assert.IsAssignableFrom<FrozenSet<string>>(result);
        Assert.False(result is HashSet<string>,
            "GetExtensionMethodNames must not return a mutable HashSet.");
    }

    [Fact]
    public void ExtensionMethodResolver_ByNameValues_AreReadOnlyCollections()
    {
        var lazyField = typeof(ClrExtensionMethodResolver)
            .GetField("_byName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(lazyField);

        var lazy = lazyField!.GetValue(null) as Lazy<Dictionary<string, IReadOnlyList<MethodInfo>>>;
        Assert.NotNull(lazy);

        var index = lazy!.Value;
        Assert.NotEmpty(index);

        foreach (var (name, methods) in index)
        {
            Assert.IsType<ReadOnlyCollection<MethodInfo>>(methods);
            Assert.False(methods is List<MethodInfo>,
                $"_byName[\"{name}\"] is a mutable List<MethodInfo> — must be ReadOnlyCollection.");
        }
    }
}
