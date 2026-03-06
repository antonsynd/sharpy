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
            try { System.IO.Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

        // ===== Constants =====

        [Fact]
        public void Sep_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(Os.Sep));
        }

        [Fact]
        public void Linesep_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(Os.Linesep));
        }

        [Fact]
        public void Name_IsPosixOrNt()
        {
            Assert.True(Os.Name == "posix" || Os.Name == "nt");
        }

        // ===== File Operations =====

        [Fact]
        public void Remove_DeletesFile()
        {
            var path = Sub("removeme.txt");
            System.IO.File.WriteAllText(path, "data");
            Os.Remove(path);
            Assert.False(System.IO.File.Exists(path));
        }

        [Fact]
        public void Remove_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => Os.Remove(Sub("nope.txt")));
        }

        [Fact]
        public void Rename_RenamesFile()
        {
            var src = Sub("old.txt");
            var dst = Sub("new.txt");
            System.IO.File.WriteAllText(src, "data");
            Os.Rename(src, dst);
            Assert.False(System.IO.File.Exists(src));
            Assert.True(System.IO.File.Exists(dst));
        }

        [Fact]
        public void Rename_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => Os.Rename(Sub("nope.txt"), Sub("also_nope.txt")));
        }

        // ===== Directory Operations =====

        [Fact]
        public void Mkdir_CreatesDirectory()
        {
            var path = Sub("newdir");
            Os.Mkdir(path);
            Assert.True(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Mkdir_ThrowsIfExists()
        {
            var path = Sub("existdir");
            System.IO.Directory.CreateDirectory(path);
            Assert.Throws<FileExistsError>(() => Os.Mkdir(path));
        }

        [Fact]
        public void Makedirs_CreatesNestedDirectories()
        {
            var path = System.IO.Path.Combine(_tempDir, "a", "b", "c");
            Os.Makedirs(path);
            Assert.True(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Makedirs_ExistOk_DoesNotThrow()
        {
            var path = Sub("existing");
            System.IO.Directory.CreateDirectory(path);
            Os.Makedirs(path, exist_ok: true); // should not throw
        }

        [Fact]
        public void Makedirs_NotExistOk_Throws()
        {
            var path = Sub("existing2");
            System.IO.Directory.CreateDirectory(path);
            Assert.Throws<FileExistsError>(() => Os.Makedirs(path, exist_ok: false));
        }

        [Fact]
        public void Rmdir_RemovesEmptyDirectory()
        {
            var path = Sub("emptydir");
            System.IO.Directory.CreateDirectory(path);
            Os.Rmdir(path);
            Assert.False(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Rmdir_ThrowsOnNonEmpty()
        {
            var path = Sub("notempty");
            System.IO.Directory.CreateDirectory(path);
            System.IO.File.WriteAllText(System.IO.Path.Combine(path, "file.txt"), "data");
            Assert.Throws<IOError>(() => Os.Rmdir(path));
        }

        [Fact]
        public void Rmdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => Os.Rmdir(Sub("nope")));
        }

        [Fact]
        public void Listdir_ReturnsEntries()
        {
            var dir = Sub("listdir");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a.txt"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "b.txt"), "");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "subdir"));

            var entries = Os.Listdir(dir);
            Assert.Contains("a.txt", (IEnumerable<string>)entries);
            Assert.Contains("b.txt", (IEnumerable<string>)entries);
            Assert.Contains("subdir", (IEnumerable<string>)entries);
        }

        [Fact]
        public void Listdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => Os.Listdir(Sub("nope")));
        }

        [Fact]
        public void Getcwd_ReturnsNonEmptyString()
        {
            var cwd = Os.Getcwd();
            Assert.False(string.IsNullOrEmpty(cwd));
        }

        [Fact]
        public void Chdir_ChangesDirectory()
        {
            var original = Os.Getcwd();
            try
            {
                Os.Chdir(_tempDir);
                // Verify we moved somewhere that contains the temp dir name component
                var cwd = Os.Getcwd();
                var dirName = System.IO.Path.GetFileName(_tempDir);
                Assert.Contains(dirName, cwd);
            }
            finally
            {
                Os.Chdir(original);
            }
        }

        [Fact]
        public void Chdir_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => Os.Chdir(Sub("nope")));
        }

        // ===== Environment Variables =====

        [Fact]
        public void Getenv_ReturnsNullForMissing()
        {
            Assert.Null(Os.Getenv("SHARPY_TEST_NONEXISTENT_" + Guid.NewGuid().ToString("N")));
        }

        [Fact]
        public void Getenv_WithDefault_ReturnsDefault()
        {
            Assert.Equal("fallback", Os.Getenv("SHARPY_TEST_NONEXISTENT_" + Guid.NewGuid().ToString("N"), "fallback"));
        }

        [Fact]
        public void Putenv_And_Getenv_RoundTrip()
        {
            var key = "SHARPY_TEST_" + Guid.NewGuid().ToString("N");
            Os.Putenv(key, "testvalue");
            try
            {
                Assert.Equal("testvalue", Os.Getenv(key));
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Fact]
        public void Environ_ReturnsDictWithEntries()
        {
            var env = Os.Environ;
            Assert.True(env.Count > 0);
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
            foreach (var (dirpath, dirnames, filenames) in Os.Walk(root))
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
            foreach (var _ in Os.Walk(Sub("nonexistent")))
            {
                count++;
            }
            Assert.Equal(0, count);
        }
    }

    // ===== OsPath Tests =====

    public class OsPathTests
    {
        [Fact]
        public void Join_CombinesPaths()
        {
            var result = OsPath.Join("a", "b");
            Assert.Contains("b", result);
        }

        [Fact]
        public void Exists_ReturnsTrueForExistingDir()
        {
            Assert.True(OsPath.Exists(System.IO.Path.GetTempPath()));
        }

        [Fact]
        public void Exists_ReturnsFalseForNonexistent()
        {
            Assert.False(OsPath.Exists("/nonexistent_dir_12345"));
        }

        [Fact]
        public void Isfile_ReturnsFalseForDirectory()
        {
            Assert.False(OsPath.Isfile(System.IO.Path.GetTempPath()));
        }

        [Fact]
        public void Isdir_ReturnsTrueForDirectory()
        {
            Assert.True(OsPath.Isdir(System.IO.Path.GetTempPath()));
        }

        [Fact]
        public void Isabs_ReturnsTrueForAbsolutePath()
        {
            Assert.True(OsPath.Isabs("/usr/local"));
        }

        [Fact]
        public void Isabs_ReturnsFalseForRelativePath()
        {
            Assert.False(OsPath.Isabs("relative/path"));
        }

        [Fact]
        public void Basename_ReturnsFileName()
        {
            Assert.Equal("file.txt", OsPath.Basename("/some/path/file.txt"));
        }

        [Fact]
        public void Dirname_ReturnsDirectoryPart()
        {
            var result = OsPath.Dirname("/some/path/file.txt");
            Assert.Contains("some", result);
            Assert.Contains("path", result);
        }

        [Fact]
        public void Split_ReturnsDirnameAndBasename()
        {
            var (head, tail) = OsPath.Split("/some/path/file.txt");
            Assert.Equal("file.txt", tail);
            Assert.Contains("path", head);
        }

        [Fact]
        public void Splitext_SplitsExtension()
        {
            var (root, ext) = OsPath.Splitext("/some/file.tar.gz");
            Assert.Equal(".gz", ext);
            Assert.EndsWith(".tar", root);
        }

        [Fact]
        public void Splitext_NoExtension()
        {
            var (root, ext) = OsPath.Splitext("noext");
            Assert.Equal("noext", root);
            Assert.Equal("", ext);
        }

        [Fact]
        public void Abspath_ReturnsAbsolutePath()
        {
            var result = OsPath.Abspath("relative");
            Assert.True(OsPath.Isabs(result));
        }

        [Fact]
        public void Expanduser_ExpandsTilde()
        {
            var result = OsPath.Expanduser("~");
            Assert.NotEqual("~", result);
            Assert.True(result.Length > 1);
        }
    }
}
