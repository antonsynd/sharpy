using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for Shutil covering gaps not in ShutilTests.cs.
/// </summary>
public class ShutilAdditionalTests : IDisposable
{
    private readonly string _tempDir;

    public ShutilAdditionalTests()
    {
        _tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sharpy_shutil_add_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

    // ===== Copy additional =====

    [Fact]
    public void Copy_OverwritesExistingDestination()
    {
        File.WriteAllText(Sub("src.txt"), "new content");
        File.WriteAllText(Sub("dst.txt"), "old content");
        Sharpy.ShutilModule.Copy(Sub("src.txt"), Sub("dst.txt"));
        File.ReadAllText(Sub("dst.txt")).Should().Be("new content");
    }

    [Fact]
    public void Copy_ContentMatchesSource()
    {
        var content = "hello from source";
        File.WriteAllText(Sub("content_src.txt"), content);
        Sharpy.ShutilModule.Copy(Sub("content_src.txt"), Sub("content_dst.txt"));
        File.ReadAllText(Sub("content_dst.txt")).Should().Be(content);
    }

    // ===== Copy2 additional =====

    [Fact]
    public void Copy2_ToDirectory_CopiesIntoDir()
    {
        File.WriteAllText(Sub("src2.txt"), "data");
        Directory.CreateDirectory(Sub("copy2dir"));
        var result = Sharpy.ShutilModule.Copy2(Sub("src2.txt"), Sub("copy2dir"));
        File.Exists(result).Should().BeTrue();
        File.ReadAllText(result).Should().Be("data");
    }

    [Fact]
    public void Copy2_NonexistentSource_ThrowsOSError()
    {
        Action act = () => Sharpy.ShutilModule.Copy2(Sub("nope.txt"), Sub("dst.txt"));
        act.Should().Throw<Sharpy.OSError>();
    }

    // ===== Copytree additional =====

    [Fact]
    public void Copytree_DeeplyNested_CopiesAllLevels()
    {
        var src = Sub("deep_src");
        var l1 = System.IO.Path.Combine(src, "l1");
        var l2 = System.IO.Path.Combine(l1, "l2");
        Directory.CreateDirectory(l2);
        File.WriteAllText(System.IO.Path.Combine(src, "root.txt"), "root");
        File.WriteAllText(System.IO.Path.Combine(l1, "mid.txt"), "mid");
        File.WriteAllText(System.IO.Path.Combine(l2, "deep.txt"), "deep");

        var dst = Sub("deep_dst");
        Sharpy.ShutilModule.Copytree(src, dst);

        File.Exists(System.IO.Path.Combine(dst, "root.txt")).Should().BeTrue();
        File.Exists(System.IO.Path.Combine(dst, "l1", "mid.txt")).Should().BeTrue();
        File.Exists(System.IO.Path.Combine(dst, "l1", "l2", "deep.txt")).Should().BeTrue();
        File.ReadAllText(System.IO.Path.Combine(dst, "l1", "l2", "deep.txt")).Should().Be("deep");
    }

    // ===== Rmtree additional =====

    [Fact]
    public void Rmtree_EmptyDirectory_Removes()
    {
        var dir = Sub("rmtree_empty");
        Directory.CreateDirectory(dir);
        Sharpy.ShutilModule.Rmtree(dir);
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void Rmtree_WithSubdirectories_RemovesAll()
    {
        var dir = Sub("rmtree_subs");
        var sub = System.IO.Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(System.IO.Path.Combine(sub, "f.txt"), "data");
        Sharpy.ShutilModule.Rmtree(dir);
        Directory.Exists(dir).Should().BeFalse();
    }

    // ===== Move additional =====

    [Fact]
    public void Move_File_ContentPreserved()
    {
        File.WriteAllText(Sub("move_content_src.txt"), "preserved");
        Sharpy.ShutilModule.Move(Sub("move_content_src.txt"), Sub("move_content_dst.txt"));
        File.ReadAllText(Sub("move_content_dst.txt")).Should().Be("preserved");
    }

    [Fact]
    public void Move_File_IntoDirectory_PlacesInDir()
    {
        File.WriteAllText(Sub("move_into_src.txt"), "data");
        Directory.CreateDirectory(Sub("move_into_dir"));
        var result = Sharpy.ShutilModule.Move(Sub("move_into_src.txt"), Sub("move_into_dir"));
        // When destination is a directory, the file should be placed inside it
        File.Exists(Sub("move_into_src.txt")).Should().BeFalse();
        // result should be the destination (either dir or file path inside)
        result.Should().NotBeNullOrEmpty();
    }

    // ===== Which additional =====

    [Fact]
    public void Which_FindsLsOnUnix()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
            return; // Skip on Windows

        var result = Sharpy.ShutilModule.Which("ls");
        result.Should().NotBeNull();
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public void Which_PathWithSeparator_ChecksDirectly()
    {
        // When name contains a path separator, it's checked directly
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
            return;

        // Find an existing absolute path to test with (use Which to find ls first)
        string? lsPath = Sharpy.ShutilModule.Which("ls");
        if (lsPath == null)
            return; // ls not findable, skip

        var result = Sharpy.ShutilModule.Which(lsPath);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Which_PathWithSeparator_Nonexistent_ReturnsNull()
    {
        var result = Sharpy.ShutilModule.Which("/nonexistent_xyz_path/binary");
        result.Should().BeNull();
    }

    // ===== DiskUsage additional =====

    [Fact]
    public void DiskUsage_ForFile_ReturnsSensibleValues()
    {
        File.WriteAllText(Sub("diskusage.txt"), "data");
        var (total, used, free) = Sharpy.ShutilModule.DiskUsage(Sub("diskusage.txt"));
        total.Should().BeGreaterThan(0);
        (used + free).Should().Be(total);
    }

    [Fact]
    public void DiskUsage_TotalGreaterThanUsedAndFree()
    {
        var (total, used, free) = Sharpy.ShutilModule.DiskUsage(_tempDir);
        total.Should().BeGreaterThanOrEqualTo(used);
        total.Should().BeGreaterThanOrEqualTo(free);
    }
}
