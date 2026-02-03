using Sharpy.Compiler.Project;
using Sharpy.Compiler.Logging;
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
            try { File.Delete(file); } catch { }
        }
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string CreateTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
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
}
