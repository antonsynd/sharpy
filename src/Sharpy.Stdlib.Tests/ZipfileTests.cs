using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class ZipfileTests : IDisposable
    {
        private readonly string _tempDir;

        public ZipfileTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_zipfile_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            { Directory.Delete(_tempDir, true); }
            catch { /* best effort */ }
        }

        private string Sub(string name) => System.IO.Path.Combine(_tempDir, name);

        // ===== Constants =====

        [Fact]
        public void ZIP_STORED_IsZero()
        {
            ZipfileModule.ZIP_STORED.Should().Be(0);
        }

        [Fact]
        public void ZIP_DEFLATED_IsEight()
        {
            ZipfileModule.ZIP_DEFLATED.Should().Be(8);
        }

        // ===== is_zipfile =====

        [Fact]
        public void IsZipfile_ValidZip_ReturnsTrue()
        {
            string zipPath = Sub("valid.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("test.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("hello");
            }

            ZipfileModule.IsZipfile(zipPath).Should().BeTrue();
        }

        [Fact]
        public void IsZipfile_NonZipFile_ReturnsFalse()
        {
            string path = Sub("notazip.txt");
            File.WriteAllText(path, "this is not a zip file");

            ZipfileModule.IsZipfile(path).Should().BeFalse();
        }

        [Fact]
        public void IsZipfile_NonexistentFile_ReturnsFalse()
        {
            ZipfileModule.IsZipfile(Sub("nope.zip")).Should().BeFalse();
        }

        // ===== ZipFile reading =====

        [Fact]
        public void ZipFile_Read_Namelist()
        {
            string zipPath = CreateTestZip("a.txt", "b.txt");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            var names = zf.Namelist();
            names.Should().Contain("a.txt");
            names.Should().Contain("b.txt");
            names.Should().HaveCount(2);
        }

        [Fact]
        public void ZipFile_Read_Getinfo()
        {
            string zipPath = CreateTestZipWithContent("file.txt", "Hello, World!");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            var info = zf.Getinfo("file.txt");
            info.Filename.Should().Be("file.txt");
            info.FileSize.Should().Be(13); // "Hello, World!" is 13 bytes
        }

        [Fact]
        public void ZipFile_Read_ReadBytes()
        {
            string content = "Hello, World!";
            string zipPath = CreateTestZipWithContent("file.txt", content);

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            byte[] data = zf.Read("file.txt");
            Encoding.UTF8.GetString(data).Should().Be(content);
        }

        [Fact]
        public void ZipFile_Read_NonexistentEntry_Throws()
        {
            string zipPath = CreateTestZip("a.txt");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            Assert.Throws<KeyNotFoundException>(() => zf.Read("nonexistent.txt"));
        }

        // ===== ZipFile writing =====

        [Fact]
        public void ZipFile_Write_WritesFile()
        {
            string srcFile = Sub("source.txt");
            File.WriteAllText(srcFile, "source content");
            string zipPath = Sub("output.zip");

            using (var zf = new Sharpy.ZipFile(zipPath, "w"))
            {
                zf.Write(srcFile, "archived.txt");
            }

            // Verify
            using var reader = new Sharpy.ZipFile(zipPath, "r");
            var names = reader.Namelist();
            names.Should().Contain("archived.txt");
            byte[] data = reader.Read("archived.txt");
            Encoding.UTF8.GetString(data).Should().Be("source content");
        }

        [Fact]
        public void ZipFile_Writestr_WritesString()
        {
            string zipPath = Sub("str_output.zip");

            using (var zf = new Sharpy.ZipFile(zipPath, "w"))
            {
                zf.Writestr("hello.txt", "Hello, World!");
            }

            // Verify
            using var reader = new Sharpy.ZipFile(zipPath, "r");
            byte[] data = reader.Read("hello.txt");
            Encoding.UTF8.GetString(data).Should().Be("Hello, World!");
        }

        // ===== Extract =====

        [Fact]
        public void ZipFile_Extractall_ExtractsAllFiles()
        {
            string zipPath = CreateTestZipWithContent("file1.txt", "content1");
            string outputDir = Sub("extracted");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            zf.Extractall(outputDir);

            File.Exists(System.IO.Path.Combine(outputDir, "file1.txt")).Should().BeTrue();
            File.ReadAllText(System.IO.Path.Combine(outputDir, "file1.txt")).Should().Be("content1");
        }

        [Fact]
        public void ZipFile_Extract_ExtractsSingleFile()
        {
            string zipPath = CreateTestZip("a.txt", "b.txt");
            string outputDir = Sub("extracted_single");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            string result = zf.Extract("a.txt", outputDir);

            File.Exists(result).Should().BeTrue();
            result.Should().Be(System.IO.Path.Combine(outputDir, "a.txt"));
        }

        // ===== Directory entries =====

        [Fact]
        public void ZipFile_DirectoryEntries_HandledCorrectly()
        {
            string zipPath = Sub("dirs.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("subdir/");
                var entry = archive.CreateEntry("subdir/file.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("nested");
            }

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            var names = zf.Namelist();
            names.Should().Contain("subdir/");
            names.Should().Contain("subdir/file.txt");
        }

        // ===== Unicode filenames =====

        [Fact]
        public void ZipFile_UnicodeFilenames_Supported()
        {
            string zipPath = Sub("unicode.zip");
            string unicodeName = "café.txt";

            using (var zf = new Sharpy.ZipFile(zipPath, "w"))
            {
                zf.Writestr(unicodeName, "unicode content");
            }

            using var reader = new Sharpy.ZipFile(zipPath, "r");
            var names = reader.Namelist();
            names.Should().Contain(unicodeName);
            byte[] data = reader.Read(unicodeName);
            Encoding.UTF8.GetString(data).Should().Be("unicode content");
        }

        // ===== Close / Dispose =====

        [Fact]
        public void ZipFile_UseAfterClose_Throws()
        {
            string zipPath = CreateTestZip("a.txt");

            var zf = new Sharpy.ZipFile(zipPath, "r");
            zf.Close();

            Assert.Throws<InvalidOperationException>(() => zf.Namelist());
        }

        [Fact]
        public void ZipFile_WriteMode_CannotRead()
        {
            string zipPath = Sub("writeonly.zip");
            using var zf = new Sharpy.ZipFile(zipPath, "w");
            Assert.Throws<InvalidOperationException>(() => zf.Read("anything"));
        }

        // ===== ZipInfo metadata =====

        [Fact]
        public void ZipInfo_DateTime_HasValidValues()
        {
            string zipPath = CreateTestZipWithContent("dated.txt", "data");

            using var zf = new Sharpy.ZipFile(zipPath, "r");
            var info = zf.Getinfo("dated.txt");
            info.DateTime.Year.Should().BeGreaterThan(2000);
            info.DateTime.Month.Should().BeInRange(1, 12);
            info.DateTime.Day.Should().BeInRange(1, 31);
        }

        // ===== Helpers =====

        private string CreateTestZip(params string[] fileNames)
        {
            string zipPath = Sub("test_" + Guid.NewGuid().ToString("N") + ".zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var name in fileNames)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write("content of " + name);
            }
            return zipPath;
        }

        private string CreateTestZipWithContent(string fileName, string content)
        {
            string zipPath = Sub("test_" + Guid.NewGuid().ToString("N") + ".zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(fileName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
            return zipPath;
        }
    }
}
