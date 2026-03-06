using System;
using System.IO;
using FluentAssertions;
using Sharpy;
using Xunit;

namespace Sharpy.Core.Tests;

public class OsPathTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    private string CreateTempFile(string content = "hello")
    {
        var path = System.IO.Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    [Fact]
    public void Join_Two_Parts()
    {
        OsPath.Join("/a", "b").Should().Be(System.IO.Path.Combine("/a", "b"));
    }

    [Fact]
    public void Join_Multiple_Parts()
    {
        OsPath.Join("a", "b", "c").Should().Be(System.IO.Path.Combine("a", "b", "c"));
    }

    [Fact]
    public void Exists_True_For_File()
    {
        var path = CreateTempFile();
        OsPath.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Exists_True_For_Directory()
    {
        OsPath.Exists(System.IO.Path.GetTempPath()).Should().BeTrue();
    }

    [Fact]
    public void Exists_False_For_Nonexistent()
    {
        OsPath.Exists("/tmp/nonexistent_" + Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Isfile_Returns_True_For_File()
    {
        var path = CreateTempFile();
        OsPath.Isfile(path).Should().BeTrue();
    }

    [Fact]
    public void Isfile_Returns_False_For_Directory()
    {
        OsPath.Isfile(System.IO.Path.GetTempPath()).Should().BeFalse();
    }

    [Fact]
    public void Isdir_Returns_True_For_Directory()
    {
        OsPath.Isdir(System.IO.Path.GetTempPath()).Should().BeTrue();
    }

    [Fact]
    public void Isdir_Returns_False_For_File()
    {
        var path = CreateTempFile();
        OsPath.Isdir(path).Should().BeFalse();
    }

    [Fact]
    public void Isabs_Returns_True_For_Absolute()
    {
        OsPath.Isabs("/usr/local").Should().BeTrue();
    }

    [Fact]
    public void Isabs_Returns_False_For_Relative()
    {
        OsPath.Isabs("a/b/c").Should().BeFalse();
    }

    [Fact]
    public void Basename_Returns_Filename()
    {
        OsPath.Basename("/a/b/c.txt").Should().Be("c.txt");
    }

    [Fact]
    public void Dirname_Returns_Directory()
    {
        OsPath.Dirname("/a/b/c.txt").Should().Be("/a/b");
    }

    [Fact]
    public void Split_Returns_Head_And_Tail()
    {
        var (head, tail) = OsPath.Split("/a/b/c.txt");
        head.Should().Be("/a/b");
        tail.Should().Be("c.txt");
    }

    [Fact]
    public void Splitext_Returns_Root_And_Ext()
    {
        var (root, ext) = OsPath.Splitext("/a/b/c.tar.gz");
        root.Should().Be("/a/b/c.tar");
        ext.Should().Be(".gz");
    }

    [Fact]
    public void Splitext_No_Extension()
    {
        var (root, ext) = OsPath.Splitext("/a/b/c");
        root.Should().Be("/a/b/c");
        ext.Should().BeEmpty();
    }

    [Fact]
    public void Abspath_Returns_Full_Path()
    {
        var result = OsPath.Abspath(".");
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void Normpath_Collapses_DotDot()
    {
        OsPath.Normpath("a/b/../c").Should().Be("a" + System.IO.Path.DirectorySeparatorChar + "c");
    }

    [Fact]
    public void Normpath_Removes_Dots()
    {
        OsPath.Normpath("a/./b").Should().Be("a" + System.IO.Path.DirectorySeparatorChar + "b");
    }

    [Fact]
    public void Normpath_Empty_Returns_Dot()
    {
        OsPath.Normpath("").Should().Be(".");
    }

    [Fact]
    public void Getsize_Returns_File_Size()
    {
        var path = CreateTempFile("hello");
        OsPath.Getsize(path).Should().Be(5);
    }

    [Fact]
    public void Getsize_Nonexistent_Throws()
    {
        var act = () => OsPath.Getsize("/tmp/nonexistent_" + Guid.NewGuid());
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Expanduser_Expands_Tilde()
    {
        var result = OsPath.Expanduser("~");
        result.Should().NotBe("~");
        System.IO.Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void Expanduser_Expands_Tilde_Slash()
    {
        var result = OsPath.Expanduser("~/foo");
        result.Should().EndWith("foo");
        result.Should().NotStartWith("~");
    }

    [Fact]
    public void Expanduser_No_Tilde_Unchanged()
    {
        OsPath.Expanduser("/a/b").Should().Be("/a/b");
    }
}
