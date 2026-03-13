using FluentAssertions;
using Sharpy.Compiler.Text;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Compiler.Tests.Text;

public class LazySourceTextTests : IDisposable
{
    private readonly string _tempDir;

    public LazySourceTextTests()
    {
        _tempDir = IOPath.Combine(
            IOPath.GetTempPath(),
            $"sharpy_lazy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Constructor_DoesNotReadFile()
    {
        var filePath = IOPath.Combine(_tempDir, "test.spy");
        File.WriteAllText(filePath, "x: int = 42");

        var lazy = new LazySourceText(filePath);

        lazy.IsLoaded.Should().BeFalse();
        lazy.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void Materialize_ReadsFile()
    {
        var filePath = IOPath.Combine(_tempDir, "test.spy");
        File.WriteAllText(filePath, "x: int = 42");

        var lazy = new LazySourceText(filePath);
        var sourceText = lazy.Materialize();

        lazy.IsLoaded.Should().BeTrue();
        sourceText.Should().NotBeNull();
        sourceText.ToString().Should().Be("x: int = 42");
        sourceText.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void IsLoaded_FalseBeforeAccess_TrueAfter()
    {
        var filePath = IOPath.Combine(_tempDir, "test.spy");
        File.WriteAllText(filePath, "hello");

        var lazy = new LazySourceText(filePath);
        lazy.IsLoaded.Should().BeFalse();

        _ = lazy.Length;
        lazy.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public void Materialize_NonexistentFile_Throws()
    {
        var filePath = IOPath.Combine(_tempDir, "nonexistent.spy");

        var lazy = new LazySourceText(filePath);

        var act = () => lazy.Materialize();
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ToString_DoesNotForceLoad_WhenNotLoaded()
    {
        var filePath = IOPath.Combine(_tempDir, "test.spy");
        File.WriteAllText(filePath, "def main():\n    pass");

        var lazy = new LazySourceText(filePath);
        lazy.IsLoaded.Should().BeFalse();

        var text = lazy.ToString();
        lazy.IsLoaded.Should().BeFalse();
        text.Should().Contain(filePath);
        text.Should().Contain("not loaded");
    }

    [Fact]
    public void ToString_ReturnsContent_WhenLoaded()
    {
        var filePath = IOPath.Combine(_tempDir, "test.spy");
        File.WriteAllText(filePath, "def main():\n    pass");

        var lazy = new LazySourceText(filePath);
        _ = lazy.Materialize(); // force load
        lazy.IsLoaded.Should().BeTrue();

        var text = lazy.ToString();
        text.Should().Be("def main():\n    pass");
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
