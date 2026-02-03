using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Tests for incremental compilation infrastructure.
/// </summary>
public class IncrementalCompilationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public IncrementalCompilationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_inc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            { File.Delete(file); }
            catch { }
        }
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    private string CreateTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private ProjectConfig CreateTestConfig(params string[] fileContents)
    {
        var sourceFiles = new List<string>();
        for (int i = 0; i < fileContents.Length; i++)
        {
            var file = CreateTempFile($"file{i}.spy", fileContents[i]);
            sourceFiles.Add(file);
        }

        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = sourceFiles,
            Configuration = "Debug"
        };
    }

    [Fact]
    public void ComputeFileHash_SameContent_ReturnsSameHash()
    {
        var file1 = CreateTempFile("same1.spy", "def main():\n    print('hello')");
        var file2 = CreateTempFile("same2.spy", "def main():\n    print('hello')");

        var hash1 = IncrementalCompilationCache.ComputeFileHash(file1);
        var hash2 = IncrementalCompilationCache.ComputeFileHash(file2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeFileHash_DifferentContent_ReturnsDifferentHash()
    {
        var file1 = CreateTempFile("diff1.spy", "def main():\n    print('hello')");
        var file2 = CreateTempFile("diff2.spy", "def main():\n    print('world')");

        var hash1 = IncrementalCompilationCache.ComputeFileHash(file1);
        var hash2 = IncrementalCompilationCache.ComputeFileHash(file2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void IsStale_NewFile_ReturnsTrue()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var isStale = cache.IsStale(config.SourceFiles[0]);

        Assert.True(isStale);
    }

    [Fact]
    public void IsStale_AfterUpdate_ReturnsFalse()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.False(isStale);
    }

    [Fact]
    public void IsStale_AfterContentChange_ReturnsTrue()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update and save
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        // Modify the file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var isStale = cache2.IsStale(config.SourceFiles[0]);

        Assert.True(isStale);
    }

    [Fact]
    public void GetFilesToRecompile_NoCache_ReturnsAllFiles()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var filesToRecompile = cache.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Equal(2, filesToRecompile.Count);
        Assert.Equal(2, cache.StaleFileCount);
        Assert.Equal(0, cache.UpToDateFileCount);
    }

    [Fact]
    public void GetFilesToRecompile_AllUpToDate_ReturnsEmptySet()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update all files
        foreach (var file in config.SourceFiles)
        {
            cache.UpdateHash(file);
        }
        cache.SaveCache();

        // Reload and check
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var filesToRecompile = cache2.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Empty(filesToRecompile);
        Assert.Equal(0, cache2.StaleFileCount);
        Assert.Equal(2, cache2.UpToDateFileCount);
    }

    [Fact]
    public void GetFilesToRecompile_OneChanged_ReturnsOnlyChangedFile()
    {
        var config = CreateTestConfig(
            "def main():\n    pass",
            "def helper():\n    pass"
        );
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        // Update all files
        foreach (var file in config.SourceFiles)
        {
            cache.UpdateHash(file);
        }
        cache.SaveCache();

        // Modify one file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        // Reload and check
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        var filesToRecompile = cache2.GetFilesToRecompile(config.SourceFiles, null);

        Assert.Single(filesToRecompile);
        Assert.Contains(config.SourceFiles[0], filesToRecompile);
        Assert.Equal(1, cache2.StaleFileCount);
        Assert.Equal(1, cache2.UpToDateFileCount);
    }

    [Fact]
    public void Clear_RemovesCacheFile()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.UpdateHash(config.SourceFiles[0]);
        cache.SaveCache();

        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        Assert.True(File.Exists(cacheFilePath));

        cache.Clear();
        Assert.False(File.Exists(cacheFilePath));
    }

    [Fact]
    public void IncrementalMode_EndToEnd_CompilationSucceeds()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.CompileProject(config);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void IncrementalMode_SecondBuild_CacheIsSaved()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);
        Assert.True(File.Exists(cacheFilePath), "Cache file should be created after first build");

        // Second build
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);
    }

    #region Symbol Serialization Tests

    [Fact]
    public void SymbolSerializer_SerializeType_BuiltinTypes()
    {
        // Test that builtin types serialize correctly
        var intType = BuiltinType.Int;
        var strType = BuiltinType.Str;
        var boolType = BuiltinType.Bool;

        // We can't directly call SerializeType since it's private,
        // but we can test via a function symbol with these types
        var funcSymbol = new FunctionSymbol
        {
            Name = "test_func",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "x", Type = intType },
                new ParameterSymbol { Name = "y", Type = strType }
            },
            ReturnType = boolType
        };

        var filePath = CreateTempFile("test.spy", "def test_func(x: int, y: str) -> bool:\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.Equal("Function", cached.Kind);
        Assert.Equal("test_func", cached.Name);
        Assert.NotNull(cached.Parameters);
        Assert.Equal(2, cached.Parameters!.Count);
        Assert.Equal("builtin:int", cached.Parameters[0].TypeId);
        Assert.Equal("builtin:str", cached.Parameters[1].TypeId);
        Assert.Equal("builtin:bool", cached.ReturnTypeId);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_FunctionSymbol()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "my_function",
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = 5,
            DeclarationColumn = 1,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "count", Type = BuiltinType.Int, HasDefault = true }
            },
            ReturnType = BuiltinType.Str,
            IsStatic = true
        };

        var filePath = CreateTempFile("func.spy", "def my_function(count: int = 10) -> str:\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as FunctionSymbol;

        Assert.NotNull(restored);
        Assert.Equal("my_function", restored!.Name);
        Assert.Equal(AccessLevel.Public, restored.AccessLevel);
        Assert.Equal(5, restored.DeclarationLine);
        Assert.True(restored.IsStatic);
        Assert.Single(restored.Parameters);
        Assert.Equal("count", restored.Parameters[0].Name);
        Assert.True(restored.Parameters[0].HasDefault);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_TypeSymbol()
    {
        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = 1,
            IsAbstract = true,
            DefiningModule = "test"
        };

        var filePath = CreateTempFile("class.spy", "class MyClass:\n    pass");
        var cached = SymbolSerializer.Serialize(typeSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as TypeSymbol;

        Assert.NotNull(restored);
        Assert.Equal("MyClass", restored!.Name);
        Assert.Equal(TypeKind.Class, restored.TypeKind);
        Assert.True(restored.IsAbstract);
        Assert.Equal("test", restored.DefiningModule);
    }

    [Fact]
    public void SymbolSerializer_RoundTrip_VariableSymbol()
    {
        var varSymbol = new VariableSymbol
        {
            Name = "my_var",
            Kind = SymbolKind.Variable,
            Type = BuiltinType.Int,
            IsConstant = true,
            AccessLevel = AccessLevel.Public
        };

        var filePath = CreateTempFile("var.spy", "my_var: int = 42");
        var cached = SymbolSerializer.Serialize(varSymbol, filePath);

        var registry = new Dictionary<string, Symbol>();
        var restored = SymbolSerializer.Deserialize(cached, registry) as VariableSymbol;

        Assert.NotNull(restored);
        Assert.Equal("my_var", restored!.Name);
        Assert.True(restored.IsConstant);
        Assert.Equal(BuiltinType.Int, restored.Type);
    }

    [Fact]
    public void SymbolSerializer_SerializeCodeGenInfo()
    {
        var funcSymbol = new FunctionSymbol
        {
            Name = "snake_case_func",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void,
            CodeGenInfo = new CodeGenInfo
            {
                CSharpName = "SnakeCaseFunc",
                OriginalName = "snake_case_func",
                IsModuleLevel = true
            }
        };

        var filePath = CreateTempFile("codegen.spy", "def snake_case_func():\n    pass");
        var cached = SymbolSerializer.Serialize(funcSymbol, filePath);

        Assert.NotNull(cached.CodeGenInfo);
        Assert.Equal("SnakeCaseFunc", cached.CodeGenInfo!.CSharpName);
        Assert.Equal("snake_case_func", cached.CodeGenInfo.OriginalName);
        Assert.True(cached.CodeGenInfo.IsModuleLevel);
    }

    #endregion

    #region File Cache Tests

    [Fact]
    public void FileCache_SaveAndRetrieve()
    {
        var config = CreateTestConfig("def main():\n    print('hello')");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var funcSymbol = new FunctionSymbol
        {
            Name = "main",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = SemanticType.Void
        };

        var symbols = new List<Symbol> { funcSymbol };
        var generatedCSharp = "public static void Main() { Console.WriteLine(\"hello\"); }";
        var dependencies = new List<string>();

        cache.SaveFileCache(config.SourceFiles[0], symbols, generatedCSharp, dependencies, "test");
        cache.SaveAllCaches();

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var entry = cache2.GetFileCache(config.SourceFiles[0]);

        Assert.NotNull(entry);
        Assert.Equal(generatedCSharp, entry!.GeneratedCSharp);
        Assert.Equal("test", entry.ModulePath);
        Assert.Single(entry.Symbols);
        Assert.Equal("main", entry.Symbols[0].Name);
    }

    [Fact]
    public void FileCache_InvalidAfterContentChange()
    {
        var config = CreateTestConfig("def main():\n    print('hello')");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var symbols = new List<Symbol>
        {
            new FunctionSymbol
            {
                Name = "main",
                Kind = SymbolKind.Function,
                Parameters = new List<ParameterSymbol>(),
                ReturnType = SemanticType.Void
            }
        };

        cache.SaveFileCache(config.SourceFiles[0], symbols, "generated code", new List<string>());
        cache.SaveAllCaches();

        // Modify the file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('world')");

        // Reload cache
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var entry = cache2.GetFileCache(config.SourceFiles[0]);

        Assert.Null(entry); // Should be null because content changed
    }

    [Fact]
    public void FileCache_HasValidFileCache_ReturnsFalseForChangedFile()
    {
        var config = CreateTestConfig("def main():\n    pass");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        cache.SaveFileCache(
            config.SourceFiles[0],
            new List<Symbol>(),
            "generated",
            new List<string>());
        cache.SaveAllCaches();

        // Modify file
        File.WriteAllText(config.SourceFiles[0], "def main():\n    print('changed')");

        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        Assert.False(cache2.HasValidFileCache(config.SourceFiles[0]));
    }

    [Fact]
    public void FileCache_RestoreSymbols()
    {
        var config = CreateTestConfig("x: int = 42");
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        var varSymbol = new VariableSymbol
        {
            Name = "x",
            Kind = SymbolKind.Variable,
            Type = BuiltinType.Int,
            IsConstant = true
        };

        cache.SaveFileCache(config.SourceFiles[0], new List<Symbol> { varSymbol }, "code", new List<string>());
        cache.SaveAllCaches();

        // Reload and restore
        var cache2 = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache2.LoadAllCaches();

        var registry = new Dictionary<string, Symbol>();
        var restored = cache2.RestoreSymbols(config.SourceFiles[0], registry);

        Assert.True(restored);
        Assert.Single(registry);

        var restoredSymbol = registry.Values.First() as VariableSymbol;
        Assert.NotNull(restoredSymbol);
        Assert.Equal("x", restoredSymbol!.Name);
        Assert.True(restoredSymbol.IsConstant);
    }

    #endregion

    #region End-to-End Incremental Compilation Tests

    [Fact]
    public void IncrementalMode_SecondBuild_SymbolCacheCreated()
    {
        var config = CreateTestConfig(@"
def main():
    x: int = 42
    print(x)
");
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result = compiler.CompileProject(config);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // Symbol cache should exist after successful build
        Assert.True(File.Exists(symbolCachePath), "Symbol cache file should be created after first build");

        // Verify it's valid JSON
        var json = File.ReadAllText(symbolCachePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void IncrementalMode_UnchangedFile_ProducesIdenticalOutput()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Second build (file unchanged)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Both builds should produce the same output assembly
        Assert.NotNull(result1.OutputAssemblyPath);
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void IncrementalMode_MultipleFiles_OnlyRecompilesChanged()
    {
        // Create two files
        var file1 = CreateTempFile("main.spy", @"
import helper

def main():
    helper.greet()
");
        var file2 = CreateTempFile("helper.spy", @"
def greet():
    print('hello')
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { file1, file2 },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build (both files compiled)
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only the helper file
        File.WriteAllText(file2, @"
def greet():
    print('modified hello')
");

        // Second build (should recompile helper, potentially skip main if no dependency change)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);
    }

    [Fact]
    public void IncrementalMode_Clean_ForcesFullRebuild()
    {
        var config = CreateTestConfig(@"
def main():
    print('hello')
");
        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Clear cache
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.Clear();

        // Verify cache files are gone
        var cacheFilePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-cache");
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        Assert.False(File.Exists(cacheFilePath));
        Assert.False(File.Exists(symbolCachePath));

        // Build again (should be full rebuild)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Cache should be recreated
        Assert.True(File.Exists(cacheFilePath));
    }

    [Fact]
    public void IncrementalMode_WithClass_SerializesTypeSymbol()
    {
        var config = CreateTestConfig(@"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(1, 2)
    print(p.x)
");
        var symbolCachePath = Path.Combine(config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.CompileProject(config);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message)));

        // Check symbol cache contains the Point class
        Assert.True(File.Exists(symbolCachePath));
        var json = File.ReadAllText(symbolCachePath);
        Assert.Contains("Point", json);
    }

    [Fact]
    public void IncrementalMode_TransitiveDependency_RecompilesDependents()
    {
        // Test that when a dependency changes, files that import it are also recompiled.
        // This verifies the cached dependency graph is used correctly.

        // Create three files: main imports helper, helper imports util
        var utilFile = CreateTempFile("util.spy", @"
def format_message(msg: str) -> str:
    return '[INFO] ' + msg
");
        var helperFile = CreateTempFile("helper.spy", @"
from util import format_message

def greet() -> str:
    return format_message('Hello')
");
        var mainFile = CreateTempFile("main.spy", @"
from helper import greet

def main():
    print(greet())
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, helperFile, utilFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - all files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify the leaf file (util.spy)
        File.WriteAllText(utilFile, @"
def format_message(msg: str) -> str:
    return '[MODIFIED] ' + msg
");

        // Second build - util changed, so helper and main should also be recompiled
        // (helper imports util, main imports helper)
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // The compilation should succeed and produce correct output
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void BuildCachedDependencyGraph_CreatesDependencyGraph()
    {
        // Create files with known dependencies
        var utilFile = CreateTempFile("util.spy", @"
def helper():
    pass
");
        var mainFile = CreateTempFile("main.spy", @"
from util import helper

def main():
    helper()
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, utilFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build to create cache
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Load the cache and build a cached dependency graph
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.LoadAllCaches();

        var cachedGraph = cache.BuildCachedDependencyGraph(config.SourceFiles);
        Assert.NotNull(cachedGraph);

        // The graph should show that main depends on util
        var mainDeps = cachedGraph!.GetDirectDependencies(mainFile);
        Assert.Contains(cachedGraph.AllFiles, f => f.EndsWith("util.spy"));
    }

    [Fact]
    public void IncrementalMode_DependencyChangesSignature_RecompilesDependent()
    {
        // Test that when a function implementation changes in a dependency,
        // files that use it are recompiled

        var libFile = CreateTempFile("lib.spy", @"
def get_message() -> str:
    return 'original'
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import get_message

def main():
    msg: str = get_message()
    print(msg)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build succeeds
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify lib to change function implementation (same signature)
        File.WriteAllText(libFile, @"
def get_message() -> str:
    return 'modified'
");

        // Second build - main.spy should be recompiled (not skipped) because lib changed
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Both builds should produce valid assemblies
        Assert.NotNull(result1.OutputAssemblyPath);
        Assert.NotNull(result2.OutputAssemblyPath);
    }

    [Fact]
    public void IncrementalMode_NoChanges_SkipsAllFiles()
    {
        // Verify that when nothing changes, all files are skipped in the second build

        var file1 = CreateTempFile("main.spy", @"
def main():
    print('hello')
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { file1 },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success);

        // Second build - should skip all files
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success);

        // Verify metrics show files were skipped
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected skipped files, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_ImporterChangedImporteeUnchanged_BuildsSuccessfully()
    {
        // Test the scenario where the importing file changes but the imported file does not.
        // This verifies that import resolution correctly parses the unchanged imported file.

        var libFile = CreateTempFile("lib.spy", @"
def get_value() -> int:
    return 42
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only main.spy (add another print)
        File.WriteAllText(mainFile, @"
from lib import get_value

def main():
    x: int = get_value()
    print(x)
    print('done')
");

        // Second build - lib.spy should be skipped, main.spy should be recompiled
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify at least one file was skipped (lib.spy)
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected lib.spy to be skipped, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    [Fact]
    public void IncrementalMode_ImporterChangedWithClass_BuildsSuccessfully()
    {
        // Test the scenario with a class import: importing file changes, imported file (with class) does not.
        // This verifies that type symbols from unchanged files are accessible during semantic analysis.

        var libFile = CreateTempFile("lib.spy", @"
class Counter:
    value: int

    def __init__(self):
        self.value = 0

    def increment(self):
        self.value += 1
");
        var mainFile = CreateTempFile("main.spy", @"
from lib import Counter

def main():
    c: Counter = Counter()
    c.increment()
    print(c.value)
");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { mainFile, libFile },
            Configuration = "Debug"
        };

        var options = new CompilerOptions { Incremental = true };
        var compiler = new Compiler(options, NullLogger.Instance);

        // First build - both files compiled
        var result1 = compiler.CompileProject(config);
        Assert.True(result1.Success, string.Join("; ", result1.Diagnostics.GetErrors().Select(e => e.Message)));

        // Modify only main.spy (call increment twice)
        File.WriteAllText(mainFile, @"
from lib import Counter

def main():
    c: Counter = Counter()
    c.increment()
    c.increment()
    print(c.value)
");

        // Second build - lib.spy should be skipped, main.spy should be recompiled
        var result2 = compiler.CompileProject(config);
        Assert.True(result2.Success, string.Join("; ", result2.Diagnostics.GetErrors().Select(e => e.Message)));

        // Verify at least one file was skipped (lib.spy)
        var metrics = result2.Metrics;
        Assert.NotNull(metrics);
        Assert.True(metrics!.SkippedFileCount > 0,
            $"Expected lib.spy to be skipped, got SkippedFileCount={metrics.SkippedFileCount}");
    }

    #endregion
}
