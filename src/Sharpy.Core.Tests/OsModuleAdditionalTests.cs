using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for Os module covering gaps not in OsModuleTests.cs.
/// </summary>
public class OsModuleAdditionalTests : IDisposable
{
    private readonly string _tempDir;

    public OsModuleAdditionalTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sharpy_os_add_tests_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

    // ===== Constants =====

    [Fact]
    public void Altsep_IsStringValue()
    {
        // Altsep is always a string (empty string or a separator character)
        Os.Altsep.Should().NotBeNull();
    }

    [Fact]
    public void Pathsep_IsNotEmpty()
    {
        Os.Pathsep.Should().NotBeNullOrEmpty();
    }

    // ===== File Operations =====

    [Fact]
    public void Remove_OnDirectoryPath_ThrowsFileNotFoundError()
    {
        // On .NET, File.Exists returns false for directories,
        // so Os.Remove always throws FileNotFoundError for directory paths.
        var dirPath = Sub("adirtoremove");
        System.IO.Directory.CreateDirectory(dirPath);
        Action act = () => Os.Remove(dirPath);
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Rename_RenamesDirectory()
    {
        var srcDir = Sub("rename_src_dir");
        var dstDir = Sub("rename_dst_dir");
        System.IO.Directory.CreateDirectory(srcDir);
        Os.Rename(srcDir, dstDir);
        System.IO.Directory.Exists(srcDir).Should().BeFalse();
        System.IO.Directory.Exists(dstDir).Should().BeTrue();
    }

    // ===== Directory Operations =====

    [Fact]
    public void Mkdir_ThrowsWhenParentMissing()
    {
        var path = Sub("missing_parent/child");
        Action act = () => Os.Mkdir(path);
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Listdir_EmptyDirectory_ReturnsEmptyList()
    {
        var emptyDir = Sub("empty_listdir_dir");
        System.IO.Directory.CreateDirectory(emptyDir);
        var entries = Os.Listdir(emptyDir);
        ((ICollection<string>)entries).Count.Should().Be(0);
    }

    [Fact]
    public void Listdir_CurrentDir_ReturnsEntries()
    {
        // Listdir with "." is valid (uses current directory)
        var entries = Os.Listdir(".");
        entries.Should().NotBeNull();
    }

    // ===== Walk content verification =====

    [Fact]
    public void Walk_YieldsCorrectFilenames()
    {
        var root = Sub("walk_files");
        System.IO.Directory.CreateDirectory(root);
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "alpha.txt"), "");
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "beta.txt"), "");

        bool found = false;
        foreach (var (dirpath, dirnames, filenames) in Os.Walk(root))
        {
            if (dirpath == root)
            {
                found = true;
                // filenames list should contain alpha.txt and beta.txt
                filenames.Should().Contain("alpha.txt");
                filenames.Should().Contain("beta.txt");
            }
        }
        found.Should().BeTrue("Walk should yield the root directory entry");
    }

    [Fact]
    public void Walk_YieldsCorrectDirnames()
    {
        var root = Sub("walk_dirs");
        System.IO.Directory.CreateDirectory(root);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "subA"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "subB"));

        bool found = false;
        foreach (var (dirpath, dirnames, filenames) in Os.Walk(root))
        {
            if (dirpath == root)
            {
                found = true;
                dirnames.Should().Contain("subA");
                dirnames.Should().Contain("subB");
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Walk_DeepTree_VisitsAllLevels()
    {
        var root = Sub("walk_deep");
        var level1 = System.IO.Path.Combine(root, "level1");
        var level2 = System.IO.Path.Combine(level1, "level2");
        System.IO.Directory.CreateDirectory(level2);
        System.IO.File.WriteAllText(System.IO.Path.Combine(level2, "deep.txt"), "");

        var visitedPaths = new List<string>();
        foreach (var (dirpath, _, _) in Os.Walk(root))
        {
            visitedPaths.Add(dirpath);
        }
        visitedPaths.Should().Contain(root);
        visitedPaths.Should().Contain(level1);
        visitedPaths.Should().Contain(level2);
    }

    // ===== Environment Variables =====

    [Fact]
    public void Environ_ContainsPathVariable()
    {
        var env = Os.Environ;
        // PATH is present on posix, Path or PATH on Windows
        bool hasPath = env.ContainsKey("PATH") || env.ContainsKey("Path");
        hasPath.Should().BeTrue("PATH should be set in environment");
    }

    [Fact]
    public void Getenv_ReturnsExistingVariable()
    {
        // PATH always exists on Unix/Windows
        string? pathVal = Os.Getenv(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows) ? "Path" : "PATH");
        pathVal.Should().NotBeNullOrEmpty();
    }

    // ===== Stat additional coverage =====

    [Fact]
    public void Stat_ModeField_IsNonNegative()
    {
        var path = Sub("stat_mode.txt");
        System.IO.File.WriteAllText(path, "mode test");
        var result = Os.Stat(path);
        result.StMode.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Stat_EmptyFile_HasZeroSize()
    {
        var path = Sub("empty_stat.txt");
        System.IO.File.WriteAllText(path, "");
        var result = Os.Stat(path);
        result.StSize.Should().Be(0);
    }

    // ===== Getcwd =====

    [Fact]
    public void Getcwd_IsAbsolutePath()
    {
        var cwd = Os.Getcwd();
        System.IO.Path.IsPathRooted(cwd).Should().BeTrue();
    }

    // ===== Makedirs edge cases =====

    [Fact]
    public void Makedirs_DeepNesting_CreatesAll()
    {
        var deep = System.IO.Path.Combine(_tempDir, "x", "y", "z", "w");
        Os.Makedirs(deep);
        System.IO.Directory.Exists(deep).Should().BeTrue();
    }
}
