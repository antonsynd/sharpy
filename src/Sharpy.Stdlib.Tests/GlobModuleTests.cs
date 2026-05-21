using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests
{
    public class GlobModuleTests : IDisposable
    {
        private readonly string _tempDir;

        public GlobModuleTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_glob_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            // Create a known directory structure for testing
            File.WriteAllText(System.IO.Path.Combine(_tempDir, "a.txt"), "a");
            File.WriteAllText(System.IO.Path.Combine(_tempDir, "b.txt"), "b");
            File.WriteAllText(System.IO.Path.Combine(_tempDir, "c.py"), "c");
            File.WriteAllText(System.IO.Path.Combine(_tempDir, "data.csv"), "d");

            string subDir = System.IO.Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(System.IO.Path.Combine(subDir, "d.txt"), "d");
            File.WriteAllText(System.IO.Path.Combine(subDir, "e.py"), "e");

            string deepDir = System.IO.Path.Combine(subDir, "deep");
            Directory.CreateDirectory(deepDir);
            File.WriteAllText(System.IO.Path.Combine(deepDir, "f.txt"), "f");
        }

        public void Dispose()
        {
            try
            { Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        [Fact]
        public void Glob_MatchesTxtFiles()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "*.txt");
            var results = Sharpy.GlobModule.Glob(pattern);

            ((ISized)results).Count.Should().Be(2);
            results.Should().Contain(r => r.EndsWith("a.txt"));
            results.Should().Contain(r => r.EndsWith("b.txt"));
        }

        [Fact]
        public void Glob_MatchesPyFiles()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "*.py");
            var results = Sharpy.GlobModule.Glob(pattern);

            ((ISized)results).Count.Should().Be(1);
            results.Should().Contain(r => r.EndsWith("c.py"));
        }

        [Fact]
        public void Glob_RecursiveDoubleStarTxt()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "**", "*.txt");
            var results = Sharpy.GlobModule.Glob(pattern);

            // Should find a.txt, b.txt, sub/d.txt, sub/deep/f.txt
            ((ISized)results).Count.Should().Be(4);
        }

        [Fact]
        public void Glob_QuestionMarkWildcard()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "?.txt");
            var results = Sharpy.GlobModule.Glob(pattern);

            // a.txt, b.txt
            ((ISized)results).Count.Should().Be(2);
        }

        [Fact]
        public void Glob_NoMatchesReturnsEmptyList()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "*.xyz");
            var results = Sharpy.GlobModule.Glob(pattern);

            ((ISized)results).Count.Should().Be(0);
        }

        [Fact]
        public void Glob_ResultsAreSorted()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "*.*");
            var results = Sharpy.GlobModule.Glob(pattern);

            var sorted = results.ToList();
            sorted.Sort(StringComparer.Ordinal);

            for (int i = 0; i < ((ISized)results).Count; i++)
            {
                results[i].Should().Be(sorted[i]);
            }
        }

        [Fact]
        public void Glob_NonExistentDirectoryReturnsEmpty()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "nonexistent", "*.txt");
            var results = Sharpy.GlobModule.Glob(pattern);

            ((ISized)results).Count.Should().Be(0);
        }

        [Fact]
        public void Iglob_ReturnsLazyEnumerable()
        {
            var pattern = System.IO.Path.Combine(_tempDir, "*.txt");
            var results = Sharpy.GlobModule.Iglob(pattern);

            // Just verify it's enumerable and yields results
            results.Count().Should().Be(2);
        }

        [Fact]
        public void Escape_EscapesAsterisk()
        {
            Sharpy.GlobModule.Escape("file*.txt").Should().Be("file[*].txt");
        }

        [Fact]
        public void Escape_EscapesQuestionMark()
        {
            Sharpy.GlobModule.Escape("file?.txt").Should().Be("file[?].txt");
        }

        [Fact]
        public void Escape_EscapesBracket()
        {
            Sharpy.GlobModule.Escape("file[1].txt").Should().Be("file[[]1].txt");
        }

        [Fact]
        public void Escape_LeavesNormalCharsUnchanged()
        {
            Sharpy.GlobModule.Escape("normal.txt").Should().Be("normal.txt");
        }

        [Fact]
        public void Escape_EmptyStringReturnsEmpty()
        {
            Sharpy.GlobModule.Escape("").Should().Be("");
        }
    }
}
