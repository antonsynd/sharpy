using System;
using System.IO;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class UnittestTmpPathTests
    {
        [Fact]
        public void Constructor_CreatesUniqueDirectory()
        {
            using var fixture = new TmpPathFixture();

            Assert.False(string.IsNullOrEmpty(fixture.Value));
            Assert.True(Directory.Exists(fixture.Value));
            // Created under the system temp path with the documented prefix.
            Assert.StartsWith("sharpy-test-", SysPath.GetFileName(fixture.Value));
            string tempRoot = SysPath.GetFullPath(SysPath.GetTempPath()).TrimEnd(SysPath.DirectorySeparatorChar);
            Assert.StartsWith(tempRoot, SysPath.GetFullPath(fixture.Value));
        }

        [Fact]
        public void DistinctInstances_GetDistinctDirectories()
        {
            // Pins pytest's per-test semantics: two tests sharing tmp_path must not collide.
            using var a = new TmpPathFixture();
            using var b = new TmpPathFixture();

            Assert.NotEqual(a.Value, b.Value);
            Assert.True(Directory.Exists(a.Value));
            Assert.True(Directory.Exists(b.Value));
        }

        [Fact]
        public void Dispose_RecursivelyDeletesDirectoryTree()
        {
            string root;
            using (var fixture = new TmpPathFixture())
            {
                root = fixture.Value;

                // Populate with nested directories and files.
                string nested = SysPath.Combine(root, "sub", "deeper");
                Directory.CreateDirectory(nested);
                File.WriteAllText(SysPath.Combine(root, "top.txt"), "top");
                File.WriteAllText(SysPath.Combine(nested, "leaf.txt"), "leaf");

                Assert.True(File.Exists(SysPath.Combine(nested, "leaf.txt")));
            }

            Assert.False(Directory.Exists(root));
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var fixture = new TmpPathFixture();
            string root = fixture.Value;

            fixture.Dispose();
            Assert.False(Directory.Exists(root));

            // A second dispose must be a harmless no-op.
            fixture.Dispose();
            Assert.False(Directory.Exists(root));
        }

        [Fact]
        public void Dispose_ToleratesAlreadyDeletedDirectory()
        {
            var fixture = new TmpPathFixture();

            // Simulate external cleanup (or a test that already removed the tree).
            Directory.Delete(fixture.Value, true);
            Assert.False(Directory.Exists(fixture.Value));

            // Must not throw.
            fixture.Dispose();
        }

        [Fact]
        public void Dispose_DoesNotThrow_WhenAFileHandleIsStillOpen()
        {
            var fixture = new TmpPathFixture();
            string filePath = SysPath.Combine(fixture.Value, "locked.txt");

            // On Windows an open handle blocks deletion (IOException, swallowed);
            // on Unix the open file is deleted. Either way, Dispose must not throw.
            using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(0x42);
                stream.Flush();

                Exception? thrown = Record.Exception(() => fixture.Dispose());
                Assert.Null(thrown);
            }

            // Clean up anything the best-effort delete left behind.
            try
            {
                if (Directory.Exists(fixture.Value))
                {
                    Directory.Delete(fixture.Value, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
