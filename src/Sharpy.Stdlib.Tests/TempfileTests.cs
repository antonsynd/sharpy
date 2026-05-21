using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests
{
    public class TempfileTests : IDisposable
    {
        private readonly System.Collections.Generic.List<string> _createdPaths = new System.Collections.Generic.List<string>();

        public void Dispose()
        {
            foreach (string path in _createdPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                    // best effort cleanup
                }
            }
        }

        [Fact]
        public void Gettempdir_ReturnsNonEmptyPath()
        {
            string result = Sharpy.Tempfile.Gettempdir();
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Gettempdir_ReturnsValidDirectory()
        {
            string result = Sharpy.Tempfile.Gettempdir();
            Directory.Exists(result).Should().BeTrue();
        }

        [Fact]
        public void Gettempdir_DoesNotEndWithSeparator()
        {
            string result = Sharpy.Tempfile.Gettempdir();
            result.Should().NotEndWith(System.IO.Path.DirectorySeparatorChar.ToString());
        }

        [Fact]
        public void Mkdtemp_CreatesDirectory()
        {
            string dir = Sharpy.Tempfile.Mkdtemp();
            _createdPaths.Add(dir);

            Directory.Exists(dir).Should().BeTrue();
        }

        [Fact]
        public void Mkdtemp_UsesDefaultPrefix()
        {
            string dir = Sharpy.Tempfile.Mkdtemp();
            _createdPaths.Add(dir);

            System.IO.Path.GetFileName(dir).Should().StartWith("tmp");
        }

        [Fact]
        public void Mkdtemp_UsesCustomPrefix()
        {
            string dir = Sharpy.Tempfile.Mkdtemp("myapp_");
            _createdPaths.Add(dir);

            System.IO.Path.GetFileName(dir).Should().StartWith("myapp_");
        }

        [Fact]
        public void Mkdtemp_CreatesUniqueDirectories()
        {
            string dir1 = Sharpy.Tempfile.Mkdtemp();
            string dir2 = Sharpy.Tempfile.Mkdtemp();
            _createdPaths.Add(dir1);
            _createdPaths.Add(dir2);

            dir1.Should().NotBe(dir2);
        }

        [Fact]
        public void Mkstemp_CreatesFile()
        {
            var (fd, path) = Sharpy.Tempfile.Mkstemp();
            _createdPaths.Add(path);

            File.Exists(path).Should().BeTrue();
            fd.Should().Be(0);
        }

        [Fact]
        public void Mkstemp_UsesDefaultPrefix()
        {
            var (_, path) = Sharpy.Tempfile.Mkstemp();
            _createdPaths.Add(path);

            System.IO.Path.GetFileName(path).Should().StartWith("tmp");
        }

        [Fact]
        public void Mkstemp_UsesCustomPrefixAndSuffix()
        {
            var (_, path) = Sharpy.Tempfile.Mkstemp("data_", ".csv");
            _createdPaths.Add(path);

            string fileName = System.IO.Path.GetFileName(path);
            fileName.Should().StartWith("data_");
            fileName.Should().EndWith(".csv");
        }

        [Fact]
        public void Mkstemp_CreatesUniqueFiles()
        {
            var (_, path1) = Sharpy.Tempfile.Mkstemp();
            var (_, path2) = Sharpy.Tempfile.Mkstemp();
            _createdPaths.Add(path1);
            _createdPaths.Add(path2);

            path1.Should().NotBe(path2);
        }
    }
}
