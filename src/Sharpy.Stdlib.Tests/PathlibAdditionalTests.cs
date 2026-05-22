using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for Pathlib.Path covering gaps not in PathlibTests.cs.
/// </summary>
public class PathlibAdditionalTests : IDisposable
{
    private readonly string _tempDir;

    public PathlibAdditionalTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sharpy_pathlib_add_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

    // ===== Constructor edge cases =====

    [Fact]
    public void Constructor_Null_ThrowsTypeError()
    {
        Action act = () => _ = new Sharpy.Path(null!);
        act.Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void Constructor_ThreeSegments_JoinsAll()
    {
        var p = new Sharpy.Path("a", "b", "c");
        p.ToString().Should().Contain("a");
        p.ToString().Should().EndWith("c");
    }

    // ===== WithName edge cases =====

    [Fact]
    public void WithName_OnRootLevel_ReturnsNewName()
    {
        // "/file.txt".WithName("other.txt") -> "/other.txt" or just "other.txt"
        var p = new Sharpy.Path("/file.txt").WithName("other.txt");
        p.Name.Should().Be("other.txt");
    }

    [Fact]
    public void WithSuffix_EmptySuffix_RemovesExtension()
    {
        var p = new Sharpy.Path("/some/file.txt").WithSuffix("");
        p.Name.Should().Be("file");
    }

    [Fact]
    public void WithSuffix_ChangesOnlyLastSuffix()
    {
        var p = new Sharpy.Path("/some/archive.tar.gz").WithSuffix(".bz2");
        p.Name.Should().Be("archive.tar.bz2");
    }

    // ===== Operator / with Path =====

    [Fact]
    public void Division_ChainedOperations()
    {
        var p = new Sharpy.Path("/root") / "a" / "b" / "c";
        p.ToString().Should().Contain("a");
        p.ToString().Should().EndWith("c");
    }

    [Fact]
    public void Division_PathAndPath_JoinsCorrectly()
    {
        var left = new Sharpy.Path("/base");
        var right = new Sharpy.Path("child");
        var result = left / right;
        result.ToString().Should().Contain("base");
        result.ToString().Should().Contain("child");
    }

    // ===== Equality operator =====

    [Fact]
    public void EqualityOperator_EqualPaths_ReturnsTrue()
    {
        var p1 = new Sharpy.Path("/a/b");
        var p2 = new Sharpy.Path("/a/b");
        (p1 == p2).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentPaths_ReturnsFalse()
    {
        var p1 = new Sharpy.Path("/a/b");
        var p2 = new Sharpy.Path("/a/c");
        (p1 != p2).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_NullComparison()
    {
        Sharpy.Path? p = null;
        (p == null).Should().BeTrue();
    }

    // ===== Suffixes =====

    [Fact]
    public void Suffixes_SingleExtension_ReturnsOneItem()
    {
        var p = new Sharpy.Path("file.txt");
        var suffixes = p.Suffixes;
        ((ICollection<string>)suffixes).Count.Should().Be(1);
        suffixes[0].Should().Be(".txt");
    }

    [Fact]
    public void Suffixes_NoExtension_ReturnsEmptyList()
    {
        var p = new Sharpy.Path("noextension");
        var suffixes = p.Suffixes;
        ((ICollection<string>)suffixes).Count.Should().Be(0);
    }

    // ===== Parent =====

    [Fact]
    public void Parent_MultiLevel_ReturnsImmediateParent()
    {
        var p = new Sharpy.Path("/a/b/c/d");
        p.Parent.ToString().Should().Contain("c");
    }

    [Fact]
    public void Parent_OfParent_GrandParent()
    {
        var p = new Sharpy.Path("/a/b/c");
        var grandParent = p.Parent.Parent;
        grandParent.ToString().Should().Contain("a");
    }

    // ===== IsSymlink =====

    [Fact]
    public void IsSymlink_ForDirectory_ReturnsFalse()
    {
        new Sharpy.Path(_tempDir).IsSymlink().Should().BeFalse();
    }

    // ===== Mkdir exist_ok =====

    [Fact]
    public void Mkdir_ExistOk_DoesNotThrowWhenExists()
    {
        var p = new Sharpy.Path(Sub("existing_dir"));
        p.Mkdir();
        Action act = () => p.Mkdir(exist_ok: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Mkdir_ExistOkFalse_ThrowsWhenExists()
    {
        var p = new Sharpy.Path(Sub("dup_dir"));
        p.Mkdir();
        Action act = () => p.Mkdir(exist_ok: false);
        act.Should().Throw<Sharpy.FileExistsError>();
    }

    [Fact]
    public void Mkdir_MissingParent_ThrowsWithoutParentsFlag()
    {
        var p = new Sharpy.Path(Sub("missing/child"));
        Action act = () => p.Mkdir();
        act.Should().Throw<Sharpy.FileNotFoundError>();
    }

    // ===== ReadText/WriteText with encoding =====

    [Fact]
    public void WriteText_And_ReadText_WithAsciiEncoding()
    {
        var p = new Sharpy.Path(Sub("ascii.txt"));
        p.WriteText("hello ascii", "ascii");
        p.ReadText("ascii").Should().Be("hello ascii");
    }

    [Fact]
    public void ReadText_UnknownEncoding_ThrowsLookupError()
    {
        var p = new Sharpy.Path(Sub("enc.txt"));
        p.WriteText("data");
        Action act = () => p.ReadText("nonexistent-encoding");
        act.Should().Throw<Sharpy.LookupError>();
    }

    // ===== WriteBytes / ReadBytes additional =====

    [Fact]
    public void WriteBytes_EmptyData_CreatesEmptyFile()
    {
        var p = new Sharpy.Path(Sub("empty.bin"));
        p.WriteBytes(Array.Empty<byte>());
        p.ReadBytes().Should().BeEmpty();
    }

    // ===== Touch edge cases =====

    [Fact]
    public void Touch_MissingParentDir_ThrowsFileNotFoundError()
    {
        var p = new Sharpy.Path(Sub("no_parent/file.txt"));
        Action act = () => p.Touch();
        act.Should().Throw<Sharpy.FileNotFoundError>();
    }

    // ===== Iterdir =====

    [Fact]
    public void Iterdir_NonexistentDir_ThrowsFileNotFoundError()
    {
        var p = new Sharpy.Path(Sub("no_such_dir"));
        Action act = () =>
        {
            foreach (var _ in p.Iterdir())
            { }
        };
        act.Should().Throw<Sharpy.FileNotFoundError>();
    }

    [Fact]
    public void Iterdir_EmptyDir_YieldsNothing()
    {
        var dir = Sub("empty_iterdir");
        Directory.CreateDirectory(dir);
        var entries = new List<Sharpy.Path>();
        foreach (var entry in new Sharpy.Path(dir).Iterdir())
        {
            entries.Add(entry);
        }
        entries.Should().BeEmpty();
    }

    // ===== Glob edge cases =====

    [Fact]
    public void Glob_NoMatches_YieldsNothing()
    {
        var dir = Sub("glob_empty");
        Directory.CreateDirectory(dir);
        File.WriteAllText(System.IO.Path.Combine(dir, "file.txt"), "");
        var matches = new List<Sharpy.Path>();
        foreach (var p in new Sharpy.Path(dir).Glob("*.py"))
        {
            matches.Add(p);
        }
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Glob_NonexistentDir_ThrowsFileNotFoundError()
    {
        var p = new Sharpy.Path(Sub("no_such_glob_dir"));
        Action act = () =>
        {
            foreach (var _ in p.Glob("*"))
            { }
        };
        act.Should().Throw<Sharpy.FileNotFoundError>();
    }

    // ===== RelativeTo edge cases =====

    [Fact]
    public void RelativeTo_NormalizesBeforeComparing()
    {
        // Both paths resolve to the same real path
        var child = new Sharpy.Path(System.IO.Path.Combine(_tempDir, "sub"));
        var relative = child.RelativeTo(_tempDir);
        relative.ToString().Should().Be("sub");
    }

    // ===== Match additional =====

    [Fact]
    public void Match_QuestionMarkWildcard_MatchesSingleChar()
    {
        new Sharpy.Path("/a/b/fil?.txt").Match("fil?.txt").Should().BeTrue();
    }

    [Fact]
    public void Match_NoMatch_ReturnsFalse()
    {
        new Sharpy.Path("/a/b/file.txt").Match("*.py").Should().BeFalse();
    }

    // ===== Rglob empty dir =====

    [Fact]
    public void Rglob_EmptyDir_YieldsNothing()
    {
        var dir = Sub("rglob_empty");
        Directory.CreateDirectory(dir);
        var matches = new List<Sharpy.Path>();
        foreach (var p in new Sharpy.Path(dir).Rglob("*.txt"))
        {
            matches.Add(p);
        }
        matches.Should().BeEmpty();
    }

    // ===== Cwd / Home additional =====

    [Fact]
    public void Cwd_IsDirectory()
    {
        var cwd = Sharpy.Path.Cwd();
        cwd.IsDir().Should().BeTrue();
    }

    [Fact]
    public void Home_IsAbsoluteDir()
    {
        var home = Sharpy.Path.Home();
        home.IsAbsolute.Should().BeTrue();
        home.IsDir().Should().BeTrue();
    }
}
