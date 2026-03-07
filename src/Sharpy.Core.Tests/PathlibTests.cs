using System;
using System.IO;
using FluentAssertions;
using Xunit;
using Path = Sharpy.Path;
using SysPath = System.IO.Path;

namespace Sharpy.Core.Tests;

public class PathlibTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _tempDirs = new();
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    private string CreateTempDir()
    {
        var path = SysPath.Combine(SysPath.GetTempPath(), "sharpy_pathlib_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private string CreateTempFile(string content = "hello")
    {
        var path = SysPath.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try
            { File.Delete(f); }
            catch { }
        }
        foreach (var d in _tempDirs)
        {
            try
            { Directory.Delete(d, true); }
            catch { }
        }
    }

    // ===== Constructor =====

    [Fact]
    public void Constructor_Stores_Path()
    {
        var p = new Path("/a/b/c");
        p.ToString().Should().Be("/a/b/c");
    }

    // ===== Operator / =====

    [Fact]
    public void Slash_Operator_Joins_With_String()
    {
        var p = new Path("/a") / "b";
        p.ToString().Should().Be(SysPath.Combine("/a", "b"));
    }

    [Fact]
    public void Slash_Operator_Joins_With_Path()
    {
        var p = new Path("/a") / new Path("b");
        p.ToString().Should().Be(SysPath.Combine("/a", "b"));
    }

    // ===== Properties =====

    [Fact]
    public void Name_Returns_Filename()
    {
        new Path("/a/b/c.txt").Name.Should().Be("c.txt");
    }

    [Fact]
    public void Stem_Returns_Name_Without_Extension()
    {
        new Path("/a/b/c.tar.gz").Stem.Should().Be("c.tar");
    }

    [Fact]
    public void Suffix_Returns_Extension()
    {
        new Path("/a/b/c.tar.gz").Suffix.Should().Be(".gz");
    }

    [Fact]
    public void Suffix_Empty_When_No_Extension()
    {
        new Path("/a/b/c").Suffix.Should().BeEmpty();
    }

    [Fact]
    public void Suffixes_Returns_All_Extensions()
    {
        var suffixes = new Path("/a/b/c.tar.gz").Suffixes;
        suffixes.Should().HaveCount(2);
        suffixes[0].Should().Be(".tar");
        suffixes[1].Should().Be(".gz");
    }

    [Fact]
    public void Parent_Returns_Parent_Directory()
    {
        new Path("/a/b/c").Parent.ToString().Should().Be("/a/b");
    }

    [Fact]
    public void Parts_Absolute_Includes_Root()
    {
        var parts = new Path("/a/b/c").Parts;
        parts[0].Should().Be("/");
        parts[1].Should().Be("a");
        parts[2].Should().Be("b");
        parts[3].Should().Be("c");
    }

    [Fact]
    public void Parts_Relative_No_Root()
    {
        var parts = new Path("a/b/c").Parts;
        parts[0].Should().Be("a");
        parts[1].Should().Be("b");
        parts[2].Should().Be("c");
    }

    [Fact]
    public void Root_Absolute()
    {
        new Path("/a/b").Root.Should().Be("/");
    }

    [Fact]
    public void Root_Relative_Empty()
    {
        new Path("a/b").Root.Should().BeEmpty();
    }

    [Fact]
    public void IsAbsolute_True()
    {
        new Path("/a/b").IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void IsAbsolute_False()
    {
        new Path("a/b").IsAbsolute.Should().BeFalse();
    }

    // ===== Query methods =====

    [Fact]
    public void Exists_True_For_File()
    {
        var path = CreateTempFile();
        new Path(path).Exists().Should().BeTrue();
    }

    [Fact]
    public void Exists_False_For_Nonexistent()
    {
        new Path("/tmp/nonexistent_" + Guid.NewGuid()).Exists().Should().BeFalse();
    }

    [Fact]
    public void IsFile_Returns_True_For_File()
    {
        var path = CreateTempFile();
        new Path(path).IsFile().Should().BeTrue();
    }

    [Fact]
    public void IsDir_Returns_True_For_Directory()
    {
        var dir = CreateTempDir();
        new Path(dir).IsDir().Should().BeTrue();
    }

    // ===== File I/O =====

    [Fact]
    public void ReadText_And_WriteText()
    {
        var dir = CreateTempDir();
        var filePath = SysPath.Combine(dir, "test.txt");
        _tempFiles.Add(filePath);

        var p = new Path(filePath);
        p.WriteText("hello world");
        p.ReadText().Should().Be("hello world");
    }

    [Fact]
    public void ReadBytes_And_WriteBytes()
    {
        var dir = CreateTempDir();
        var filePath = SysPath.Combine(dir, "test.bin");
        _tempFiles.Add(filePath);

        var p = new Path(filePath);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        p.WriteBytes(data);
        p.ReadBytes().Should().Equal(data);
    }

    [Fact]
    public void ReadText_Nonexistent_Throws()
    {
        var p = new Path("/tmp/nonexistent_" + Guid.NewGuid());
        var act = () => p.ReadText();
        act.Should().Throw<FileNotFoundError>();
    }

    // ===== Directory operations =====

    [Fact]
    public void Mkdir_Creates_Directory()
    {
        var root = CreateTempDir();
        var sub = SysPath.Combine(root, "sub");
        new Path(sub).Mkdir();
        Directory.Exists(sub).Should().BeTrue();
    }

    [Fact]
    public void Mkdir_Parents_Creates_Nested()
    {
        var root = CreateTempDir();
        var nested = SysPath.Combine(root, "a", "b", "c");
        new Path(nested).Mkdir(parents: true);
        Directory.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Mkdir_ExistOk_No_Error()
    {
        var dir = CreateTempDir();
        var act = () => new Path(dir).Mkdir(exist_ok: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Rmdir_Removes_Empty()
    {
        var dir = CreateTempDir();
        new Path(dir).Rmdir();
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void Iterdir_Lists_Entries()
    {
        var dir = CreateTempDir();
        File.WriteAllText(SysPath.Combine(dir, "a.txt"), "");
        File.WriteAllText(SysPath.Combine(dir, "b.txt"), "");

        var entries = new System.Collections.Generic.List<Path>();
        foreach (var entry in new Path(dir).Iterdir())
        {
            entries.Add(entry);
        }
        entries.Should().HaveCount(2);
    }

    // ===== Mutation =====

    [Fact]
    public void Rename_Moves_File()
    {
        var file = CreateTempFile("content");
        var dest = file + "_renamed";
        _tempFiles.Add(dest);

        var result = new Path(file).Rename(dest);
        result.ToString().Should().Be(dest);
        File.Exists(dest).Should().BeTrue();
        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void Unlink_Deletes_File()
    {
        var file = CreateTempFile();
        new Path(file).Unlink();
        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void Unlink_MissingOk_True_No_Error()
    {
        var act = () => new Path("/tmp/nonexistent_" + Guid.NewGuid()).Unlink(missing_ok: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Unlink_MissingOk_False_Throws()
    {
        var act = () => new Path("/tmp/nonexistent_" + Guid.NewGuid()).Unlink(missing_ok: false);
        act.Should().Throw<FileNotFoundError>();
    }

    // ===== Navigation =====

    [Fact]
    public void Resolve_Returns_Absolute()
    {
        var p = new Path("relative/path").Resolve();
        p.IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void WithName_Changes_Name()
    {
        new Path("/a/b/c.txt").WithName("d.txt").ToString().Should().Be("/a/b/d.txt");
    }

    [Fact]
    public void WithStem_Changes_Stem()
    {
        new Path("/a/b/c.tar.gz").WithStem("d").ToString().Should().Be("/a/b/d.gz");
    }

    [Fact]
    public void WithSuffix_Changes_Suffix()
    {
        new Path("/a/b/c.tar.gz").WithSuffix(".txt").ToString().Should().Be("/a/b/c.tar.txt");
    }

    [Fact]
    public void RelativeTo_Returns_Relative_Path()
    {
        var p = new Path("/a/b/c").RelativeTo(new Path("/a"));
        p.ToString().Should().Be("b" + SysPath.DirectorySeparatorChar + "c");
    }

    [Fact]
    public void RelativeTo_Not_Subpath_Throws()
    {
        var act = () => new Path("/a/b").RelativeTo(new Path("/c"));
        act.Should().Throw<ValueError>();
    }

    // ===== Equality =====

    [Fact]
    public void Equals_Same_Path()
    {
        var a = new Path("/a/b");
        var b = new Path("/a/b");
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void NotEquals_Different_Path()
    {
        var a = new Path("/a/b");
        var b = new Path("/a/c");
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_Same_For_Equal()
    {
        var a = new Path("/a/b");
        var b = new Path("/a/b");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
