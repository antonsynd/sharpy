using System.Collections.Immutable;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Utilities;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class DependencyQueryTests
{
    // PathNormalizer converts relative paths to absolute, lowercased on macOS.
    // Use N() to normalize expected values so assertions match.
    private static string N(string path) => PathNormalizer.Normalize(path);

    private static IDependencyQuery CreateGraph(Dictionary<string, ImmutableHashSet<string>> deps)
    {
        var graph = new DependencyGraph(deps);
        return graph;
    }

    [Fact]
    public void DependencyGraph_Implements_IDependencyQuery()
    {
        var graph = new DependencyGraph(
            new Dictionary<string, ImmutableHashSet<string>>());
        Assert.IsAssignableFrom<IDependencyQuery>(graph);
    }

    [Fact]
    public void AllFiles_ReturnsAllFilesInGraph()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["main.spy"] = ImmutableHashSet.Create("utils.spy"),
            ["utils.spy"] = ImmutableHashSet<string>.Empty
        };

        IDependencyQuery query = CreateGraph(deps);
        Assert.Equal(2, query.AllFiles.Count);
        Assert.Contains(N("main.spy"), query.AllFiles);
        Assert.Contains(N("utils.spy"), query.AllFiles);
    }

    [Fact]
    public void GetDirectDependencies_ReturnsDeps()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["main.spy"] = ImmutableHashSet.Create("utils.spy", "models.spy"),
            ["utils.spy"] = ImmutableHashSet<string>.Empty,
            ["models.spy"] = ImmutableHashSet<string>.Empty
        };

        IDependencyQuery query = CreateGraph(deps);
        var mainDeps = query.GetDirectDependencies("main.spy");
        Assert.Equal(2, mainDeps.Count);
        Assert.Contains(N("utils.spy"), mainDeps);
        Assert.Contains(N("models.spy"), mainDeps);
    }

    [Fact]
    public void GetDirectDependencies_ReturnsEmpty_ForUnknownFile()
    {
        IDependencyQuery query = CreateGraph(
            new Dictionary<string, ImmutableHashSet<string>>());
        Assert.Empty(query.GetDirectDependencies("nonexistent.spy"));
    }

    [Fact]
    public void GetDirectDependents_ReturnsDependents()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["main.spy"] = ImmutableHashSet.Create("utils.spy"),
            ["other.spy"] = ImmutableHashSet.Create("utils.spy"),
            ["utils.spy"] = ImmutableHashSet<string>.Empty
        };

        IDependencyQuery query = CreateGraph(deps);
        var utilsDependents = query.GetDirectDependents("utils.spy");
        Assert.Equal(2, utilsDependents.Count);
        Assert.Contains(N("main.spy"), utilsDependents);
        Assert.Contains(N("other.spy"), utilsDependents);
    }

    [Fact]
    public void GetBuildOrder_ReturnsDepsFirst()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["main.spy"] = ImmutableHashSet.Create("utils.spy"),
            ["utils.spy"] = ImmutableHashSet<string>.Empty
        };

        IDependencyQuery query = CreateGraph(deps);
        var order = query.GetBuildOrder().ToList();
        Assert.Equal(2, order.Count);
        Assert.True(order.IndexOf(N("utils.spy")) < order.IndexOf(N("main.spy")));
    }

    [Fact]
    public void GetAffectedFiles_ReturnsTransitiveDependents()
    {
        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["main.spy"] = ImmutableHashSet.Create("models.spy"),
            ["models.spy"] = ImmutableHashSet.Create("utils.spy"),
            ["utils.spy"] = ImmutableHashSet<string>.Empty
        };

        IDependencyQuery query = CreateGraph(deps);
        var affected = query.GetAffectedFiles("utils.spy");
        Assert.Equal(3, affected.Count);
        Assert.Contains(N("utils.spy"), affected);
        Assert.Contains(N("models.spy"), affected);
        Assert.Contains(N("main.spy"), affected);
    }

    [Fact]
    public void ImportQueryAdapter_Implements_IImportQuery()
    {
        Assert.True(typeof(IImportQuery).IsAssignableFrom(typeof(ImportQueryAdapter)));
    }
}
