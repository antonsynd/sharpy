using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests
{
    public class TarfileTests : IDisposable
    {
        private readonly string _tempDir;

        public TarfileTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "sharpy_tarfile_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            { Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => Path.Combine(_tempDir, name);

        // ===== Create and read tar archives =====

        [Fact]
        public void Open_CreateAndReadTar()
        {
            string archivePath = Sub("test.tar");
            string filePath = Sub("hello.txt");
            File.WriteAllText(filePath, "Hello, World!");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "hello.txt");
            }

            File.Exists(archivePath).Should().BeTrue();

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var names = tar.Getnames();
                names.Should().Contain("hello.txt");
            }
        }

        // ===== Compressed archives (gz) =====

        [Fact]
        public void Open_CreateAndReadGzTar()
        {
            string archivePath = Sub("test.tar.gz");
            string filePath = Sub("data.txt");
            File.WriteAllText(filePath, "Compressed content");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:gz"))
            {
                tar.Add(filePath, arcname: "data.txt");
            }

            File.Exists(archivePath).Should().BeTrue();

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:gz"))
            {
                var names = tar.Getnames();
                names.Should().Contain("data.txt");
            }
        }

        // ===== extractall =====

        [Fact]
        public void Extractall_ExtractsFilesToDirectory()
        {
            string archivePath = Sub("extract.tar");
            string filePath = Sub("source.txt");
            string extractDir = Sub("output");
            File.WriteAllText(filePath, "Extract me!");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "source.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                tar.Extractall(extractDir);
            }

            File.Exists(Path.Combine(extractDir, "source.txt")).Should().BeTrue();
            File.ReadAllText(Path.Combine(extractDir, "source.txt")).Should().Be("Extract me!");
        }

        // ===== extractfile =====

        [Fact]
        public void Extractfile_ReturnsFileContent()
        {
            string archivePath = Sub("file.tar");
            string filePath = Sub("content.txt");
            File.WriteAllText(filePath, "File content here");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "content.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                byte[]? data = tar.Extractfile("content.txt");
                data.Should().NotBeNull();
                System.Text.Encoding.UTF8.GetString(data!).Should().Be("File content here");
            }
        }

        // ===== getnames and getmembers =====

        [Fact]
        public void Getnames_ReturnsAllMemberNames()
        {
            string archivePath = Sub("names.tar");
            string f1 = Sub("a.txt");
            string f2 = Sub("b.txt");
            File.WriteAllText(f1, "aaa");
            File.WriteAllText(f2, "bbb");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(f1, arcname: "a.txt");
                tar.Add(f2, arcname: "b.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var names = tar.Getnames();
                names.Should().HaveCount(2);
                names.Should().Contain("a.txt");
                names.Should().Contain("b.txt");
            }
        }

        [Fact]
        public void Getmembers_ReturnsTarInfoObjects()
        {
            string archivePath = Sub("members.tar");
            string filePath = Sub("info.txt");
            File.WriteAllText(filePath, "twelve chars");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "info.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var members = tar.Getmembers();
                members.Should().HaveCount(1);
                members[0].Name.Should().Be("info.txt");
                members[0].Isfile.Should().BeTrue();
                members[0].Isdir.Should().BeFalse();
                members[0].Size.Should().Be(12);
            }
        }

        // ===== add files with arcname =====

        [Fact]
        public void Add_WithArcname_UsesArchiveName()
        {
            string archivePath = Sub("arcname.tar");
            string filePath = Sub("original.txt");
            File.WriteAllText(filePath, "renamed");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "renamed.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var names = tar.Getnames();
                names.Should().Contain("renamed.txt");
                names.Should().NotContain("original.txt");
            }
        }

        // ===== is_tarfile =====

        [Fact]
        public void Is_tarfile_ReturnsTrueForValidTar()
        {
            string archivePath = Sub("valid.tar");
            string filePath = Sub("x.txt");
            File.WriteAllText(filePath, "x");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "x.txt");
            }

            Sharpy.TarfileModule.Is_tarfile(archivePath).Should().BeTrue();
        }

        [Fact]
        public void Is_tarfile_ReturnsFalseForNonTar()
        {
            string filePath = Sub("notatar.txt");
            File.WriteAllText(filePath, "This is not a tar file");

            Sharpy.TarfileModule.Is_tarfile(filePath).Should().BeFalse();
        }

        [Fact]
        public void Is_tarfile_ReturnsFalseForNonexistentFile()
        {
            Sharpy.TarfileModule.Is_tarfile(Sub("nonexistent.tar")).Should().BeFalse();
        }

        [Fact]
        public void Is_tarfile_ReturnsTrueForGzTar()
        {
            string archivePath = Sub("valid.tar.gz");
            string filePath = Sub("y.txt");
            File.WriteAllText(filePath, "y");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:gz"))
            {
                tar.Add(filePath, arcname: "y.txt");
            }

            Sharpy.TarfileModule.Is_tarfile(archivePath).Should().BeTrue();
        }

        // ===== TarInfo metadata =====

        [Fact]
        public void TarInfo_HasCorrectMetadata()
        {
            string archivePath = Sub("meta.tar");
            string filePath = Sub("meta.txt");
            File.WriteAllText(filePath, "metadata test content");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "meta.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var members = tar.Getmembers();
                var info = members[0];
                info.Name.Should().Be("meta.txt");
                info.Size.Should().Be(21);
                info.Isfile.Should().BeTrue();
                info.Isdir.Should().BeFalse();
                info.Issym.Should().BeFalse();
                info.Linkname.Should().Be("");
                info.Mtime.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void TarInfo_ToString_ContainsName()
        {
            var info = new Sharpy.TarInfo("test.txt", 100, 1700000000, true, false, false, "");
            info.ToString().Should().Contain("test.txt");
            info.ToString().Should().Contain("file");
        }

        // ===== Large file handling =====

        [Fact]
        public void Add_LargeFile_HandledCorrectly()
        {
            string archivePath = Sub("large.tar");
            string filePath = Sub("large.bin");
            // Create a 1MB file
            var data = new byte[1024 * 1024];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(filePath, data);

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "large.bin");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r:"))
            {
                var members = tar.Getmembers();
                members.Should().HaveCount(1);
                members[0].Size.Should().Be(1024 * 1024);

                byte[]? extracted = tar.Extractfile("large.bin");
                extracted.Should().NotBeNull();
                extracted!.Length.Should().Be(1024 * 1024);
            }
        }

        // ===== Error cases =====

        [Fact]
        public void Open_InvalidMode_ThrowsValueError()
        {
            Action act = () => Sharpy.TarfileModule.Open(Sub("bad.tar"), "x:");
            act.Should().Throw<Sharpy.ValueError>();
        }

        [Fact]
        public void Close_PreventsFurtherOperations()
        {
            string archivePath = Sub("closed.tar");
            string filePath = Sub("c.txt");
            File.WriteAllText(filePath, "c");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w:"))
            {
                tar.Add(filePath, arcname: "c.txt");
            }

            var readTar = Sharpy.TarfileModule.Open(archivePath, "r:");
            readTar.Close();

            Action act = () => readTar.Getnames();
            act.Should().Throw<Sharpy.ValueError>();
        }

        // ===== Mode normalization =====

        [Fact]
        public void Open_ShortMode_WorksCorrectly()
        {
            string archivePath = Sub("short.tar");
            string filePath = Sub("s.txt");
            File.WriteAllText(filePath, "short");

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "w"))
            {
                tar.Add(filePath, arcname: "s.txt");
            }

            using (var tar = Sharpy.TarfileModule.Open(archivePath, "r"))
            {
                var names = tar.Getnames();
                names.Should().Contain("s.txt");
            }
        }
    }
}
