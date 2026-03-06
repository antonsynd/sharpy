using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class TextFileTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    private string CreateTempFile(string content = "")
    {
        var path = System.IO.Path.GetTempFileName();
        _tempFiles.Add(path);
        if (content.Length > 0)
            File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private string GetTempPath()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_test_" + Guid.NewGuid().ToString("N") + ".txt");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            { File.Delete(path); }
            catch { }
        }
    }

    // ===== Read mode tests =====

    [Fact]
    public void Read_ReturnsEntireFileContent()
    {
        var path = CreateTempFile("hello world");
        using var f = Open(path, "r");
        f.Read().Should().Be("hello world");
    }

    [Fact]
    public void Read_EmptyFile_ReturnsEmptyString()
    {
        var path = CreateTempFile("");
        using var f = Open(path, "r");
        f.Read().Should().Be("");
    }

    [Fact]
    public void Read_WithSize_ReadsExactCharacters()
    {
        var path = CreateTempFile("hello world");
        using var f = Open(path, "r");
        f.Read(5).Should().Be("hello");
        f.Read(1).Should().Be(" ");
        f.Read(5).Should().Be("world");
    }

    [Fact]
    public void Read_WithSize_AtEof_ReturnsRemaining()
    {
        var path = CreateTempFile("hi");
        using var f = Open(path, "r");
        f.Read(100).Should().Be("hi");
    }

    [Fact]
    public void Read_WithNegativeSize_ReadsAll()
    {
        var path = CreateTempFile("hello");
        using var f = Open(path, "r");
        f.Read(-1).Should().Be("hello");
    }

    [Fact]
    public void Readline_ReturnsLineWithNewline()
    {
        var path = CreateTempFile("line1\nline2\nline3");
        using var f = Open(path, "r");
        f.Readline().Should().Be("line1\n");
        f.Readline().Should().Be("line2\n");
        f.Readline().Should().Be("line3");
    }

    [Fact]
    public void Readline_AtEof_ReturnsEmptyString()
    {
        var path = CreateTempFile("one");
        using var f = Open(path, "r");
        f.Readline().Should().Be("one");
        f.Readline().Should().Be("");
    }

    [Fact]
    public void Readline_EmptyFile_ReturnsEmptyString()
    {
        var path = CreateTempFile("");
        using var f = Open(path, "r");
        f.Readline().Should().Be("");
    }

    [Fact]
    public void Readlines_ReturnsAllLinesWithNewlines()
    {
        var path = CreateTempFile("line1\nline2\nline3");
        using var f = Open(path, "r");
        var lines = f.Readlines();
        lines.Should().HaveCount(3);
        lines[0].Should().Be("line1\n");
        lines[1].Should().Be("line2\n");
        lines[2].Should().Be("line3");
    }

    [Fact]
    public void Readlines_EmptyFile_ReturnsEmptyList()
    {
        var path = CreateTempFile("");
        using var f = Open(path, "r");
        f.Readlines().Should().BeEmpty();
    }

    [Fact]
    public void Readlines_TrailingNewline()
    {
        var path = CreateTempFile("a\nb\n");
        using var f = Open(path, "r");
        var lines = f.Readlines();
        lines.Should().HaveCount(2);
        lines[0].Should().Be("a\n");
        lines[1].Should().Be("b\n");
    }

    // ===== Write mode tests =====

    [Fact]
    public void Write_CreatesFileWithContent()
    {
        var path = GetTempPath();
        using (var f = Open(path, "w"))
        {
            f.Write("hello");
        }
        File.ReadAllText(path).Should().Be("hello");
    }

    [Fact]
    public void Write_ReturnsCharacterCount()
    {
        var path = GetTempPath();
        using var f = Open(path, "w");
        f.Write("hello").Should().Be(5);
        f.Write("").Should().Be(0);
    }

    [Fact]
    public void Write_TruncatesExistingFile()
    {
        var path = CreateTempFile("old content");
        using (var f = Open(path, "w"))
        {
            f.Write("new");
        }
        File.ReadAllText(path).Should().Be("new");
    }

    [Fact]
    public void Writelines_WritesMultipleStrings()
    {
        var path = GetTempPath();
        using (var f = Open(path, "w"))
        {
            f.Writelines(new[] { "hello", " ", "world" });
        }
        File.ReadAllText(path).Should().Be("hello world");
    }

    [Fact]
    public void Flush_ForcesWriteToDisk()
    {
        var path = GetTempPath();
        using var f = Open(path, "w");
        f.Write("flushed");
        f.Flush();
        // File should have content after flush even before close
        File.ReadAllText(path).Should().Be("flushed");
    }

    // ===== Append mode tests =====

    [Fact]
    public void Append_AddsToExistingFile()
    {
        var path = CreateTempFile("first");
        using (var f = Open(path, "a"))
        {
            f.Write(" second");
        }
        File.ReadAllText(path).Should().Be("first second");
    }

    [Fact]
    public void Append_CreatesFileIfNotExists()
    {
        var path = GetTempPath();
        using (var f = Open(path, "a"))
        {
            f.Write("new");
        }
        File.ReadAllText(path).Should().Be("new");
    }

    // ===== Exclusive create mode tests =====

    [Fact]
    public void ExclusiveCreate_CreatesNewFile()
    {
        var path = GetTempPath();
        using (var f = Open(path, "x"))
        {
            f.Write("exclusive");
        }
        File.ReadAllText(path).Should().Be("exclusive");
    }

    [Fact]
    public void ExclusiveCreate_ThrowsIfFileExists()
    {
        var path = CreateTempFile("existing");
        Action act = () => Open(path, "x");
        act.Should().Throw<FileExistsError>();
    }

    // ===== IDisposable / context manager tests =====

    [Fact]
    public void Using_ClosesFileAutomatically()
    {
        var path = CreateTempFile("test");
        TextFile f;
        using (f = Open(path, "r"))
        {
            f.Closed.Should().BeFalse();
        }
        f.Closed.Should().BeTrue();
    }

    [Fact]
    public void Close_SetsClosedProperty()
    {
        var path = CreateTempFile("test");
        var f = Open(path, "r");
        f.Closed.Should().BeFalse();
        f.Close();
        f.Closed.Should().BeTrue();
    }

    [Fact]
    public void Close_IsIdempotent()
    {
        var path = CreateTempFile("test");
        var f = Open(path, "r");
        f.Close();
        f.Close(); // should not throw
        f.Closed.Should().BeTrue();
    }

    // ===== Properties tests =====

    [Fact]
    public void Name_ReturnsFilePath()
    {
        var path = CreateTempFile("test");
        using var f = Open(path, "r");
        f.Name.Should().Be(path);
    }

    [Fact]
    public void Mode_ReturnsModeString()
    {
        var path = CreateTempFile("test");
        using var f = Open(path, "r");
        f.Mode.Should().Be("r");
    }

    // ===== Error cases =====

    [Fact]
    public void Read_FileNotFound_ThrowsFileNotFoundError()
    {
        Action act = () => Open("/nonexistent/path/file.txt", "r");
        act.Should().Throw<FileNotFoundError>();
    }

    [Fact]
    public void Read_FromWriteMode_ThrowsValueError()
    {
        var path = GetTempPath();
        using var f = Open(path, "w");
        Action act = () => f.Read();
        act.Should().Throw<ValueError>().WithMessage("not readable");
    }

    [Fact]
    public void Write_ToReadMode_ThrowsValueError()
    {
        var path = CreateTempFile("test");
        using var f = Open(path, "r");
        Action act = () => f.Write("nope");
        act.Should().Throw<ValueError>().WithMessage("not writable");
    }

    [Fact]
    public void Read_OnClosedFile_ThrowsValueError()
    {
        var path = CreateTempFile("test");
        var f = Open(path, "r");
        f.Close();
        Action act = () => f.Read();
        act.Should().Throw<ValueError>().WithMessage("I/O operation on closed file.");
    }

    [Fact]
    public void Write_OnClosedFile_ThrowsValueError()
    {
        var path = GetTempPath();
        var f = Open(path, "w");
        f.Close();
        Action act = () => f.Write("nope");
        act.Should().Throw<ValueError>().WithMessage("I/O operation on closed file.");
    }

    [Fact]
    public void InvalidMode_ThrowsValueError()
    {
        Action act = () => Open("/tmp/test.txt", "z");
        act.Should().Throw<ValueError>().WithMessage("*invalid mode*");
    }

    [Fact]
    public void Readline_OnClosedFile_ThrowsValueError()
    {
        var path = CreateTempFile("test");
        var f = Open(path, "r");
        f.Close();
        Action act = () => f.Readline();
        act.Should().Throw<ValueError>().WithMessage("I/O operation on closed file.");
    }

    [Fact]
    public void Flush_OnReadMode_ThrowsValueError()
    {
        var path = CreateTempFile("test");
        using var f = Open(path, "r");
        Action act = () => f.Flush();
        act.Should().Throw<ValueError>().WithMessage("not writable");
    }

    // ===== Encoding tests =====

    [Fact]
    public void Utf8_Encoding_HandlesUnicode()
    {
        var path = GetTempPath();
        using (var f = Open(path, "w", "utf-8"))
        {
            f.Write("caf\u00e9 \u2603");
        }
        using (var f = Open(path, "r", "utf-8"))
        {
            f.Read().Should().Be("caf\u00e9 \u2603");
        }
    }

    [Fact]
    public void Ascii_Encoding_Works()
    {
        var path = GetTempPath();
        using (var f = Open(path, "w", "ascii"))
        {
            f.Write("hello");
        }
        using (var f = Open(path, "r", "ascii"))
        {
            f.Read().Should().Be("hello");
        }
    }

    [Fact]
    public void UnknownEncoding_ThrowsValueError()
    {
        Action act = () => Open("/tmp/test.txt", "r", "bogus-encoding-xyz");
        act.Should().Throw<ValueError>().WithMessage("*unknown encoding*");
    }

    // ===== Seek and Tell tests =====

    [Fact]
    public void Seek_ResetsReadPosition()
    {
        var path = CreateTempFile("hello");
        using var f = Open(path, "r");
        f.Read(3).Should().Be("hel");
        f.Seek(0);
        f.Read(3).Should().Be("hel");
    }

    [Fact]
    public void Tell_ReportsPosition()
    {
        var path = CreateTempFile("hello");
        using var f = Open(path, "r");
        f.Tell().Should().Be(0);
        f.Read(3);
        f.Tell().Should().BeGreaterThan(0);
    }

    // ===== IsADirectoryError tests =====

    [Fact]
    public void Open_Directory_ThrowsIsADirectoryError()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_test_dir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Action act = () => Open(dir, "r");
            act.Should().Throw<IsADirectoryError>();
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    // ===== Writelines edge cases =====

    [Fact]
    public void Writelines_WithNewlines_DoesNotAddExtra()
    {
        var path = GetTempPath();
        using (var f = Open(path, "w"))
        {
            f.Writelines(new[] { "a\n", "b\n", "c" });
        }
        File.ReadAllText(path).Should().Be("a\nb\nc");
    }

    // ===== File with no trailing newline =====

    [Fact]
    public void Readlines_NoTrailingNewline_LastLineHasNoNewline()
    {
        var path = CreateTempFile("a\nb");
        using var f = Open(path, "r");
        var lines = f.Readlines();
        lines[0].Should().Be("a\n");
        lines[1].Should().Be("b");
    }
}
