using System.Collections.Immutable;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class DependencyGraphTests
{
    #region Helper Methods

    /// <summary>
    /// Find the index of an item in a read-only list.
    /// </summary>
    private static int IndexOf(IReadOnlyList<string> list, string item)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == item) return i;
        }
        return -1;
    }

    /// <summary>
    /// Build a dependency graph from a list of edges.
    /// Each tuple represents (source, dependency) - source depends on dependency.
    /// </summary>
    private static DependencyGraph BuildGraph(params (string source, string dependency)[] edges)
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>.Builder>();

        foreach (var (source, dependency) in edges)
        {
            if (!deps.ContainsKey(source))
            {
                deps[source] = ImmutableHashSet.CreateBuilder<string>();
            }
            deps[source].Add(dependency);

            // Ensure dependency is also in the graph
            if (!deps.ContainsKey(dependency))
            {
                deps[dependency] = ImmutableHashSet.CreateBuilder<string>();
            }
        }

        var finalDeps = deps.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToImmutable());

        return new DependencyGraph(finalDeps);
    }

    /// <summary>
    /// Build a graph with just files (no dependencies).
    /// </summary>
    private static DependencyGraph BuildGraphWithFiles(params string[] files)
    {
        var deps = files.ToDictionary(
            f => f,
            _ => ImmutableHashSet<string>.Empty);

        return new DependencyGraph(deps);
    }

    #endregion

    #region GetDirectDependencies Tests

    [Fact]
    public void GetDirectDependencies_ReturnsCorrectDependencies()
    {
        // a depends on b and c
        var graph = BuildGraph(("a.spy", "b.spy"), ("a.spy", "c.spy"));

        var deps = graph.GetDirectDependencies("a.spy");

        Assert.Equal(2, deps.Count);
        Assert.Contains("b.spy", deps);
        Assert.Contains("c.spy", deps);
    }

    [Fact]
    public void GetDirectDependencies_FileWithNoDependencies_ReturnsEmpty()
    {
        var graph = BuildGraph(("a.spy", "b.spy"));

        var deps = graph.GetDirectDependencies("b.spy");

        Assert.Empty(deps);
    }

    [Fact]
    public void GetDirectDependencies_UnknownFile_ReturnsEmpty()
    {
        var graph = BuildGraph(("a.spy", "b.spy"));

        var deps = graph.GetDirectDependencies("unknown.spy");

        Assert.Empty(deps);
    }

    #endregion

    #region GetDirectDependents Tests

    [Fact]
    public void GetDirectDependents_ReturnsCorrectDependents()
    {
        // a and c both depend on b
        var graph = BuildGraph(("a.spy", "b.spy"), ("c.spy", "b.spy"));

        var dependents = graph.GetDirectDependents("b.spy");

        Assert.Equal(2, dependents.Count);
        Assert.Contains("a.spy", dependents);
        Assert.Contains("c.spy", dependents);
    }

    [Fact]
    public void GetDirectDependents_FileWithNoDependents_ReturnsEmpty()
    {
        var graph = BuildGraph(("a.spy", "b.spy"));

        var dependents = graph.GetDirectDependents("a.spy");

        Assert.Empty(dependents);
    }

    #endregion

    #region GetBuildOrder Tests

    [Fact]
    public void GetBuildOrder_ReturnsTopologicalOrder()
    {
        // a depends on b, b depends on c
        var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"));

        var order = graph.GetBuildOrder();

        Assert.Equal(3, order.Count);
        Assert.True(IndexOf(order, "c.spy") < IndexOf(order, "b.spy"),
            "c.spy should come before b.spy");
        Assert.True(IndexOf(order, "b.spy") < IndexOf(order, "a.spy"),
            "b.spy should come before a.spy");
    }

    [Fact]
    public void GetBuildOrder_EmptyGraph_ReturnsEmptyList()
    {
        var graph = new DependencyGraph(
            new Dictionary<string, ImmutableHashSet<string>>());

        var order = graph.GetBuildOrder();

        Assert.Empty(order);
    }

    [Fact]
    public void GetBuildOrder_SingleFile_ReturnsSingleFile()
    {
        var graph = BuildGraphWithFiles("only.spy");

        var order = graph.GetBuildOrder();

        Assert.Single(order);
        Assert.Equal("only.spy", order[0]);
    }

    [Fact]
    public void GetBuildOrder_LinearChain_ReturnsCorrectOrder()
    {
        // a → b → c → d
        var graph = BuildGraph(
            ("a.spy", "b.spy"),
            ("b.spy", "c.spy"),
            ("c.spy", "d.spy"));

        var order = graph.GetBuildOrder();

        Assert.Equal(4, order.Count);
        Assert.Equal("d.spy", order[0]);
        Assert.Equal("c.spy", order[1]);
        Assert.Equal("b.spy", order[2]);
        Assert.Equal("a.spy", order[3]);
    }

    [Fact]
    public void GetBuildOrder_Diamond_HandlesCorrectly()
    {
        // a → b → d
        // a → c → d
        var graph = BuildGraph(
            ("a.spy", "b.spy"),
            ("a.spy", "c.spy"),
            ("b.spy", "d.spy"),
            ("c.spy", "d.spy"));

        var order = graph.GetBuildOrder();

        Assert.Equal(4, order.Count);
        // d must come first
        Assert.Equal("d.spy", order[0]);
        // b and c must come after d but before a
        Assert.True(IndexOf(order, "b.spy") > IndexOf(order, "d.spy"));
        Assert.True(IndexOf(order, "c.spy") > IndexOf(order, "d.spy"));
        Assert.True(IndexOf(order, "a.spy") > IndexOf(order, "b.spy"));
        Assert.True(IndexOf(order, "a.spy") > IndexOf(order, "c.spy"));
    }

    [Fact]
    public void GetBuildOrder_IndependentFiles_AllIncluded()
    {
        // Three independent files
        var graph = BuildGraphWithFiles("a.spy", "b.spy", "c.spy");

        var order = graph.GetBuildOrder();

        Assert.Equal(3, order.Count);
        Assert.Contains("a.spy", order);
        Assert.Contains("b.spy", order);
        Assert.Contains("c.spy", order);
    }

    #endregion

    #region DetectCycles Tests

    [Fact]
    public void DetectCycles_NoCycles_ReturnsEmpty()
    {
        var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"));

        var cycles = graph.DetectCycles();

        Assert.Empty(cycles);
    }

    [Fact]
    public void DetectCycles_SimpleCycle_ReturnsCycle()
    {
        // a → b → c → a
        var graph = BuildGraph(
            ("a.spy", "b.spy"),
            ("b.spy", "c.spy"),
            ("c.spy", "a.spy"));

        var cycles = graph.DetectCycles();

        Assert.Single(cycles);
        Assert.Contains("a.spy", cycles[0]);
        Assert.Contains("b.spy", cycles[0]);
        Assert.Contains("c.spy", cycles[0]);
    }

    [Fact]
    public void DetectCycles_SelfCycle_ReturnsSingleElementCycle()
    {
        // a → a (self-reference)
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["a.spy"] = ImmutableHashSet.Create("a.spy")
        };
        var graph = new DependencyGraph(deps);

        var cycles = graph.DetectCycles();

        Assert.Single(cycles);
        Assert.Contains("a.spy", cycles[0]);
    }

    [Fact]
    public void DetectCycles_TwoSeparateCycles_ReturnsBoth()
    {
        // Cycle 1: a → b → a
        // Cycle 2: c → d → c
        var graph = BuildGraph(
            ("a.spy", "b.spy"),
            ("b.spy", "a.spy"),
            ("c.spy", "d.spy"),
            ("d.spy", "c.spy"));

        var cycles = graph.DetectCycles();

        Assert.True(cycles.Count >= 2, $"Expected at least 2 cycles, got {cycles.Count}");
    }

    #endregion

    #region GetAffectedFiles Tests

    [Fact]
    public void GetAffectedFiles_SingleChange_ReturnsTransitiveDependents()
    {
        // a depends on b, c depends on b
        var graph = BuildGraph(("a.spy", "b.spy"), ("c.spy", "b.spy"));

        var affected = graph.GetAffectedFiles("b.spy");

        Assert.Equal(3, affected.Count);
        Assert.Contains("a.spy", affected);
        Assert.Contains("b.spy", affected);
        Assert.Contains("c.spy", affected);
    }

    [Fact]
    public void GetAffectedFiles_TransitiveChain_ReturnsAllDependents()
    {
        // a → b → c
        var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"));

        var affected = graph.GetAffectedFiles("c.spy");

        Assert.Equal(3, affected.Count);
        Assert.Contains("a.spy", affected);
        Assert.Contains("b.spy", affected);
        Assert.Contains("c.spy", affected);
    }

    [Fact]
    public void GetAffectedFiles_LeafFile_ReturnsSelf()
    {
        // a depends on b
        var graph = BuildGraph(("a.spy", "b.spy"));

        var affected = graph.GetAffectedFiles("a.spy");

        Assert.Single(affected);
        Assert.Contains("a.spy", affected);
    }

    [Fact]
    public void GetAffectedFiles_MultipleChanges_CombinesResults()
    {
        // a depends on b, c depends on d
        var graph = BuildGraph(("a.spy", "b.spy"), ("c.spy", "d.spy"));

        var affected = graph.GetAffectedFiles(new[] { "b.spy", "d.spy" });

        Assert.Equal(4, affected.Count);
        Assert.Contains("a.spy", affected);
        Assert.Contains("b.spy", affected);
        Assert.Contains("c.spy", affected);
        Assert.Contains("d.spy", affected);
    }

    [Fact]
    public void GetAffectedFiles_UnknownFile_ReturnsEmpty()
    {
        var graph = BuildGraph(("a.spy", "b.spy"));

        var affected = graph.GetAffectedFiles("unknown.spy");

        Assert.Empty(affected);
    }

    #endregion

    #region GetParallelizableGroups Tests

    [Fact]
    public void GetParallelizableGroups_IndependentFiles_AllInFirstGroup()
    {
        var graph = BuildGraphWithFiles("a.spy", "b.spy", "c.spy");

        var groups = graph.GetParallelizableGroups();

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count);
        Assert.Contains("a.spy", groups[0]);
        Assert.Contains("b.spy", groups[0]);
        Assert.Contains("c.spy", groups[0]);
    }

    [Fact]
    public void GetParallelizableGroups_LinearChain_OneFilePerGroup()
    {
        // a → b → c
        var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"));

        var groups = graph.GetParallelizableGroups();

        Assert.Equal(3, groups.Count);
        Assert.Single(groups[0]);
        Assert.Contains("c.spy", groups[0]);
        Assert.Single(groups[1]);
        Assert.Contains("b.spy", groups[1]);
        Assert.Single(groups[2]);
        Assert.Contains("a.spy", groups[2]);
    }

    [Fact]
    public void GetParallelizableGroups_Diamond_CorrectGrouping()
    {
        // a depends on b and c; b and c have no deps
        var graph = BuildGraph(("a.spy", "b.spy"), ("a.spy", "c.spy"));

        var groups = graph.GetParallelizableGroups();

        Assert.Equal(2, groups.Count);
        // Group 0: b and c (no dependencies)
        Assert.Equal(2, groups[0].Count);
        Assert.Contains("b.spy", groups[0]);
        Assert.Contains("c.spy", groups[0]);
        // Group 1: a (depends on b and c)
        Assert.Single(groups[1]);
        Assert.Contains("a.spy", groups[1]);
    }

    [Fact]
    public void GetParallelizableGroups_EmptyGraph_ReturnsEmpty()
    {
        var graph = new DependencyGraph(
            new Dictionary<string, ImmutableHashSet<string>>());

        var groups = graph.GetParallelizableGroups();

        Assert.Empty(groups);
    }

    [Fact]
    public void GetParallelizableGroups_ComplexGraph_CorrectLevels()
    {
        // a → b → d
        // a → c → d
        // e → d (independent of a)
        var graph = BuildGraph(
            ("a.spy", "b.spy"),
            ("a.spy", "c.spy"),
            ("b.spy", "d.spy"),
            ("c.spy", "d.spy"),
            ("e.spy", "d.spy"));

        var groups = graph.GetParallelizableGroups();

        Assert.Equal(3, groups.Count);
        // Group 0: d (no dependencies)
        Assert.Single(groups[0]);
        Assert.Contains("d.spy", groups[0]);
        // Group 1: b, c, e (all depend only on d)
        Assert.Equal(3, groups[1].Count);
        Assert.Contains("b.spy", groups[1]);
        Assert.Contains("c.spy", groups[1]);
        Assert.Contains("e.spy", groups[1]);
        // Group 2: a (depends on b and c)
        Assert.Single(groups[2]);
        Assert.Contains("a.spy", groups[2]);
    }

    #endregion

    #region Path Normalization Tests

    [Fact]
    public void PathNormalization_HandlesSlashVariants()
    {
        // Create graph with forward slashes
        var graph = BuildGraph(("src/a.spy", "src/b.spy"));

        // Query with backslashes
        var deps = graph.GetDirectDependencies("src\\a.spy");

        Assert.Single(deps);
        Assert.Contains("src/b.spy", deps);
    }

    [Fact]
    public void PathNormalization_HandlesCaseDifferences()
    {
        // This test behavior depends on the OS
        var graph = BuildGraph(("A.spy", "B.spy"));

        var deps = graph.GetDirectDependencies("a.spy");

        // On non-Linux systems, should match case-insensitively
        if (!OperatingSystem.IsLinux())
        {
            Assert.Single(deps);
        }
    }

    #endregion

    #region IsStale Tests

    [Fact]
    public void IsStale_NoHashesProvided_ReturnsTrue()
    {
        var graph = BuildGraph(("a.spy", "b.spy"));

        var isStale = graph.IsStale("a.spy", "somehash");

        Assert.True(isStale);
    }

    [Fact]
    public void IsStale_HashMatches_ReturnsFalse()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["a.spy"] = ImmutableHashSet<string>.Empty
        };
        var hashes = new Dictionary<string, string>
        {
            ["a.spy"] = "hash123"
        };
        var graph = new DependencyGraph(deps, hashes);

        var isStale = graph.IsStale("a.spy", "hash123");

        Assert.False(isStale);
    }

    [Fact]
    public void IsStale_HashDiffers_ReturnsTrue()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["a.spy"] = ImmutableHashSet<string>.Empty
        };
        var hashes = new Dictionary<string, string>
        {
            ["a.spy"] = "hash123"
        };
        var graph = new DependencyGraph(deps, hashes);

        var isStale = graph.IsStale("a.spy", "differenthash");

        Assert.True(isStale);
    }

    [Fact]
    public void IsStale_FileNotInGraph_ReturnsTrue()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["a.spy"] = ImmutableHashSet<string>.Empty
        };
        var hashes = new Dictionary<string, string>
        {
            ["a.spy"] = "hash123"
        };
        var graph = new DependencyGraph(deps, hashes);

        var isStale = graph.IsStale("unknown.spy", "somehash");

        Assert.True(isStale);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_NullFileDependencies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DependencyGraph(null!));
    }

    [Fact]
    public void AllFiles_ContainsAllFilesFromDependencies()
    {
        // a depends on b, b is a leaf
        var graph = BuildGraph(("a.spy", "b.spy"));

        Assert.Contains("a.spy", graph.AllFiles);
        Assert.Contains("b.spy", graph.AllFiles);
    }

    [Fact]
    public void AllFiles_IncludesDependenciesNotExplicitlyAdded()
    {
        // Create a graph where dependency is only mentioned as a target
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["a.spy"] = ImmutableHashSet.Create("external.spy")
        };
        var graph = new DependencyGraph(deps);

        Assert.Contains("a.spy", graph.AllFiles);
        Assert.Contains("external.spy", graph.AllFiles);
    }

    #endregion
}
