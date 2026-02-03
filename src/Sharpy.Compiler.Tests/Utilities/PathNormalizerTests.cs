using Sharpy.Compiler.Utilities;
using Xunit;

namespace Sharpy.Compiler.Tests.Utilities;

public class PathNormalizerTests
{
    [Fact]
    public void NormalizesRelativeToAbsolute()
    {
        var normalized = PathNormalizer.Normalize("./foo/bar.spy");

        Assert.True(Path.IsPathRooted(normalized));
        Assert.Contains("foo/bar.spy", normalized);
    }

    [Fact]
    public void ConvertsBackslashesToForwardSlashes()
    {
        // Use a path that works on all platforms
        var testPath = Path.Combine("foo", "bar", "test.spy");
        var normalized = PathNormalizer.Normalize(testPath);

        Assert.DoesNotContain("\\", normalized);
        Assert.Contains("/", normalized);
    }

    [Fact]
    public void SameFileGetsSameNormalization()
    {
        // Different ways to refer to the same file should get the same normalized path
        var abs = Path.GetFullPath("test.spy");
        var rel = "./test.spy";
        var dotdot = "../" + Path.GetFileName(Directory.GetCurrentDirectory()) + "/test.spy";

        var n1 = PathNormalizer.Normalize(abs);
        var n2 = PathNormalizer.Normalize(rel);
        var n3 = PathNormalizer.Normalize(dotdot);

        Assert.Equal(n1, n2);
        Assert.Equal(n2, n3);
    }

    [Fact]
    public void HandlesEmptyString()
    {
        Assert.Equal("", PathNormalizer.Normalize(""));
    }

    [Fact]
    public void HandlesNullString()
    {
        Assert.Null(PathNormalizer.Normalize(null!));
    }

    [Fact]
    public void ResolvesDotComponents()
    {
        // Test that ./foo resolves correctly
        var currentDir = Directory.GetCurrentDirectory();
        var withDot = Path.Combine(currentDir, ".", "test.spy");
        var withoutDot = Path.Combine(currentDir, "test.spy");

        var normalizedWithDot = PathNormalizer.Normalize(withDot);
        var normalizedWithoutDot = PathNormalizer.Normalize(withoutDot);

        Assert.Equal(normalizedWithDot, normalizedWithoutDot);
    }

    [Fact]
    public void ResolvesDotDotComponents()
    {
        // Test that ../current_dir/foo resolves correctly
        var currentDir = Directory.GetCurrentDirectory();
        var parentDir = Path.GetDirectoryName(currentDir) ?? currentDir;
        var currentDirName = Path.GetFileName(currentDir);
        var withDotDot = Path.Combine(currentDir, "..", currentDirName, "test.spy");
        var direct = Path.Combine(currentDir, "test.spy");

        var normalizedWithDotDot = PathNormalizer.Normalize(withDotDot);
        var normalizedDirect = PathNormalizer.Normalize(direct);

        Assert.Equal(normalizedWithDotDot, normalizedDirect);
    }

    [Theory]
    [InlineData("a/b/c.spy")]
    [InlineData("./a/b/c.spy")]
    [InlineData("a/./b/c.spy")]
    public void AlwaysReturnsAbsolutePath(string input)
    {
        var normalized = PathNormalizer.Normalize(input);

        Assert.True(Path.IsPathRooted(normalized), $"Expected absolute path but got: {normalized}");
    }

    [Fact]
    public void GetRelativeReturnsForwardSlashes()
    {
        var basePath = Path.GetFullPath(".");
        var targetPath = Path.Combine(basePath, "foo", "bar", "test.spy");

        var relative = PathNormalizer.GetRelative(basePath, targetPath);

        Assert.DoesNotContain("\\", relative);
        Assert.Equal("foo/bar/test.spy", relative);
    }

    [Fact]
    public void CaseHandlingDependsOnPlatform()
    {
        var lowerPath = PathNormalizer.Normalize("test.spy");
        var upperPath = PathNormalizer.Normalize("TEST.SPY");

        if (OperatingSystem.IsLinux())
        {
            // Linux is case-sensitive, so paths should differ
            Assert.NotEqual(lowerPath, upperPath);
        }
        else
        {
            // Windows and macOS are case-insensitive, paths should be equal
            Assert.Equal(lowerPath, upperPath);
        }
    }
}
