using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Tempfile tests not covered by TempfileTests.cs (11 tests).
/// </summary>
public class TempfileCompleteTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _createdPaths = new System.Collections.Generic.List<string>();

    public void Dispose()
    {
        foreach (string path in _createdPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    [Fact]
    public void Gettempdir_PathExists()
    {
        string dir = Sharpy.TempfileModule.Gettempdir();

        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void Mkdtemp_PathIsInsideTempDir()
    {
        string tempDir = Sharpy.TempfileModule.Gettempdir();
        string dir = Sharpy.TempfileModule.Mkdtemp();
        _createdPaths.Add(dir);

        dir.Should().StartWith(tempDir);
    }

    [Fact]
    public void Mkdtemp_WithSuffix_PrefixAppearsInName()
    {
        // Mkdtemp only accepts prefix, but let's verify prefix is in the name
        string dir = Sharpy.TempfileModule.Mkdtemp("sharpy_test_");
        _createdPaths.Add(dir);

        System.IO.Path.GetFileName(dir).Should().StartWith("sharpy_test_");
    }

    [Fact]
    public void Mkdtemp_CreatedDirectory_IsDeletable()
    {
        string dir = Sharpy.TempfileModule.Mkdtemp();

        // We can delete it
        Directory.Exists(dir).Should().BeTrue();
        Directory.Delete(dir);
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void Mkdtemp_CreatedDirectory_IsEmpty()
    {
        string dir = Sharpy.TempfileModule.Mkdtemp();
        _createdPaths.Add(dir);

        // A freshly created temp dir should be empty
        Directory.GetFileSystemEntries(dir).Should().BeEmpty();
    }

    [Fact]
    public void Mkstemp_PathIsInsideTempDir()
    {
        string tempDir = Sharpy.TempfileModule.Gettempdir();
        var (_, path) = Sharpy.TempfileModule.Mkstemp();
        _createdPaths.Add(path);

        path.Should().StartWith(tempDir);
    }

    [Fact]
    public void Mkstemp_DefaultNoSuffix()
    {
        var (_, path) = Sharpy.TempfileModule.Mkstemp();
        _createdPaths.Add(path);

        // Default suffix is empty — file has no extension
        System.IO.Path.GetExtension(path).Should().Be("");
    }

    [Fact]
    public void Mkstemp_WithSuffix_SuffixAppearsAtEnd()
    {
        var (_, path) = Sharpy.TempfileModule.Mkstemp(suffix: ".txt");
        _createdPaths.Add(path);

        System.IO.Path.GetFileName(path).Should().EndWith(".txt");
    }

    [Fact]
    public void Mkstemp_CreatedFile_IsWritable()
    {
        var (_, path) = Sharpy.TempfileModule.Mkstemp();
        _createdPaths.Add(path);

        // Should be able to write to the file
        File.WriteAllText(path, "test content");
        File.ReadAllText(path).Should().Be("test content");
    }

    [Fact]
    public void Mkstemp_CreatedFile_IsDeletable()
    {
        var (_, path) = Sharpy.TempfileModule.Mkstemp();

        File.Exists(path).Should().BeTrue();
        File.Delete(path);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Mkstemp_ReturnsZeroAsFd()
    {
        // .NET doesn't use POSIX file descriptors; fd is always 0
        var (fd, path) = Sharpy.TempfileModule.Mkstemp();
        _createdPaths.Add(path);

        fd.Should().Be(0);
    }
}
