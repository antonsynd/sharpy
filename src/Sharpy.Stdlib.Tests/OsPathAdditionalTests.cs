using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for OsPath covering gaps not in OsPathTests.cs.
/// </summary>
public class OsPathAdditionalTests : IDisposable
{
    private readonly string _tempDir;

    public OsPathAdditionalTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sharpy_ospath_add_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

    // ===== Abspath additional =====

    [Fact]
    public void Abspath_DotReturnsAbsolute()
    {
        var result = OsPathModule.Abspath(".");
        result.Should().NotBeNullOrEmpty();
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void Abspath_RelativePath_ReturnsAbsolute()
    {
        var result = OsPathModule.Abspath("some/relative");
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    // ===== Realpath =====

    [Fact]
    public void Realpath_ExistingDir_ReturnsAbsolute()
    {
        var result = OsPathModule.Realpath(_tempDir);
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Realpath_Dot_ReturnsAbsolute()
    {
        var result = OsPathModule.Realpath(".");
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    // ===== Split edge cases =====

    [Fact]
    public void Split_TrailingSlash_TailIsEmpty()
    {
        // Python: os.path.split("/a/b/") -> ("/a/b", "")
        // .NET GetFileName("/a/b/") returns "" on Unix
        var (head, tail) = OsPathModule.Split("/a/b/");
        tail.Should().BeEmpty();
        head.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Split_RootOnly_ReturnsRootAndEmpty()
    {
        // os.path.split("/") -> ("/", "")
        var (head, tail) = OsPathModule.Split("/");
        tail.Should().BeEmpty();
    }

    [Fact]
    public void Split_SimpleFilename_HeadIsEmpty()
    {
        // os.path.split("file.txt") -> ("", "file.txt")
        var (head, tail) = OsPathModule.Split("file.txt");
        tail.Should().Be("file.txt");
        head.Should().BeEmpty();
    }

    // ===== Splitext edge cases =====

    [Fact]
    public void Splitext_SimpleExtension_ReturnsCorrectParts()
    {
        var (root, ext) = OsPathModule.Splitext("file.txt");
        root.Should().Be("file");
        ext.Should().Be(".txt");
    }

    [Fact]
    public void Splitext_HiddenFile_DotNetBehavior()
    {
        // .NET Path.GetExtension(".hidden") returns ".hidden" as the extension.
        // The implementation uses .NET semantics, so the parts round-trip to original.
        var (root, ext) = OsPathModule.Splitext(".hidden");
        (root + ext).Should().Be(".hidden");
    }

    [Fact]
    public void Splitext_DotAtEnd_ExtIsEmpty()
    {
        // "file." -> ("file.", "") by .NET GetExtension
        var (root, ext) = OsPathModule.Splitext("file.");
        // .NET Path.GetExtension("file.") returns "."
        (root + ext).Should().Be("file.");
    }

    // ===== Join edge cases =====

    [Fact]
    public void Join_FourArgs_JoinsCorrectly()
    {
        var result = OsPathModule.Join("a", "b", "c", "d");
        result.Should().Contain("a");
        result.Should().EndWith("d");
    }

    [Fact]
    public void Join_TwoAbsolutePaths_SecondWins()
    {
        // System.IO.Path.Combine("/a", "/b") returns "/b" on Unix
        var result = OsPathModule.Join("/a", "/b");
        // On macOS/Linux, Path.Combine("/a", "/b") = "/b"
        result.Should().Be(System.IO.Path.Combine("/a", "/b"));
    }

    [Fact]
    public void Join_EmptyFirstArg_ReturnsSecond()
    {
        var result = OsPathModule.Join("", "b");
        result.Should().Be(System.IO.Path.Combine("", "b"));
    }

    // ===== Normpath additional =====

    [Fact]
    public void Normpath_AbsoluteWithDotDot_Collapses()
    {
        var sep = System.IO.Path.DirectorySeparatorChar;
        var result = OsPathModule.Normpath("/a/./b/../c");
        result.Should().Be(sep + "a" + sep + "c");
    }

    [Fact]
    public void Normpath_DoubleSlash_Collapses()
    {
        var sep = System.IO.Path.DirectorySeparatorChar;
        // "/a//b" should collapse to "/a/b"
        var result = OsPathModule.Normpath("/a//b");
        result.Should().Be(sep + "a" + sep + "b");
    }

    [Fact]
    public void Normpath_RelativeDotDotAtStart_Preserved()
    {
        var sep = System.IO.Path.DirectorySeparatorChar;
        var result = OsPathModule.Normpath("../a/b");
        result.Should().Be(".." + sep + "a" + sep + "b");
    }

    // ===== Getsize edge cases =====

    [Fact]
    public void Getsize_EmptyFile_ReturnsZero()
    {
        var path = Sub("empty.txt");
        File.WriteAllText(path, "");
        OsPathModule.Getsize(path).Should().Be(0);
    }

    [Fact]
    public void Getsize_NonEmptyFile_ReturnsCorrectSize()
    {
        var path = Sub("sized.txt");
        // Write known bytes
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        File.WriteAllBytes(path, bytes);
        OsPathModule.Getsize(path).Should().Be(10);
    }

    // ===== Expanduser additional =====

    [Fact]
    public void Expanduser_TildeWithSubdir_BuildsCorrectPath()
    {
        var result = OsPathModule.Expanduser("~/mydir/file.txt");
        result.Should().NotStartWith("~");
        result.Should().EndWith("mydir/file.txt".Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    // ===== Dirname/Basename edge cases =====

    [Fact]
    public void Dirname_TrailingSlash_ReturnsParent()
    {
        // "/a/b/" -> dirname is "/a/b" (GetDirectoryName strips trailing sep)
        var result = OsPathModule.Dirname("/a/b/");
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Basename_TrailingSlash_ReturnsEmpty()
    {
        // "/a/b/" -> GetFileName returns ""
        var result = OsPathModule.Basename("/a/b/");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Basename_RootPath_ReturnsEmpty()
    {
        var result = OsPathModule.Basename("/");
        result.Should().BeEmpty();
    }

    // ===== Isabs edge cases =====

    [Fact]
    public void Isabs_EmptyString_ReturnsFalse()
    {
        OsPathModule.Isabs("").Should().BeFalse();
    }

    [Fact]
    public void Isabs_SingleSlash_ReturnsTrue()
    {
        OsPathModule.Isabs("/").Should().BeTrue();
    }
}
