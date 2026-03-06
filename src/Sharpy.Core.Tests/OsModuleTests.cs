using System;
using System.IO;
using FluentAssertions;
using Sharpy;
using Xunit;

namespace Sharpy.Core.Tests;

public class OsModuleTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _tempDirs = new();
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    private string CreateTempDir()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_os_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

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
        foreach (var d in _tempDirs)
        {
            try { Directory.Delete(d, true); } catch { }
        }
    }

    // ===== Properties =====

    [Fact]
    public void Name_Returns_Posix_Or_Nt()
    {
        var name = Os.Name;
        name.Should().BeOneOf("posix", "nt");
    }

    [Fact]
    public void Sep_Returns_Directory_Separator()
    {
        Os.Sep.Should().Be(System.IO.Path.DirectorySeparatorChar.ToString());
    }

    [Fact]
    public void Linesep_Returns_Newline()
    {
        Os.Linesep.Should().Be(Environment.NewLine);
    }

    [Fact]
    public void Environ_Returns_Dict_With_Entries()
    {
        var env = Os.Environ;
        env.Should().NotBeNull();
        // PATH is almost always set
        env.Count.Should().BeGreaterThan(0);
    }

    // ===== File operations =====

    [Fact]
    public void Remove_Deletes_File()
    {
        var path = CreateTempFile();
        File.Exists(path).Should().BeTrue();
        Os.Remove(path);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Remove_Nonexistent_Throws_FileNotFoundError()
    {
        var act = () => Os.Remove("/tmp/nonexistent_sharpy_" + Guid.NewGuid());
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Remove_Directory_Throws_IsADirectoryError()
    {
        var dir = CreateTempDir();
        var act = () => Os.Remove(dir);
        act.Should().Throw<IsADirectoryError>();
    }

    [Fact]
    public void Rename_Moves_File()
    {
        var src = CreateTempFile("content");
        var dst = src + "_renamed";
        _tempFiles.Add(dst);
        Os.Rename(src, dst);
        File.Exists(src).Should().BeFalse();
        File.Exists(dst).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("content");
    }

    [Fact]
    public void Rename_Nonexistent_Throws()
    {
        var act = () => Os.Rename("/tmp/nonexistent_" + Guid.NewGuid(), "/tmp/dest");
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Mkdir_Creates_Directory()
    {
        var parent = CreateTempDir();
        var child = System.IO.Path.Combine(parent, "subdir");
        Os.Mkdir(child);
        Directory.Exists(child).Should().BeTrue();
    }

    [Fact]
    public void Mkdir_No_Parent_Throws_FileNotFoundError()
    {
        var path = System.IO.Path.Combine("/tmp/nonexistent_" + Guid.NewGuid(), "child");
        var act = () => Os.Mkdir(path);
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Mkdir_Already_Exists_Throws_FileExistsError()
    {
        var dir = CreateTempDir();
        var act = () => Os.Mkdir(dir);
        act.Should().Throw<FileExistsError>();
    }

    [Fact]
    public void Makedirs_Creates_Nested()
    {
        var root = CreateTempDir();
        var nested = System.IO.Path.Combine(root, "a", "b", "c");
        Os.Makedirs(nested);
        Directory.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Makedirs_ExistOk_True_No_Error()
    {
        var dir = CreateTempDir();
        var act = () => Os.Makedirs(dir, exist_ok: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Makedirs_ExistOk_False_Throws()
    {
        var dir = CreateTempDir();
        var act = () => Os.Makedirs(dir, exist_ok: false);
        act.Should().Throw<FileExistsError>();
    }

    [Fact]
    public void Rmdir_Removes_Empty_Directory()
    {
        var dir = CreateTempDir();
        Os.Rmdir(dir);
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void Rmdir_NonEmpty_Throws()
    {
        var dir = CreateTempDir();
        File.WriteAllText(System.IO.Path.Combine(dir, "file.txt"), "x");
        var act = () => Os.Rmdir(dir);
        act.Should().Throw<OSError>();
    }

    [Fact]
    public void Rmdir_Nonexistent_Throws()
    {
        var act = () => Os.Rmdir("/tmp/nonexistent_" + Guid.NewGuid());
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Listdir_Returns_Entries()
    {
        var dir = CreateTempDir();
        File.WriteAllText(System.IO.Path.Combine(dir, "a.txt"), "");
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "subdir"));

        var entries = Os.Listdir(dir);
        entries.Should().Contain("a.txt");
        entries.Should().Contain("subdir");
    }

    [Fact]
    public void Listdir_Nonexistent_Throws()
    {
        var act = () => Os.Listdir("/tmp/nonexistent_" + Guid.NewGuid());
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Getcwd_Returns_Current_Directory()
    {
        Os.Getcwd().Should().NotBeNullOrEmpty();
    }

    // ===== Environment =====

    [Fact]
    public void Getenv_Returns_Null_For_Missing()
    {
        Os.Getenv("SHARPY_NONEXISTENT_VAR_12345").Should().BeNull();
    }

    [Fact]
    public void Getenv_With_Default_Returns_Default_For_Missing()
    {
        Os.Getenv("SHARPY_NONEXISTENT_VAR_12345", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void Putenv_Sets_Variable()
    {
        Os.Putenv("SHARPY_TEST_VAR", "test_value");
        Os.Getenv("SHARPY_TEST_VAR").Should().Be("test_value");
        // Clean up
        Environment.SetEnvironmentVariable("SHARPY_TEST_VAR", null);
    }

    // ===== Walk =====

    [Fact]
    public void Walk_Traverses_Directory_Tree()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(System.IO.Path.Combine(root, "sub1"));
        Directory.CreateDirectory(System.IO.Path.Combine(root, "sub2"));
        File.WriteAllText(System.IO.Path.Combine(root, "root.txt"), "");
        File.WriteAllText(System.IO.Path.Combine(root, "sub1", "a.txt"), "");

        var entries = new System.Collections.Generic.List<(string, Sharpy.List<string>, Sharpy.List<string>)>();
        foreach (var entry in Os.Walk(root))
        {
            entries.Add(entry);
        }

        entries.Should().HaveCountGreaterOrEqualTo(3);
        entries[0].dirpath.Should().Be(root);
        entries[0].dirnames.Should().Contain("sub1");
        entries[0].filenames.Should().Contain("root.txt");
    }

    [Fact]
    public void Walk_Nonexistent_Yields_Nothing()
    {
        var entries = new System.Collections.Generic.List<(string, Sharpy.List<string>, Sharpy.List<string>)>();
        foreach (var entry in Os.Walk("/tmp/nonexistent_" + Guid.NewGuid()))
        {
            entries.Add(entry);
        }
        entries.Should().BeEmpty();
    }

    // ===== Stat =====

    [Fact]
    public void Stat_Returns_File_Info()
    {
        var path = CreateTempFile("hello world");
        var result = Os.Stat(path);
        result.StSize.Should().BeGreaterThan(0);
        result.StMtime.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Stat_Directory_Returns_Size_Zero()
    {
        var dir = CreateTempDir();
        var result = Os.Stat(dir);
        result.StSize.Should().Be(0);
    }

    [Fact]
    public void Stat_Nonexistent_Throws()
    {
        var act = () => Os.Stat("/tmp/nonexistent_" + Guid.NewGuid());
        act.Should().Throw<FileNotFoundError>();
    }
}
