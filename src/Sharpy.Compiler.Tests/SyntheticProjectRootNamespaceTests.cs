using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Pins the root namespace <see cref="SyntheticProject.BuildConfig"/> chooses for an entry-file
/// compile (#1171). A single-file closure must keep the empty root namespace the #1038 contract
/// promises (its generated C# is byte-compared by snapshot fixtures); a multi-file closure must get
/// a namespace, or cross-module <c>Module.Type</c> references cannot bind (CS0426 → SPY0908).
/// </summary>
public class SyntheticProjectRootNamespaceTests : IDisposable
{
    private readonly string _tempDir;

    public SyntheticProjectRootNamespaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_synthns_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
        GC.SuppressFinalize(this);
    }

    private string WriteFile(string name, string source)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, source);
        return path;
    }

    private ProjectConfig BuildConfig(string entryPath, string? @namespace = null)
    {
        var options = new CompilerOptions { OutputType = "exe", Namespace = @namespace };
        return SyntheticProject.BuildConfig(
            File.ReadAllText(entryPath), entryPath, options, NullLogger.Instance);
    }

    [Fact]
    public void SingleFileClosure_KeepsEmptyRootNamespace()
    {
        var entry = WriteFile("solo.spy", "print(\"hi\")\n");

        var config = BuildConfig(entry);

        Assert.Single(config.SourceFiles);
        Assert.Equal(string.Empty, config.RootNamespace);
    }

    [Fact]
    public void MultiFileClosure_GetsDefaultRootNamespace()
    {
        WriteFile("models.spy", "class Person:\n    name: str\n\n    def __init__(self, name: str):\n        self.name = name\n");
        var entry = WriteFile("main.spy", "from models import Person\n\ndef main():\n    print(Person(\"Alice\").name)\n");

        var config = BuildConfig(entry);

        Assert.Equal(2, config.SourceFiles.Count);
        Assert.Equal(SyntheticProject.DefaultMultiFileRootNamespace, config.RootNamespace);
    }

    [Fact]
    public void CallerSuppliedNamespace_WinsForSingleFileClosure()
    {
        var entry = WriteFile("solo.spy", "print(\"hi\")\n");

        var config = BuildConfig(entry, @namespace: "Game.Scripts");

        Assert.Equal("Game.Scripts", config.RootNamespace);
    }

    [Fact]
    public void CallerSuppliedNamespace_WinsForMultiFileClosure()
    {
        WriteFile("models.spy", "class Person:\n    name: str\n\n    def __init__(self, name: str):\n        self.name = name\n");
        var entry = WriteFile("main.spy", "from models import Person\n\ndef main():\n    print(Person(\"Alice\").name)\n");

        var config = BuildConfig(entry, @namespace: "Game.Scripts");

        Assert.Equal(2, config.SourceFiles.Count);
        Assert.Equal("Game.Scripts", config.RootNamespace);
    }

    /// <summary>
    /// The default must be a fixed constant, not something derived from the entry file or its
    /// directory: a derived value could equal a module-derived namespace segment, and then the
    /// module's static class would shadow the namespace holding its own types — the very CS0426
    /// this default prevents.
    /// </summary>
    [Fact]
    public void DefaultRootNamespace_DoesNotDependOnTheEntryFileName()
    {
        WriteFile("models.spy", "class Person:\n    name: str\n\n    def __init__(self, name: str):\n        self.name = name\n");
        var main = WriteFile("main.spy", "from models import Person\n\ndef main():\n    print(Person(\"Alice\").name)\n");
        var app = WriteFile("app.spy", "from models import Person\n\ndef main():\n    print(Person(\"Bob\").name)\n");

        Assert.Equal(BuildConfig(main).RootNamespace, BuildConfig(app).RootNamespace);
        Assert.Equal(SyntheticProject.DefaultMultiFileRootNamespace, BuildConfig(main).RootNamespace);
    }
}
