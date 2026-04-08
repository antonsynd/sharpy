using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class PathlibTests : IDisposable
    {
        private readonly string _tempDir;

        public PathlibTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_pathlib_tests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            { System.IO.Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

        // ===== Constructor =====

        [Fact]
        public void Constructor_StoresPath()
        {
            var p = new Path("/some/path");
            Assert.Equal("/some/path", p.ToString());
        }

        [Fact]
        public void Constructor_JoinsSegments()
        {
            var p = new Path("a", "b");
            Assert.Contains("b", p.ToString(), StringComparison.Ordinal);
        }

        // ===== Operator / =====

        [Fact]
        public void DivisionOperator_JoinsString()
        {
            var p = new Path("/root") / "child";
            Assert.Contains("child", p.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void DivisionOperator_JoinsPath()
        {
            var p = new Path("/root") / new Path("child");
            Assert.Contains("child", p.ToString(), StringComparison.Ordinal);
        }

        // ===== Properties =====

        [Fact]
        public void Name_ReturnsFinalComponent()
        {
            var p = new Path("/some/path/file.txt");
            Assert.Equal("file.txt", p.Name);
        }

        [Fact]
        public void Stem_ReturnsNameWithoutExtension()
        {
            var p = new Path("/some/path/file.txt");
            Assert.Equal("file", p.Stem);
        }

        [Fact]
        public void Suffix_ReturnsExtension()
        {
            var p = new Path("/some/path/file.txt");
            Assert.Equal(".txt", p.Suffix);
        }

        [Fact]
        public void Suffix_EmptyWhenNoExtension()
        {
            var p = new Path("/some/path/file");
            Assert.Equal("", p.Suffix);
        }

        [Fact]
        public void Suffixes_ReturnsAll()
        {
            var p = new Path("archive.tar.gz");
            var suffixes = p.Suffixes;
            Assert.Equal(2, ((ICollection<string>)suffixes).Count);
            Assert.Equal(".tar", suffixes[0]);
            Assert.Equal(".gz", suffixes[1]);
        }

        [Fact]
        public void Parent_ReturnsParentPath()
        {
            var p = new Path("/some/path/file.txt");
            Assert.Contains("path", p.Parent.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Root_ReturnsPathRoot()
        {
            var p = new Path("/some/path");
            Assert.Equal("/", p.Root);
        }

        [Fact]
        public void IsAbsolute_TrueForAbsolutePaths()
        {
            Assert.True(new Path("/absolute").IsAbsolute);
        }

        [Fact]
        public void IsAbsolute_FalseForRelativePaths()
        {
            Assert.False(new Path("relative").IsAbsolute);
        }

        // ===== Query Methods =====

        [Fact]
        public void Exists_TrueForExistingFile()
        {
            var path = Sub("exists.txt");
            System.IO.File.WriteAllText(path, "data");
            Assert.True(new Path(path).Exists());
        }

        [Fact]
        public void Exists_FalseForNonexistent()
        {
            Assert.False(new Path(Sub("nope.txt")).Exists());
        }

        [Fact]
        public void IsFile_TrueForFile()
        {
            var path = Sub("file.txt");
            System.IO.File.WriteAllText(path, "data");
            Assert.True(new Path(path).IsFile());
        }

        [Fact]
        public void IsDir_TrueForDirectory()
        {
            Assert.True(new Path(_tempDir).IsDir());
        }

        // ===== File I/O =====

        [Fact]
        public void ReadText_And_WriteText_RoundTrip()
        {
            var path = Sub("roundtrip.txt");
            var p = new Path(path);
            p.WriteText("hello world");
            Assert.Equal("hello world", p.ReadText());
        }

        [Fact]
        public void ReadBytes_And_WriteBytes_RoundTrip()
        {
            var path = Sub("bytes.dat");
            var p = new Path(path);
            var data = new byte[] { 1, 2, 3, 4, 5 };
            p.WriteBytes(data);
            Assert.Equal(data, p.ReadBytes());
        }

        // ===== Directory Operations =====

        [Fact]
        public void Mkdir_CreatesDirectory()
        {
            var p = new Path(Sub("newdir"));
            p.Mkdir();
            Assert.True(p.IsDir());
        }

        [Fact]
        public void Mkdir_Parents_CreatesNestedDirectories()
        {
            var p = new Path(System.IO.Path.Combine(_tempDir, "a", "b", "c"));
            p.Mkdir(parents: true);
            Assert.True(p.IsDir());
        }

        [Fact]
        public void Rmdir_RemovesEmptyDirectory()
        {
            var path = Sub("tormdir");
            System.IO.Directory.CreateDirectory(path);
            new Path(path).Rmdir();
            Assert.False(System.IO.Directory.Exists(path));
        }

        [Fact]
        public void Iterdir_ListsEntries()
        {
            System.IO.File.WriteAllText(Sub("a.txt"), "");
            System.IO.File.WriteAllText(Sub("b.txt"), "");

            var entries = new System.Collections.Generic.List<string>();
            foreach (var entry in new Path(_tempDir).Iterdir())
            {
                entries.Add(entry.Name);
            }
            Assert.Contains("a.txt", entries);
            Assert.Contains("b.txt", entries);
        }

        [Fact]
        public void Glob_MatchesPattern()
        {
            System.IO.File.WriteAllText(Sub("test1.txt"), "");
            System.IO.File.WriteAllText(Sub("test2.txt"), "");
            System.IO.File.WriteAllText(Sub("other.md"), "");

            var matches = new System.Collections.Generic.List<string>();
            foreach (var entry in new Path(_tempDir).Glob("*.txt"))
            {
                matches.Add(entry.Name);
            }
            Assert.Equal(2, matches.Count);
        }

        // ===== Mutation =====

        [Fact]
        public void Unlink_DeletesFile()
        {
            var path = Sub("todelete.txt");
            System.IO.File.WriteAllText(path, "data");
            new Path(path).Unlink();
            Assert.False(System.IO.File.Exists(path));
        }

        [Fact]
        public void Unlink_MissingOk_DoesNotThrow()
        {
            new Path(Sub("nonexistent.txt")).Unlink(missing_ok: true);
        }

        [Fact]
        public void Unlink_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => new Path(Sub("nope.txt")).Unlink());
        }

        // ===== Navigation =====

        [Fact]
        public void Resolve_ReturnsAbsolutePath()
        {
            var p = new Path("relative").Resolve();
            Assert.True(p.IsAbsolute);
        }

        [Fact]
        public void WithName_ChangesName()
        {
            var p = new Path("/some/path/file.txt").WithName("other.md");
            Assert.Equal("other.md", p.Name);
        }

        [Fact]
        public void WithStem_ChangesStem()
        {
            var p = new Path("/some/path/file.txt").WithStem("other");
            Assert.Equal("other.txt", p.Name);
        }

        [Fact]
        public void WithSuffix_ChangesSuffix()
        {
            var p = new Path("/some/path/file.txt").WithSuffix(".md");
            Assert.Equal("file.md", p.Name);
        }

        // ===== Rename =====

        [Fact]
        public void Rename_RenamesFile()
        {
            var src = Sub("rename_src.txt");
            System.IO.File.WriteAllText(src, "content");
            var dst = Sub("rename_dst.txt");
            var result = new Path(src).Rename(dst);
            Assert.False(System.IO.File.Exists(src));
            Assert.True(System.IO.File.Exists(dst));
            Assert.Equal(dst, result.ToString());
            Assert.Equal("content", System.IO.File.ReadAllText(dst));
        }

        [Fact]
        public void Rename_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => new Path(Sub("nonexistent.txt")).Rename(Sub("dst.txt")));
        }

        // ===== Replace =====

        [Fact]
        public void Replace_ReplacesExistingTarget()
        {
            var src = Sub("replace_src.txt");
            var dst = Sub("replace_dst.txt");
            System.IO.File.WriteAllText(src, "new content");
            System.IO.File.WriteAllText(dst, "old content");
            var result = new Path(src).Replace(dst);
            Assert.False(System.IO.File.Exists(src));
            Assert.True(System.IO.File.Exists(dst));
            Assert.Equal(dst, result.ToString());
            Assert.Equal("new content", System.IO.File.ReadAllText(dst));
        }

        [Fact]
        public void Replace_WorksWhenTargetDoesNotExist()
        {
            var src = Sub("replace_src2.txt");
            var dst = Sub("replace_dst2.txt");
            System.IO.File.WriteAllText(src, "data");
            var result = new Path(src).Replace(dst);
            Assert.False(System.IO.File.Exists(src));
            Assert.True(System.IO.File.Exists(dst));
            Assert.Equal(dst, result.ToString());
        }

        // ===== RelativeTo =====

        [Fact]
        public void RelativeTo_ComputesRelativePath()
        {
            var child = new Path(System.IO.Path.Combine(_tempDir, "a", "b"));
            var relative = child.RelativeTo(_tempDir);
            var expected = System.IO.Path.Combine("a", "b");
            Assert.Equal(expected, relative.ToString());
        }

        [Fact]
        public void RelativeTo_ThrowsWhenNotRelative()
        {
            Assert.Throws<ValueError>(() => new Path("/completely/different").RelativeTo("/other/base"));
        }

        [Fact]
        public void RelativeTo_SamePath_ReturnsDot()
        {
            var p = new Path(_tempDir);
            var result = p.RelativeTo(_tempDir);
            Assert.Equal(".", result.ToString());
        }

        // ===== Parts =====

        [Fact]
        public void Parts_ReturnsComponents()
        {
            var p = new Path("/usr/local/bin");
            var parts = p.Parts;
            Assert.Equal("/", parts[0]);
            Assert.Equal("usr", parts[1]);
            Assert.Equal("local", parts[2]);
            Assert.Equal("bin", parts[3]);
        }

        [Fact]
        public void Parts_RelativePath()
        {
            var p = new Path("a/b/c");
            var parts = p.Parts;
            Assert.Equal(3, ((ICollection<string>)parts).Count);
            Assert.Equal("a", parts[0]);
            Assert.Equal("b", parts[1]);
            Assert.Equal("c", parts[2]);
        }

        // ===== Anchor =====

        [Fact]
        public void Anchor_AbsolutePath()
        {
            var p = new Path("/usr/local");
            Assert.Equal("/", p.Anchor);
        }

        [Fact]
        public void Anchor_RelativePath_IsEmpty()
        {
            var p = new Path("relative/path");
            Assert.Equal("", p.Anchor);
        }

        // ===== Equality =====

        [Fact]
        public void Equals_SamePaths()
        {
            Assert.Equal(new Path("/a/b"), new Path("/a/b"));
        }

        [Fact]
        public void Equals_DifferentPaths()
        {
            Assert.NotEqual(new Path("/a/b"), new Path("/a/c"));
        }

        [Fact]
        public void GetHashCode_SameForEqualPaths()
        {
            Assert.Equal(new Path("/a/b").GetHashCode(), new Path("/a/b").GetHashCode());
        }

        // ===== Cwd / Home =====

        [Fact]
        public void Cwd_ReturnsCurrentDirectory()
        {
            var cwd = Path.Cwd();
            Assert.True(cwd.IsAbsolute);
            Assert.True(cwd.IsDir());
        }

        [Fact]
        public void Home_ReturnsHomeDirectory()
        {
            var home = Path.Home();
            Assert.True(home.IsAbsolute);
            Assert.True(home.IsDir());
        }

        // ===== Touch =====

        [Fact]
        public void Touch_CreatesNewFile()
        {
            var path = Sub("touched.txt");
            new Path(path).Touch();
            Assert.True(System.IO.File.Exists(path));
        }

        [Fact]
        public void Touch_ExistingFile_UpdatesTimestamp()
        {
            var path = Sub("touch_existing.txt");
            System.IO.File.WriteAllText(path, "data");
            var before = System.IO.File.GetLastWriteTimeUtc(path);
            System.Threading.Thread.Sleep(50);
            new Path(path).Touch();
            var after = System.IO.File.GetLastWriteTimeUtc(path);
            Assert.True(after >= before);
        }

        [Fact]
        public void Touch_ExistOkFalse_ThrowsOnExisting()
        {
            var path = Sub("touch_exists.txt");
            System.IO.File.WriteAllText(path, "data");
            Assert.Throws<FileExistsError>(() => new Path(path).Touch(existOk: false));
        }

        // ===== Stat =====

        [Fact]
        public void Stat_ReturnsFileInfo()
        {
            var path = Sub("stat_file.txt");
            System.IO.File.WriteAllText(path, "hello");
            var stat = new Path(path).Stat();
            Assert.Equal(5, stat.StSize);
            Assert.True(stat.StMtime > 0);
        }

        [Fact]
        public void Stat_ReturnsDirectoryInfo()
        {
            var stat = new Path(_tempDir).Stat();
            Assert.Equal(0, stat.StSize);
            Assert.True(stat.StMtime > 0);
        }

        [Fact]
        public void Stat_ThrowsOnNonexistent()
        {
            Assert.Throws<FileNotFoundError>(() => new Path(Sub("nonexistent")).Stat());
        }

        // ===== IsSymlink =====

        [Fact]
        public void IsSymlink_FalseForRegularFile()
        {
            var path = Sub("regular.txt");
            System.IO.File.WriteAllText(path, "data");
            Assert.False(new Path(path).IsSymlink());
        }

        [Fact]
        public void IsSymlink_FalseForNonexistent()
        {
            Assert.False(new Path(Sub("nope")).IsSymlink());
        }

        // ===== Rglob =====

        [Fact]
        public void Rglob_FindsFilesRecursively()
        {
            var subDir = Sub("rglob_sub");
            System.IO.Directory.CreateDirectory(subDir);
            System.IO.File.WriteAllText(Sub("top.txt"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(subDir, "nested.txt"), "");

            var matches = new System.Collections.Generic.List<string>();
            foreach (var p in new Path(_tempDir).Rglob("*.txt"))
            {
                matches.Add(p.Name);
            }
            Assert.Contains("top.txt", matches);
            Assert.Contains("nested.txt", matches);
        }

        // ===== Match =====

        [Fact]
        public void Match_MatchesName()
        {
            Assert.True(new Path("/some/path/file.txt").Match("*.txt"));
            Assert.False(new Path("/some/path/file.txt").Match("*.md"));
        }

        [Fact]
        public void Match_ExactName()
        {
            Assert.True(new Path("/some/path/file.txt").Match("file.txt"));
            Assert.False(new Path("/some/path/file.txt").Match("other.txt"));
        }

        // ===== Expanduser =====

        [Fact]
        public void Expanduser_ExpandsTilde()
        {
            var expanded = new Path("~").Expanduser();
            Assert.True(expanded.IsAbsolute);
            Assert.Equal(Path.Home().ToString(), expanded.ToString());
        }

        [Fact]
        public void Expanduser_ExpandsTildeSlash()
        {
            var expanded = new Path("~/docs").Expanduser();
            Assert.True(expanded.IsAbsolute);
            Assert.Contains("docs", expanded.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Expanduser_NoTilde_ReturnsOriginal()
        {
            var p = new Path("/absolute/path");
            var result = p.Expanduser();
            Assert.Equal("/absolute/path", result.ToString());
        }
    }
}
