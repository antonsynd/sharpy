using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests
{
    public class ShutilTests : IDisposable
    {
        private readonly string _tempDir;

        public ShutilTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_shutil_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            { Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

        // ===== copy =====

        [Fact]
        public void Copy_CopiesToFile()
        {
            File.WriteAllText(Sub("src.txt"), "hello");
            string result = Sharpy.ShutilModule.Copy(Sub("src.txt"), Sub("dst.txt"));

            File.Exists(Sub("dst.txt")).Should().BeTrue();
            File.ReadAllText(Sub("dst.txt")).Should().Be("hello");
            result.Should().Be(Sub("dst.txt"));
        }

        [Fact]
        public void Copy_CopiesToDirectory()
        {
            File.WriteAllText(Sub("src.txt"), "hello");
            Directory.CreateDirectory(Sub("destdir"));

            string result = Sharpy.ShutilModule.Copy(Sub("src.txt"), Sub("destdir"));

            string expected = System.IO.Path.Combine(Sub("destdir"), "src.txt");
            File.Exists(expected).Should().BeTrue();
            result.Should().Be(expected);
        }

        [Fact]
        public void Copy_ThrowsOnNonexistentSource()
        {
            Assert.Throws<OSError>(() => Sharpy.ShutilModule.Copy(Sub("nope.txt"), Sub("dst.txt")));
        }

        // ===== copy2 =====

        [Fact]
        public void Copy2_PreservesTimestamps()
        {
            string srcPath = Sub("src2.txt");
            File.WriteAllText(srcPath, "data");
            var originalWrite = File.GetLastWriteTimeUtc(srcPath);

            // Set a distinct timestamp
            var customTime = new System.DateTime(2020, 6, 15, 12, 0, 0, System.DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(srcPath, customTime);

            string result = Sharpy.ShutilModule.Copy2(srcPath, Sub("dst2.txt"));

            File.GetLastWriteTimeUtc(result).Should().Be(customTime);
        }

        // ===== copytree =====

        [Fact]
        public void Copytree_CopiesRecursively()
        {
            string srcDir = Sub("treesrc");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(System.IO.Path.Combine(srcDir, "a.txt"), "a");
            Directory.CreateDirectory(System.IO.Path.Combine(srcDir, "sub"));
            File.WriteAllText(System.IO.Path.Combine(srcDir, "sub", "b.txt"), "b");

            string dstDir = Sub("treedst");
            string result = Sharpy.ShutilModule.Copytree(srcDir, dstDir);

            result.Should().Be(dstDir);
            File.Exists(System.IO.Path.Combine(dstDir, "a.txt")).Should().BeTrue();
            File.Exists(System.IO.Path.Combine(dstDir, "sub", "b.txt")).Should().BeTrue();
            File.ReadAllText(System.IO.Path.Combine(dstDir, "sub", "b.txt")).Should().Be("b");
        }

        [Fact]
        public void Copytree_ThrowsOnNonexistentSource()
        {
            Assert.Throws<OSError>(() => Sharpy.ShutilModule.Copytree(Sub("nope"), Sub("dst")));
        }

        // ===== rmtree =====

        [Fact]
        public void Rmtree_DeletesDirectoryTree()
        {
            string dir = Sub("rmdir");
            Directory.CreateDirectory(dir);
            File.WriteAllText(System.IO.Path.Combine(dir, "file.txt"), "data");
            Directory.CreateDirectory(System.IO.Path.Combine(dir, "sub"));
            File.WriteAllText(System.IO.Path.Combine(dir, "sub", "inner.txt"), "inner");

            Sharpy.ShutilModule.Rmtree(dir);

            Directory.Exists(dir).Should().BeFalse();
        }

        [Fact]
        public void Rmtree_ThrowsOnNonexistent()
        {
            Assert.Throws<OSError>(() => Sharpy.ShutilModule.Rmtree(Sub("nope")));
        }

        // ===== move =====

        [Fact]
        public void Move_MovesFile()
        {
            File.WriteAllText(Sub("movesrc.txt"), "move");
            string result = Sharpy.ShutilModule.Move(Sub("movesrc.txt"), Sub("movedst.txt"));

            File.Exists(Sub("movesrc.txt")).Should().BeFalse();
            File.Exists(Sub("movedst.txt")).Should().BeTrue();
            File.ReadAllText(Sub("movedst.txt")).Should().Be("move");
            result.Should().Be(Sub("movedst.txt"));
        }

        [Fact]
        public void Move_MovesDirectory()
        {
            string srcDir = Sub("movedirsrc");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(System.IO.Path.Combine(srcDir, "f.txt"), "f");

            string dstDir = Sub("movedirdst");
            string result = Sharpy.ShutilModule.Move(srcDir, dstDir);

            Directory.Exists(srcDir).Should().BeFalse();
            Directory.Exists(dstDir).Should().BeTrue();
            File.Exists(System.IO.Path.Combine(dstDir, "f.txt")).Should().BeTrue();
            result.Should().Be(dstDir);
        }

        [Fact]
        public void Move_ThrowsOnNonexistentSource()
        {
            Assert.Throws<OSError>(() => Sharpy.ShutilModule.Move(Sub("nope.txt"), Sub("dst.txt")));
        }

        // ===== which =====

        [Fact]
        public void Which_FindsDotnet()
        {
            string? result = Sharpy.ShutilModule.Which("dotnet");
            result.Should().NotBeNull();
            File.Exists(result).Should().BeTrue();
        }

        [Fact]
        public void Which_ReturnsNullForNonexistent()
        {
            string? result = Sharpy.ShutilModule.Which("this_command_does_not_exist_xyz_123");
            result.Should().BeNull();
        }

        [Fact]
        public void Which_ReturnsNullForEmpty()
        {
            string? result = Sharpy.ShutilModule.Which("");
            result.Should().BeNull();
        }

        // ===== disk_usage =====

        [Fact]
        public void DiskUsage_ReturnsSensibleValues()
        {
            var (total, used, free) = Sharpy.ShutilModule.DiskUsage(_tempDir);

            total.Should().BeGreaterThan(0);
            used.Should().BeGreaterThanOrEqualTo(0);
            free.Should().BeGreaterThanOrEqualTo(0);
            (used + free).Should().Be(total);
        }
    }
}
