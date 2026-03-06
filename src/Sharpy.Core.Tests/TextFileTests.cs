using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class TextFileTests : IDisposable
    {
        private readonly string _tempDir;

        public TextFileTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_textfile_tests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        private string TempFile(string name = "test.txt")
        {
            return System.IO.Path.Combine(_tempDir, name);
        }

        private void WriteTestFile(string name, string content)
        {
            System.IO.File.WriteAllText(TempFile(name), content, new UTF8Encoding(false));
        }

        // ===== Read Mode Tests =====

        [Fact]
        public void Read_ReturnsEntireFileContent()
        {
            WriteTestFile("read.txt", "hello world");
            using var f = Builtins.Open(TempFile("read.txt"));
            Assert.Equal("hello world", f.Read());
        }

        [Fact]
        public void Read_EmptyFile_ReturnsEmptyString()
        {
            WriteTestFile("empty.txt", "");
            using var f = Builtins.Open(TempFile("empty.txt"));
            Assert.Equal("", f.Read());
        }

        [Fact]
        public void Read_WithSize_ReadsExactCharacters()
        {
            WriteTestFile("sized.txt", "hello world");
            using var f = Builtins.Open(TempFile("sized.txt"));
            Assert.Equal("hello", f.Read(5));
            Assert.Equal(" world", f.Read(100)); // reads remainder
        }

        [Fact]
        public void Read_WithNegativeSize_ReadsAll()
        {
            WriteTestFile("neg.txt", "hello");
            using var f = Builtins.Open(TempFile("neg.txt"));
            Assert.Equal("hello", f.Read(-1));
        }

        [Fact]
        public void Readline_IncludesTrailingNewline()
        {
            WriteTestFile("lines.txt", "line1\nline2\nline3");
            using var f = Builtins.Open(TempFile("lines.txt"));
            Assert.Equal("line1\n", f.Readline());
            Assert.Equal("line2\n", f.Readline());
            Assert.Equal("line3", f.Readline());
            Assert.Equal("", f.Readline()); // EOF
        }

        [Fact]
        public void Readlines_ReturnsAllLinesWithNewlines()
        {
            WriteTestFile("readlines.txt", "hello\nworld");
            using var f = Builtins.Open(TempFile("readlines.txt"));
            var lines = f.Readlines();
            Assert.Equal(2, ((ICollection<string>)lines).Count);
            Assert.Equal("hello\n", lines[0]);
            Assert.Equal("world", lines[1]);
        }

        [Fact]
        public void Readlines_EmptyFile_ReturnsEmptyList()
        {
            WriteTestFile("empty2.txt", "");
            using var f = Builtins.Open(TempFile("empty2.txt"));
            var lines = f.Readlines();
            Assert.Empty(lines);
        }

        // ===== Write Mode Tests =====

        [Fact]
        public void Write_CreatesFileAndWritesContent()
        {
            var path = TempFile("write.txt");
            using (var f = Builtins.Open(path, "w"))
            {
                f.Write("hello world");
            }
            Assert.Equal("hello world", System.IO.File.ReadAllText(path));
        }

        [Fact]
        public void Write_ReturnsCharacterCount()
        {
            var path = TempFile("count.txt");
            using var f = Builtins.Open(path, "w");
            Assert.Equal(11, f.Write("hello world"));
        }

        [Fact]
        public void Write_TruncatesExistingFile()
        {
            var path = TempFile("trunc.txt");
            System.IO.File.WriteAllText(path, "old content that is longer");
            using (var f = Builtins.Open(path, "w"))
            {
                f.Write("new");
            }
            Assert.Equal("new", System.IO.File.ReadAllText(path));
        }

        [Fact]
        public void Writelines_WritesMultipleStrings()
        {
            var path = TempFile("writelines.txt");
            using (var f = Builtins.Open(path, "w"))
            {
                f.Writelines(new[] { "line1\n", "line2\n", "line3" });
            }
            Assert.Equal("line1\nline2\nline3", System.IO.File.ReadAllText(path));
        }

        // ===== Append Mode Tests =====

        [Fact]
        public void AppendMode_AddsToExistingContent()
        {
            var path = TempFile("append.txt");
            System.IO.File.WriteAllText(path, "existing");
            using (var f = Builtins.Open(path, "a"))
            {
                f.Write(" appended");
            }
            Assert.Equal("existing appended", System.IO.File.ReadAllText(path));
        }

        [Fact]
        public void AppendMode_CreatesFileIfNotExists()
        {
            var path = TempFile("newappend.txt");
            using (var f = Builtins.Open(path, "a"))
            {
                f.Write("created");
            }
            Assert.Equal("created", System.IO.File.ReadAllText(path));
        }

        // ===== Exclusive Create Mode Tests =====

        [Fact]
        public void ExclusiveMode_CreatesNewFile()
        {
            var path = TempFile("exclusive.txt");
            using (var f = Builtins.Open(path, "x"))
            {
                f.Write("exclusive");
            }
            Assert.Equal("exclusive", System.IO.File.ReadAllText(path));
        }

        [Fact]
        public void ExclusiveMode_ThrowsIfFileExists()
        {
            var path = TempFile("exists.txt");
            System.IO.File.WriteAllText(path, "existing");
            Assert.Throws<FileExistsError>(() => Builtins.Open(path, "x"));
        }

        // ===== Error Handling Tests =====

        [Fact]
        public void Open_ReadMode_ThrowsIfFileNotFound()
        {
            Assert.Throws<FileNotFoundError>(() => Builtins.Open(TempFile("nonexistent.txt")));
        }

        [Fact]
        public void Open_InvalidMode_ThrowsValueError()
        {
            Assert.Throws<ValueError>(() => Builtins.Open(TempFile("x.txt"), "z"));
        }

        [Fact]
        public void Read_OnWriteMode_ThrowsIOError()
        {
            var path = TempFile("writeonly.txt");
            using var f = Builtins.Open(path, "w");
            Assert.Throws<IOError>(() => f.Read());
        }

        [Fact]
        public void Write_OnReadMode_ThrowsIOError()
        {
            WriteTestFile("readonly.txt", "content");
            using var f = Builtins.Open(TempFile("readonly.txt"));
            Assert.Throws<IOError>(() => f.Write("data"));
        }

        [Fact]
        public void Read_OnClosedFile_ThrowsValueError()
        {
            WriteTestFile("closed.txt", "content");
            var f = Builtins.Open(TempFile("closed.txt"));
            f.Close();
            Assert.Throws<ValueError>(() => f.Read());
        }

        [Fact]
        public void Write_OnClosedFile_ThrowsValueError()
        {
            var path = TempFile("closedw.txt");
            var f = Builtins.Open(path, "w");
            f.Close();
            Assert.Throws<ValueError>(() => f.Write("data"));
        }

        // ===== Property Tests =====

        [Fact]
        public void Name_ReturnsFilePath()
        {
            WriteTestFile("name.txt", "");
            using var f = Builtins.Open(TempFile("name.txt"));
            Assert.Equal(TempFile("name.txt"), f.Name);
        }

        [Fact]
        public void Mode_ReturnsOpenMode()
        {
            WriteTestFile("mode.txt", "");
            using var f = Builtins.Open(TempFile("mode.txt"));
            Assert.Equal("r", f.Mode);
        }

        [Fact]
        public void Closed_IsFalseWhileOpen()
        {
            WriteTestFile("open.txt", "");
            using var f = Builtins.Open(TempFile("open.txt"));
            Assert.False(f.Closed);
        }

        [Fact]
        public void Closed_IsTrueAfterClose()
        {
            WriteTestFile("close.txt", "");
            var f = Builtins.Open(TempFile("close.txt"));
            f.Close();
            Assert.True(f.Closed);
        }

        // ===== Context Manager (IDisposable) Tests =====

        [Fact]
        public void Using_ClosesFileOnDispose()
        {
            WriteTestFile("dispose.txt", "");
            TextFile file;
            using (file = Builtins.Open(TempFile("dispose.txt")))
            {
                Assert.False(file.Closed);
            }
            Assert.True(file.Closed);
        }

        [Fact]
        public void Close_CanBeCalledMultipleTimes()
        {
            WriteTestFile("multi.txt", "");
            var f = Builtins.Open(TempFile("multi.txt"));
            f.Close();
            f.Close(); // Should not throw
            Assert.True(f.Closed);
        }

        // ===== Encoding Tests =====

        [Fact]
        public void Open_WithAsciiEncoding_Works()
        {
            var path = TempFile("ascii.txt");
            using (var f = Builtins.Open(path, "w", "ascii"))
            {
                f.Write("hello");
            }
            using (var f = Builtins.Open(path, "r", "ascii"))
            {
                Assert.Equal("hello", f.Read());
            }
        }

        [Fact]
        public void Open_WithUnknownEncoding_Throws()
        {
            Assert.Throws<LookupError>(() => Builtins.Open(TempFile("bad.txt"), "r", "unknown-encoding"));
        }

        // ===== Flush Tests =====

        [Fact]
        public void Flush_OnWriteMode_DoesNotThrow()
        {
            var path = TempFile("flush.txt");
            using var f = Builtins.Open(path, "w");
            f.Write("data");
            f.Flush(); // Should not throw
        }

        [Fact]
        public void Flush_OnReadMode_DoesNotThrow()
        {
            WriteTestFile("flushr.txt", "data");
            using var f = Builtins.Open(TempFile("flushr.txt"));
            f.Flush(); // Should not throw (no-op for read mode)
        }

        // ===== Round-Trip Tests =====

        [Fact]
        public void RoundTrip_WriteAndReadBack()
        {
            var path = TempFile("roundtrip.txt");
            var content = "Line 1\nLine 2\nLine 3\n";
            using (var w = Builtins.Open(path, "w"))
            {
                w.Write(content);
            }
            using (var r = Builtins.Open(path))
            {
                Assert.Equal(content, r.Read());
            }
        }

        [Fact]
        public void RoundTrip_WritelinesAndReadlines()
        {
            var path = TempFile("roundtrip2.txt");
            var inputLines = new[] { "first\n", "second\n", "third" };
            using (var w = Builtins.Open(path, "w"))
            {
                w.Writelines(inputLines);
            }
            using (var r = Builtins.Open(path))
            {
                var lines = r.Readlines();
                Assert.Equal(3, ((ICollection<string>)lines).Count);
                Assert.Equal("first\n", lines[0]);
                Assert.Equal("second\n", lines[1]);
                Assert.Equal("third", lines[2]);
            }
        }

        [Fact]
        public void RoundTrip_UnicodeContent()
        {
            var path = TempFile("unicode.txt");
            var content = "Hello 世界 🌍 café";
            using (var w = Builtins.Open(path, "w"))
            {
                w.Write(content);
            }
            using (var r = Builtins.Open(path))
            {
                Assert.Equal(content, r.Read());
            }
        }
    }
}
