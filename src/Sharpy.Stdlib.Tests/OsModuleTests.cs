using System;
using System.Collections.Generic;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class OsModuleTests : IDisposable
    {
        private readonly string _tempDir;

        public OsModuleTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_os_tests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            { System.IO.Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

        // ===== Constants =====

        [Fact]
        public void Sep_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(OsModule.Sep));
        }

        [Fact]
        public void Linesep_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(OsModule.Linesep));
        }

        [Fact]
        public void Name_IsPosixOrNt()
        {
            Assert.True(OsModule.Name == "posix" || OsModule.Name == "nt");
        }

        // ===== File Operations =====

        [Fact]
        public void Remove_DeletesFile()
        {
            var path = Sub("removeme.txt");
            System.IO.File.WriteAllText(path, "data");
            OsModule.Remove(path);
            Assert.False(System.IO.File.Exists(path));
        }

        [Fact]
        public void Remove_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Remove(Sub("nope.txt")));
        }

        [Fact]
        public void Rename_RenamesFile()
        {
            var src = Sub("old.txt");
            var dst = Sub("new.txt");
            System.IO.File.WriteAllText(src, "data");
            OsModule.Rename(src, dst);
            Assert.False(System.IO.File.Exists(src));
            Assert.True(System.IO.File.Exists(dst));
        }

        [Fact]
        public void Rename_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Rename(Sub("nope.txt"), Sub("also_nope.txt")));
        }

        // ===== Directory Operations =====

        [Fact]
        public void Mkdir_CreatesDirectory()
        {
            var path = Sub("newdir");
            OsModule.Mkdir(path);
            Assert.True(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Mkdir_ThrowsIfExists()
        {
            var path = Sub("existdir");
            System.IO.Directory.CreateDirectory(path);
            Assert.Throws<FileExistsError>(() => OsModule.Mkdir(path));
        }

        [Fact]
        public void Makedirs_CreatesNestedDirectories()
        {
            var path = System.IO.Path.Combine(_tempDir, "a", "b", "c");
            OsModule.Makedirs(path);
            Assert.True(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Makedirs_ExistOk_DoesNotThrow()
        {
            var path = Sub("existing");
            System.IO.Directory.CreateDirectory(path);
            OsModule.Makedirs(path, existOk: true); // should not throw
        }

        [Fact]
        public void Makedirs_NotExistOk_Throws()
        {
            var path = Sub("existing2");
            System.IO.Directory.CreateDirectory(path);
            Assert.Throws<FileExistsError>(() => OsModule.Makedirs(path, existOk: false));
        }

        [Fact]
        public void Rmdir_RemovesEmptyDirectory()
        {
            var path = Sub("emptydir");
            System.IO.Directory.CreateDirectory(path);
            OsModule.Rmdir(path);
            Assert.False(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Rmdir_ThrowsOnNonEmpty()
        {
            var path = Sub("notempty");
            System.IO.Directory.CreateDirectory(path);
            System.IO.File.WriteAllText(System.IO.Path.Combine(path, "file.txt"), "data");
            Assert.Throws<IOError>(() => OsModule.Rmdir(path));
        }

        [Fact]
        public void Rmdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Rmdir(Sub("nope")));
        }

        [Fact]
        public void Listdir_ReturnsEntries()
        {
            var dir = Sub("listdir");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a.txt"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "b.txt"), "");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "subdir"));

            var entries = OsModule.Listdir(dir);
            Assert.Contains("a.txt", (IEnumerable<string>)entries);
            Assert.Contains("b.txt", (IEnumerable<string>)entries);
            Assert.Contains("subdir", (IEnumerable<string>)entries);
        }

        [Fact]
        public void Listdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Listdir(Sub("nope")));
        }

        [Fact]
        public void Getcwd_ReturnsNonEmptyString()
        {
            var cwd = OsModule.Getcwd();
            Assert.False(string.IsNullOrEmpty(cwd));
        }

        [Fact]
        public void Chdir_ChangesDirectory()
        {
            var original = OsModule.Getcwd();
            try
            {
                OsModule.Chdir(_tempDir);
                // Verify we moved somewhere that contains the temp dir name component
                var cwd = OsModule.Getcwd();
                var dirName = System.IO.Path.GetFileName(_tempDir);
                Assert.Contains(dirName, cwd, StringComparison.Ordinal);
            }
            finally
            {
                OsModule.Chdir(original);
            }
        }

        [Fact]
        public void Chdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Chdir(Sub("nope")));
        }

        // ===== Environment Variables =====

        [Fact]
        public void Getenv_ReturnsNullForMissing()
        {
            Assert.True(OsModule.Getenv("SHARPY_TEST_NONEXISTENT_" + Guid.NewGuid().ToString("N")).IsNone);
        }

        [Fact]
        public void Getenv_WithDefault_ReturnsDefault()
        {
            Assert.Equal("fallback", OsModule.Getenv("SHARPY_TEST_NONEXISTENT_" + Guid.NewGuid().ToString("N"), "fallback"));
        }

        [Fact]
        public void Putenv_And_Getenv_RoundTrip()
        {
            var key = "SHARPY_TEST_" + Guid.NewGuid().ToString("N");
            OsModule.Putenv(key, "testvalue");
            try
            {
                Assert.Equal("testvalue", OsModule.Getenv(key).Unwrap());
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Fact]
        public void Environ_ReturnsDictWithEntries()
        {
            var env = OsModule.Environ;
            Assert.True(env.Count > 0);
        }

        // ===== PathExists =====

        [Fact]
        public void PathExists_TrueForFile()
        {
            var path = Sub("exists_file.txt");
            System.IO.File.WriteAllText(path, "data");
            Assert.True(OsModule.PathExists(path));
        }

        [Fact]
        public void PathExists_TrueForDirectory()
        {
            var path = Sub("exists_dir");
            System.IO.Directory.CreateDirectory(path);
            Assert.True(OsModule.PathExists(path));
        }

        [Fact]
        public void PathExists_FalseForNonexistent()
        {
            Assert.False(OsModule.PathExists(Sub("nonexistent_path")));
        }

        // ===== Stat =====

        [Fact]
        public void Stat_ReturnsFileSize()
        {
            var path = Sub("stat_file.txt");
            System.IO.File.WriteAllText(path, "hello");
            var result = OsModule.Stat(path);
            Assert.True(result.StSize > 0);
        }

        [Fact]
        public void Stat_ReturnsTimestamps()
        {
            var path = Sub("stat_time.txt");
            System.IO.File.WriteAllText(path, "data");
            var result = OsModule.Stat(path);
            // Timestamps should be reasonable (after year 2020 = 1577836800)
            Assert.True(result.StMtime > 1577836800);
            Assert.True(result.StCtime > 1577836800);
            Assert.True(result.StAtime > 1577836800);
        }

        [Fact]
        public void Stat_WorksForDirectories()
        {
            var path = Sub("stat_dir");
            System.IO.Directory.CreateDirectory(path);
            var result = OsModule.Stat(path);
            Assert.Equal(0, result.StSize);
            Assert.True(result.StMtime > 1577836800);
        }

        [Fact]
        public void Stat_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => OsModule.Stat(Sub("nonexistent_stat")));
        }

        // ===== Walk =====

        [Fact]
        public void Walk_TraversesDirectoryTree()
        {
            // Create a small directory tree
            var root = Sub("walktest");
            System.IO.Directory.CreateDirectory(root);
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, "file1.txt"), "");
            var sub = System.IO.Path.Combine(root, "sub");
            System.IO.Directory.CreateDirectory(sub);
            System.IO.File.WriteAllText(System.IO.Path.Combine(sub, "file2.txt"), "");

            var results = new System.Collections.Generic.List<string>();
            foreach (var (dirpath, dirnames, filenames) in OsModule.Walk(root))
            {
                results.Add(dirpath);
            }

            Assert.Equal(2, results.Count);
            Assert.Equal(root, results[0]);
            Assert.Equal(sub, results[1]);
        }

        [Fact]
        public void Walk_NonexistentPath_YieldsNothing()
        {
            var count = 0;
            foreach (var _ in OsModule.Walk(Sub("nonexistent")))
            {
                count++;
            }
            Assert.Equal(0, count);
        }
    }
}
