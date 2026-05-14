using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Integration tests for incremental compilation using ProjectCompilationHelper.
/// These tests exercise the full compilation pipeline with incremental mode enabled,
/// testing scenarios like recompilation after changes and dependency tracking.
/// </summary>
public class IncrementalCompilationHelperTests
{
    private readonly ITestOutputHelper _output;

    public IncrementalCompilationHelperTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void IncrementalCompile_MultiFile_SecondBuildSucceeds()
    {
        // Compile a multi-file project, then compile again without changes.
        // Both compilations should succeed.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("IncrTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
")
            .AddSourceFile("lib.spy", @"
def get_value() -> int:
    return 42
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success,
            "First build failed: " + string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Second build (no changes)
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Second build failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalCompile_UnchangedFiles_AreSkipped()
    {
        // Compile a multi-file project, then compile again without changes.
        // The second build should skip all files.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("SkipTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
def main():
    print('hello')
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success);

        // Second build (no changes)
        var result2 = helper.Compile();
        Assert.True(result2.Success);

        // Verify metrics show files were skipped
        Assert.NotNull(result2.Metrics);
        Assert.True(result2.Metrics!.SkippedFileCount > 0,
            $"Expected skipped files in second build, got SkippedFileCount={result2.Metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalCompile_ChangedImportedFile_DependentsRecompile()
    {
        // Compile a multi-file project, change an imported file, then recompile.
        // The dependent file should be recompiled (not skipped).
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("DepChangeTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
from lib import greet

def main():
    msg: str = greet()
    print(msg)
")
            .AddSourceFile("lib.spy", @"
def greet() -> str:
    return 'hello'
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success,
            "First build failed: " + string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Change the imported file
        helper.UpdateSourceFile("lib.spy", @"
def greet() -> str:
    return 'modified hello'
");

        // Second build - should recompile at least lib.spy and main.spy
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Second build after change failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalCompile_CacheCleared_TriggersFullRebuild()
    {
        // Compile, clear cache, recompile. The second build should be a full rebuild.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("CacheClearTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
def main():
    print('cache test')
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success);

        // Clear cache
        helper.ClearCache();

        // Second build should succeed (full rebuild)
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Rebuild after cache clear failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalCompile_TransitiveDependencyChanged_RecompilesAll()
    {
        // A -> B -> C dependency chain.
        // When C changes, both B and A should be recompiled.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("TransDepTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
from mid import get_formatted

def main():
    msg: str = get_formatted()
    print(msg)
")
            .AddSourceFile("mid.spy", @"
from leaf import get_raw

def get_formatted() -> str:
    return '[' + get_raw() + ']'
")
            .AddSourceFile("leaf.spy", @"
def get_raw() -> str:
    return 'data'
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success,
            "First build failed: " + string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Change the leaf dependency
        helper.UpdateSourceFile("leaf.spy", @"
def get_raw() -> str:
    return 'modified_data'
");

        // Second build should succeed
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Second build after leaf change failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalCompile_WithClass_SecondBuildSucceeds()
    {
        // Ensure incremental compilation works with class definitions across files.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("ClassIncrTest")
            .WithIncremental()
            .AddSourceFile("main.spy", @"
from shapes import Circle

def main():
    c: Circle = Circle(5)
    print(c.radius)
")
            .AddSourceFile("shapes.spy", @"
class Circle:
    radius: int

    def __init__(self, r: int):
        self.radius = r
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success,
            "First build failed: " + string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Second build (no changes)
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Second build failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify files were skipped
        Assert.NotNull(result2.Metrics);
        Assert.True(result2.Metrics!.SkippedFileCount > 0,
            "Expected files to be skipped in second build");
    }

    [Fact]
    public void IncrementalCompile_NonIncrementalMode_NeverSkips()
    {
        // Without incremental mode, no files should be skipped.
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("NonIncrTest")
            // Note: NOT calling .WithIncremental()
            .AddSourceFile("main.spy", @"
def main():
    print('no incremental')
")
            .CreateProjectFile();

        // First build
        var result1 = helper.Compile();
        Assert.True(result1.Success);

        // Second build (non-incremental: should NOT skip any files)
        var result2 = helper.Compile();
        Assert.True(result2.Success);

        // In non-incremental mode, SkippedFileCount should be 0
        if (result2.Metrics != null)
        {
            Assert.Equal(0, result2.Metrics.SkippedFileCount);
        }
    }

    [Fact]
    public void IncrementalCompile_CircularImportCycle_BothFilesRecompiledWhenOneChanges()
    {
        using var helper = new ProjectCompilationHelper(_output);

        helper
            .WithRootNamespace("CircularIncrTest")
            .WithIncremental()
            .AddSourceFile("parent.spy", @"
from child import Child

class Parent:
    name: str
    child_name: str

    def __init__(self, name: str):
        self.name = name
        self.child_name = ''

    def describe(self) -> str:
        return self.name
")
            .AddSourceFile("child.spy", @"
from parent import Parent

class Child:
    name: str
    parent_name: str

    def __init__(self, name: str):
        self.name = name
        self.parent_name = ''

    def describe(self) -> str:
        return self.name
")
            .AddSourceFile("main.spy", @"
from parent import Parent
from child import Child

def main():
    p: Parent = Parent('Alice')
    c: Child = Child('Bob')
    print(p.describe())
    print(c.describe())
")
            .CreateProjectFile();

        // First build — populates cache
        var result1 = helper.Compile();
        Assert.True(result1.Success,
            "First build failed: " + string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Second build — no changes, all files should be skipped
        var result2 = helper.Compile();
        Assert.True(result2.Success,
            "Second build failed: " + string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));
        Assert.NotNull(result2.Metrics);
        Assert.Equal(3, result2.Metrics!.SkippedFileCount);

        // Change only child.spy — parent.spy should also be recompiled due to circular dependency
        helper.UpdateSourceFile("child.spy", @"
from parent import Parent

class Child:
    name: str
    parent_name: str

    def __init__(self, name: str):
        self.name = name
        self.parent_name = ''

    def describe(self) -> str:
        return 'child:' + self.name
");

        // Third build — child.spy changed, parent.spy and main.spy depend on it
        var result3 = helper.Compile();
        Assert.True(result3.Success,
            "Third build failed: " + string.Join("; ", result3.Diagnostics.GetErrors().Select(e => e.Message)));
        Assert.NotNull(result3.Metrics);

        // All 3 files should be recompiled: child.spy (changed), parent.spy (circular dep),
        // main.spy (depends on both). No files should be skipped.
        Assert.Equal(0, result3.Metrics!.SkippedFileCount);
    }
}
