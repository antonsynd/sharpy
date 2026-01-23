using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Model;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying DependencyGraph works correctly with ProjectCompiler.
/// These tests ensure dependency tracking, circular dependency detection, and
/// parallelizable group computation work in real compilation scenarios.
/// </summary>
public class DependencyGraphIntegrationTests : IntegrationTestBase
{
    public DependencyGraphIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ProjectCompiler_BuildsCorrectDependencyGraph()
    {
        // Create a multi-file project with known dependencies
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("utils.spy", @"
def helper() -> int:
    return 42
");

        tempDir.CreateFile("models.spy", @"
from utils import helper

class Model:
    value: int

    def __init__(self):
        self.value = helper()
");

        tempDir.CreateFile("main.spy", @"
from models import Model

def main() -> None:
    m = Model()
");

        // Compile and get dependency graph
        var result = CompileProject(tempDir.Path, "main.spy");

        // Verify compilation succeeded
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");

        // Verify graph structure
        Assert.NotNull(result.DependencyGraph);
        var graph = result.DependencyGraph;

        // main depends on models (path normalization may change filenames)
        var mainDeps = graph.GetDirectDependencies(FindFile(graph, "main.spy"));
        Assert.Contains(mainDeps, d => d.EndsWith("models.spy", StringComparison.OrdinalIgnoreCase));

        // models depends on utils
        var modelDeps = graph.GetDirectDependencies(FindFile(graph, "models.spy"));
        Assert.Contains(modelDeps, d => d.EndsWith("utils.spy", StringComparison.OrdinalIgnoreCase));

        // utils has no dependencies
        var utilsDeps = graph.GetDirectDependencies(FindFile(graph, "utils.spy"));
        Assert.Empty(utilsDeps);

        // Build order should be: utils, models, main
        var buildOrder = graph.GetBuildOrder();
        Assert.Equal(3, buildOrder.Count);
        Assert.True(IndexOf(buildOrder, "utils.spy") < IndexOf(buildOrder, "models.spy"),
            $"Expected utils before models. Build order: {string.Join(" -> ", buildOrder)}");
        Assert.True(IndexOf(buildOrder, "models.spy") < IndexOf(buildOrder, "main.spy"),
            $"Expected models before main. Build order: {string.Join(" -> ", buildOrder)}");
    }

    [Fact]
    public void ProjectCompiler_DetectsCircularDependencies()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("a.spy", @"
from b import foo

def bar() -> int:
    return 1
");

        tempDir.CreateFile("b.spy", @"
from a import bar

def foo() -> int:
    return 2
");

        tempDir.CreateFile("main.spy", @"
from a import bar

def main() -> None:
    print(bar())
");

        var result = CompileProject(tempDir.Path, "main.spy");

        // Should have circular dependency error
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e =>
            e.Contains("circular", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("Circular", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DependencyGraph_GetParallelizableGroups_WorksCorrectly()
    {
        // Diamond dependency: a depends on b,c; b,c depend on d
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("d.spy", @"
x: int = 1
");

        tempDir.CreateFile("b.spy", @"
from d import x
y: int = x
");

        tempDir.CreateFile("c.spy", @"
from d import x
z: int = x
");

        tempDir.CreateFile("main.spy", @"
from b import y
from c import z
result: int = y + z
");

        var result = CompileProject(tempDir.Path, "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.DependencyGraph);

        var groups = result.DependencyGraph.GetParallelizableGroups();

        // Group 0: d (no deps)
        // Group 1: b, c (depend on d)
        // Group 2: main (depends on b, c)
        Assert.Equal(3, groups.Count);

        // Group 0 should contain only d
        Assert.Single(groups[0]);
        Assert.Contains(groups[0], f => f.EndsWith("d.spy", StringComparison.OrdinalIgnoreCase));

        // Group 1 should contain b and c (parallelizable)
        Assert.Equal(2, groups[1].Count);
        Assert.Contains(groups[1], f => f.EndsWith("b.spy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups[1], f => f.EndsWith("c.spy", StringComparison.OrdinalIgnoreCase));

        // Group 2 should contain only main
        Assert.Single(groups[2]);
        Assert.Contains(groups[2], f => f.EndsWith("main.spy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DependencyGraph_GetAffectedFiles_TracksTransitiveDependents()
    {
        using var tempDir = new TempDirectory();

        tempDir.CreateFile("base.spy", @"
def helper() -> int:
    return 42
");

        tempDir.CreateFile("middle.spy", @"
from base import helper

def wrapper() -> int:
    return helper()
");

        tempDir.CreateFile("main.spy", @"
from middle import wrapper

def main() -> None:
    print(wrapper())
");

        var result = CompileProject(tempDir.Path, "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.DependencyGraph);

        var graph = result.DependencyGraph;
        var baseFile = FindFile(graph, "base.spy");

        // If base.spy changes, all files should be affected
        var affected = graph.GetAffectedFiles(baseFile);

        Assert.Equal(3, affected.Count);
        Assert.Contains(affected, f => f.EndsWith("base.spy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(affected, f => f.EndsWith("middle.spy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(affected, f => f.EndsWith("main.spy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DependencyGraph_IndependentFiles_InSameGroup()
    {
        using var tempDir = new TempDirectory();

        // Two independent modules
        tempDir.CreateFile("math_utils.spy", @"
def add(a: int, b: int) -> int:
    return a + b
");

        tempDir.CreateFile("string_utils.spy", @"
def concat(a: str, b: str) -> str:
    return a + b
");

        tempDir.CreateFile("main.spy", @"
from math_utils import add
from string_utils import concat

def main() -> None:
    print(add(1, 2))
    print(concat(""Hello"", ""World""))
");

        var result = CompileProject(tempDir.Path, "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.DependencyGraph);

        var groups = result.DependencyGraph.GetParallelizableGroups();

        // Should have 2 groups: independent files, then main
        Assert.Equal(2, groups.Count);

        // First group should have both independent files
        Assert.Equal(2, groups[0].Count);
        Assert.Contains(groups[0], f => f.EndsWith("math_utils.spy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups[0], f => f.EndsWith("string_utils.spy", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Helper to find a file in the graph by partial name match (handles path normalization).
    /// </summary>
    private static string FindFile(DependencyGraph graph, string partialName)
    {
        var match = graph.AllFiles.FirstOrDefault(f =>
            f.EndsWith(partialName, StringComparison.OrdinalIgnoreCase));
        return match ?? partialName;
    }

    /// <summary>
    /// Find the index of an item in a list by partial match.
    /// </summary>
    private static int IndexOf(IReadOnlyList<string> list, string partialName)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].EndsWith(partialName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Compile a multi-file Sharpy project and return the result with dependency graph.
    /// </summary>
    private ProjectCompilationResult CompileProject(string projectDir, string entryPointFile)
    {
        var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.TopDirectoryOnly)
            .ToList();

        var config = new ProjectConfig
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
            RootNamespace = "Sharpy.Test",
            OutputType = "exe",
            EntryPoint = entryPointFile,
            SourceFiles = sourceFiles,
            Configuration = "Debug",
            TargetFramework = "net10.0"
        };

        var projectCompiler = new ProjectCompiler();
        return projectCompiler.Compile(config);
    }
}
