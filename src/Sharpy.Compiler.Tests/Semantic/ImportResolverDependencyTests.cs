using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for ImportResolver dependency graph integration.
/// Verifies that dependencies are correctly tracked when resolving imports.
/// </summary>
public class ImportResolverDependencyTests : IDisposable
{
    private readonly string _testDir;
    private readonly ICompilerLogger _logger;

    public ImportResolverDependencyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sharpy_dep_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _logger = NullLogger.Instance;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Test Helpers

    private string CreateModuleFile(string moduleName, string content)
    {
        var filePath = Path.Combine(_testDir, $"{moduleName}.spy");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string CreateModuleFile(string subdir, string moduleName, string content)
    {
        var dir = Path.Combine(_testDir, subdir);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{moduleName}.spy");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Normalize a path for comparison with paths in the dependency graph.
    /// Uses the same normalization logic as DependencyGraph.
    /// </summary>
    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }
        return normalized;
    }

    /// <summary>
    /// Assert that a set contains a path (using normalized comparison).
    /// </summary>
    private static void AssertContainsPath(IEnumerable<string> set, string path)
    {
        var normalized = NormalizePath(path);
        Assert.Contains(normalized, set);
    }

    #endregion

    #region ResolveImport Dependency Tracking

    [Fact]
    public void ResolveImport_AddsDependency()
    {
        // Create two module files
        var utilsPath = CreateModuleFile("utils", @"
def helper():
    pass
");
        var mainPath = CreateModuleFile("main", @"
import utils
");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);
        resolver.SetCurrentModule(mainPath);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "utils", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        };

        resolver.ResolveImport(importStmt, _testDir);

        var graph = builder.Build();
        var deps = graph.GetDirectDependencies(mainPath);

        Assert.Single(deps);
        AssertContainsPath(deps, utilsPath);
    }

    [Fact]
    public void ResolveImport_MultipleImports_AddsAllDependencies()
    {
        // Create multiple module files
        var utilsPath = CreateModuleFile("utils", "def helper(): pass");
        var modelsPath = CreateModuleFile("models", "class Model: pass");
        var mainPath = CreateModuleFile("main", "import utils, models");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);
        resolver.SetCurrentModule(mainPath);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "utils", AsName = null, LineStart = 1, ColumnStart = 1 },
                new ImportAlias { Name = "models", AsName = null, LineStart = 1, ColumnStart = 8 }
            },
            LineStart = 1,
            ColumnStart = 1
        };

        resolver.ResolveImport(importStmt, _testDir);

        var graph = builder.Build();
        var deps = graph.GetDirectDependencies(mainPath);

        Assert.Equal(2, deps.Count);
        AssertContainsPath(deps, utilsPath);
        AssertContainsPath(deps, modelsPath);
    }

    #endregion

    #region ResolveFromImport Dependency Tracking

    [Fact]
    public void ResolveFromImport_AddsDependency()
    {
        var utilsPath = CreateModuleFile("utils", @"
def helper():
    pass

def other():
    pass
");
        var mainPath = CreateModuleFile("main", @"
from utils import helper
");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);
        resolver.SetCurrentModule(mainPath);

        var fromImport = new FromImportStatement
        {
            Module = "utils",
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "helper", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            ImportAll = false,
            LineStart = 1,
            ColumnStart = 1
        };

        resolver.ResolveFromImport(fromImport, _testDir);

        var graph = builder.Build();
        var deps = graph.GetDirectDependencies(mainPath);

        Assert.Single(deps);
        AssertContainsPath(deps, utilsPath);
    }

    [Fact]
    public void ResolveFromImport_ImportAll_AddsDependency()
    {
        var utilsPath = CreateModuleFile("utils", @"
def func1(): pass
def func2(): pass
");
        var mainPath = CreateModuleFile("main", "from utils import *");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);
        resolver.SetCurrentModule(mainPath);

        var fromImport = new FromImportStatement
        {
            Module = "utils",
            Names = new List<ImportAlias>(),
            ImportAll = true,
            LineStart = 1,
            ColumnStart = 1
        };

        resolver.ResolveFromImport(fromImport, _testDir);

        var graph = builder.Build();
        var deps = graph.GetDirectDependencies(mainPath);

        Assert.Single(deps);
        AssertContainsPath(deps, utilsPath);
    }

    #endregion

    #region Transitive Dependencies

    [Fact(Skip = "Complex transitive dependencies tested in ProjectCompiler integration tests")]
    public void NestedImports_AddsTransitiveDependencies()
    {
        // c.spy has no imports
        var cPath = CreateModuleFile("c", "def c_func(): pass");

        // b.spy imports c
        var bPath = CreateModuleFile("b", @"
import c
def b_func(): pass
");

        // a.spy imports b
        var aPath = CreateModuleFile("a", "import b");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);

        // Resolve imports from a.spy
        resolver.SetCurrentModule(aPath);
        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "b", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        };
        resolver.ResolveImport(importStmt, _testDir);

        var graph = builder.Build();

        // Check direct dependencies
        var aDeps = graph.GetDirectDependencies(aPath);
        Assert.Single(aDeps);
        AssertContainsPath(aDeps, bPath);

        var bDeps = graph.GetDirectDependencies(bPath);
        Assert.Single(bDeps);
        AssertContainsPath(bDeps, cPath);

        // Check build order
        var order = graph.GetBuildOrder();
        Assert.Equal(3, order.Count);

        // Find indices using linear search since IReadOnlyList doesn't have IndexOf
        // Use normalized paths for comparison
        var normCPath = NormalizePath(cPath);
        var normBPath = NormalizePath(bPath);
        var normAPath = NormalizePath(aPath);
        int cIndex = -1, bIndex = -1, aIndex = -1;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == normCPath) cIndex = i;
            if (order[i] == normBPath) bIndex = i;
            if (order[i] == normAPath) aIndex = i;
        }

        Assert.True(cIndex < bIndex, "c should be built before b");
        Assert.True(bIndex < aIndex, "b should be built before a");
    }

    #endregion

    #region .NET Module Dependencies

    [Fact]
    public void NetModule_NotAddedToGraph()
    {
        // Create a module that would import a .NET module
        var mainPath = CreateModuleFile("main", "# imports .NET module");

        // Create a mock ModuleRegistry that claims to have the module
        var moduleRegistry = new ModuleRegistry(_logger);
        // We can't easily add fake .NET modules, but we can verify behavior
        // by checking that when TryResolveNetModule returns a result,
        // no dependency is added to the graph.

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger, moduleRegistry);
        resolver.SetDependencyGraphBuilder(builder);
        resolver.SetCurrentModule(mainPath);

        // Try to import a non-existent .NET module (will fall through to .spy lookup)
        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "nonexistent_net_module", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        };

        // This will fail to resolve (no .spy file exists), but importantly
        // we verify the graph builder behavior
        resolver.ResolveImport(importStmt, _testDir);

        var graph = builder.Build();

        // The main path should have no dependencies since the import failed
        // (there's no .spy file for "nonexistent_net_module")
        var deps = graph.GetDirectDependencies(mainPath);
        Assert.Empty(deps);
    }

    #endregion

    #region No Builder Set

    [Fact]
    public void ResolveImport_NoBuilder_DoesNotThrow()
    {
        var utilsPath = CreateModuleFile("utils", "def helper(): pass");
        var mainPath = CreateModuleFile("main", "import utils");

        // Don't set a builder
        var resolver = new ImportResolver(_logger);
        resolver.SetCurrentModule(mainPath);

        var importStmt = new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "utils", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        };

        // Should not throw even without a dependency graph builder
        var exception = Record.Exception(() => resolver.ResolveImport(importStmt, _testDir));
        Assert.Null(exception);
    }

    #endregion

    #region Graph Queries After Import Resolution

    [Fact(Skip = "Complex transitive dependencies tested in ProjectCompiler integration tests")]
    public void GetAffectedFiles_AfterImportResolution_ReturnsCorrectFiles()
    {
        // Create a diamond dependency: main -> {utils, models}, utils -> base, models -> base
        var basePath = CreateModuleFile("base", "BASE_VALUE: int = 42");
        var utilsPath = CreateModuleFile("utils", "import base\ndef helper(): pass");
        var modelsPath = CreateModuleFile("models", "import base\nclass Model: pass");
        var mainPath = CreateModuleFile("main", "import utils\nimport models");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);

        // Resolve main's imports (which triggers resolution of utils and models)
        resolver.SetCurrentModule(mainPath);
        resolver.ResolveImport(new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "utils", AsName = null, LineStart = 1, ColumnStart = 1 },
                new ImportAlias { Name = "models", AsName = null, LineStart = 2, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        }, _testDir);

        var graph = builder.Build();

        // If base changes, everything should be affected
        var affected = graph.GetAffectedFiles(basePath);

        Assert.Equal(4, affected.Count);
        AssertContainsPath(affected, basePath);
        AssertContainsPath(affected, utilsPath);
        AssertContainsPath(affected, modelsPath);
        AssertContainsPath(affected, mainPath);
    }

    [Fact(Skip = "Complex transitive dependencies tested in ProjectCompiler integration tests")]
    public void GetParallelizableGroups_AfterImportResolution_ReturnsCorrectGroups()
    {
        // Create a simple hierarchy: main -> utils -> base
        var basePath = CreateModuleFile("base", "VALUE: int = 1");
        var utilsPath = CreateModuleFile("utils", "import base\ndef helper(): pass");
        var mainPath = CreateModuleFile("main", "import utils");

        var builder = new DependencyGraphBuilder();
        var resolver = new ImportResolver(_logger);
        resolver.SetDependencyGraphBuilder(builder);

        resolver.SetCurrentModule(mainPath);
        resolver.ResolveImport(new ImportStatement
        {
            Names = new List<ImportAlias>
            {
                new ImportAlias { Name = "utils", AsName = null, LineStart = 1, ColumnStart = 1 }
            },
            LineStart = 1,
            ColumnStart = 1
        }, _testDir);

        var graph = builder.Build();
        var groups = graph.GetParallelizableGroups();

        Assert.Equal(3, groups.Count);

        // Group 0: base (no deps)
        Assert.Single(groups[0]);
        AssertContainsPath(groups[0], basePath);

        // Group 1: utils (depends on base)
        Assert.Single(groups[1]);
        AssertContainsPath(groups[1], utilsPath);

        // Group 2: main (depends on utils)
        Assert.Single(groups[2]);
        AssertContainsPath(groups[2], mainPath);
    }

    #endregion
}
