using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

/// <summary>
/// The one file identity every reuse key shares (<see cref="FileContentIdentity"/>): content, not size
/// and write time, memoised for one build and never across builds.
/// </summary>
public class FileContentIdentityTests : IDisposable
{
    private readonly string _tempDir;

    public FileContentIdentityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_fileidentity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    /// <summary>Replaces <paramref name="path"/> by <paramref name="bytes"/> of the same size, keeping its write time.</summary>
    private static void ReplaceKeepingSizeAndTime(string path, byte[] bytes)
    {
        var time = File.GetLastWriteTimeUtc(path);
        new FileInfo(path).Length.Should().Be(bytes.Length, "precondition: same size");
        var staged = path + ".staged";
        File.WriteAllBytes(staged, bytes);
        File.SetLastWriteTimeUtc(staged, time);
        File.Move(staged, path, overwrite: true);
    }

    [Fact]
    public void AScope_AnswersOnce_ANewScope_SeesTheNewBytes()
    {
        var path = Path.Combine(_tempDir, "a.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        var build1 = new FileContentIdentity();
        var before = build1.Of(path);
        var disk = FileContentIdentity.DiskIdentity(path);

        ReplaceKeepingSizeAndTime(path, new byte[] { 3, 2, 1 });

        FileContentIdentity.DiskIdentity(path).Should().Be(disk, "control: disk identity cannot see the change");
        build1.Of(path).Should().Be(before, "one build sees one identity per file");
        new FileContentIdentity().Of(path).Should().NotBe(before, "the next build sees the new bytes");
        before.Should().StartWith(disk).And.Contain(":sha256=");
    }

    /// <summary>
    /// The Roslyn metadata-reference cache keys a project or package reference by content: after a
    /// same-size, same-time replacement, the next build's scope gets a new reference (the new metadata),
    /// while one build reuses one. Before, it was keyed by (path, write time), so the rebuild compiled
    /// against the old metadata.
    /// </summary>
    [Fact]
    public void TheMetadataReferenceCache_KeysUserReferencesByContent()
    {
        var path = Path.Combine(_tempDir, "Lib.dll");
        File.Copy(typeof(FileContentIdentityTests).Assembly.Location, path);
        var bytes = File.ReadAllBytes(path);

        var build1 = new FileContentIdentity();
        var first = AssemblyCompiler.GetOrCreateUserReference(path, build1);
        AssemblyCompiler.GetOrCreateUserReference(path, build1).Should().BeSameAs(first, "one build reuses one");

        bytes[^1] ^= 0xFF;
        ReplaceKeepingSizeAndTime(path, bytes);

        AssemblyCompiler.GetOrCreateUserReference(path, new FileContentIdentity()).Should().NotBeSameAs(first,
            "other bytes of the same size and time are another reference");
    }
}
