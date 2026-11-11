using System.Reflection;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// The copies <see cref="ReferenceLoadContext"/> loads a replaced reference from
/// (<see cref="ReferenceImageCache"/>): a per-user, owner-only, content-addressed directory whose files
/// are loaded only after their bytes hash to their entry's name, and whose stale entries are pruned.
/// Before, the copies went to <c>/tmp/sharpy-reference-images/&lt;key&gt;</c>, a predictable path in a
/// world-writable directory, and a file already there was returned by name: another local user could
/// plant the assembly the compiler then loaded, and nothing ever deleted an entry.
/// </summary>
public class ReferenceImageCacheTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ReferenceImageCacheTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_refimages_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    /// <summary>A one-type assembly named <paramref name="assemblyName"/> whose marker says <paramref name="who"/>.</summary>
    private string EmitMarkerAssembly(string directory, string assemblyName, string who)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, assemblyName + ".dll");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText($"public static class Marker {{ public static string Who() => \"{who}\"; }}") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emitted = compilation.Emit(path);
        emitted.Success.Should().BeTrue(string.Join("\n", emitted.Diagnostics));
        return path;
    }

    /// <summary>What the assembly at <paramref name="path"/> says, loaded as the compiler loads a copy.</summary>
    private static string LoadedMarker(string path)
    {
        var context = new AssemblyLoadContext("refimage-probe-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            return (string)assembly.GetType("Marker")!.GetMethod("Who", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// (a) A file planted where the genuine reference's copy belongs — the right directory (the content
    /// hash of the genuine bytes, which anyone can compute) and the right file name — but holding another
    /// assembly is not what gets loaded: the published path holds the genuine bytes and loads the genuine
    /// assembly. Positive control: the planted file is itself a loadable assembly with a different answer,
    /// so loading it by name would have been observed.
    /// </summary>
    [Fact]
    public void APlantedFileWithTheRightNameAndWrongContent_IsNotLoaded()
    {
        var genuine = EmitMarkerAssembly(Path.Combine(_tempDir, "genuine"), "PlantProbe", "genuine");
        var impostor = EmitMarkerAssembly(Path.Combine(_tempDir, "impostor"), "PlantProbe", "planted");
        LoadedMarker(impostor).Should().Be("planted", "positive control: the planted bytes load and answer differently");

        var root = Path.Combine(_tempDir, "cache");
        var entry = Path.Combine(root, ReferenceImageCache.ContentHash(genuine));
        Directory.CreateDirectory(entry);
        var planted = Path.Combine(entry, Path.GetFileName(genuine));
        File.Copy(impostor, planted);

        var published = ReferenceImageCache.Publish(genuine, root);

        published.Should().Be(planted, "the entry is where the content hash says, under the reference's own name");
        File.ReadAllBytes(published).Should().Equal(File.ReadAllBytes(genuine),
            "the planted bytes were replaced, not trusted by name");
        LoadedMarker(published).Should().Be("genuine", "the assembly loaded from the cache is the reference's");
    }

    /// <summary>
    /// A verified entry is reused as it is (same path, same bytes), and the source changing afterwards
    /// publishes a new entry named by the new bytes rather than overwriting the old one.
    /// </summary>
    [Fact]
    public void Entries_AreNamedByContent_AndReusedOnlyForTheSameBytes()
    {
        var root = Path.Combine(_tempDir, "cache");
        var v1 = EmitMarkerAssembly(Path.Combine(_tempDir, "v1"), "ContentProbe", "one");
        var v2 = EmitMarkerAssembly(Path.Combine(_tempDir, "v2"), "ContentProbe", "two");

        var first = ReferenceImageCache.Publish(v1, root);
        var again = ReferenceImageCache.Publish(v1, root);
        var other = ReferenceImageCache.Publish(v2, root);

        again.Should().Be(first);
        Path.GetFileName(Path.GetDirectoryName(first)).Should().Be(ReferenceImageCache.ContentHash(v1));
        other.Should().NotBe(first);
        LoadedMarker(first).Should().Be("one");
        LoadedMarker(other).Should().Be("two");
        Directory.EnumerateFiles(root).Should().BeEmpty("no staging file is left behind");
    }

    /// <summary>
    /// On Unix the cache directories are owner-only, and a pre-existing root that others could reach is
    /// narrowed before anything is published into it.
    /// </summary>
    [SkippableFact]
    public void TheCacheDirectories_AreOwnerOnly_OnUnix()
    {
        Skip.If(OperatingSystem.IsWindows(), "Unix file modes; Windows relies on the per-user profile ACL");
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(root);
        File.SetUnixFileMode(root, (UnixFileMode)Convert.ToInt32("777", 8));

        var published = ReferenceImageCache.Publish(
            EmitMarkerAssembly(Path.Combine(_tempDir, "src"), "ModeProbe", "mode"), root);

        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.GetUnixFileMode(root).Should().Be(ownerOnly, "a root others could write is narrowed");
        File.GetUnixFileMode(Path.GetDirectoryName(published)!).Should().Be(ownerOnly, "each entry is created owner-only");
    }

    /// <summary>
    /// A root that cannot be used (here a file sits where the directory belongs) falls back to a private
    /// temporary directory, never to a guessable shared path, and still yields the genuine bytes.
    /// </summary>
    [Fact]
    public void AnUnusableRoot_FallsBackToAPrivateDirectory()
    {
        var root = Path.Combine(_tempDir, "not-a-directory");
        File.WriteAllText(root, "occupied");
        var genuine = EmitMarkerAssembly(Path.Combine(_tempDir, "src"), "FallbackProbe", "fallback");

        var published = ReferenceImageCache.Publish(genuine, root);
        try
        {
            published.Should().NotStartWith(root);
            Path.GetFileName(Path.GetDirectoryName(published)!).Should().StartWith("sharpy-reference-image-");
            LoadedMarker(published).Should().Be("fallback");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(published)!, recursive: true);
        }
    }

    /// <summary>
    /// (c) Pruning deletes entries (and leftover staging files) untouched for longer than
    /// <see cref="ReferenceImageCache.StaleAfterDays"/>, keeps fresh ones, and never deletes the entry
    /// about to be loaded even when it is as old as a stale one. Publishing prunes: a stale entry is gone
    /// after the next publish.
    /// </summary>
    [Fact]
    public void Pruning_RemovesStaleEntries_KeepsFreshOnes_AndTheOneBeingLoaded()
    {
        var root = Path.Combine(_tempDir, "cache");
        var now = DateTime.UtcNow;
        var old = now.AddDays(-(ReferenceImageCache.StaleAfterDays + 1));

        string Entry(string name, DateTime writeTime)
        {
            var entry = Path.Combine(root, name);
            Directory.CreateDirectory(entry);
            File.WriteAllText(Path.Combine(entry, "X.dll"), name);
            Directory.SetLastWriteTimeUtc(entry, writeTime);
            return entry;
        }

        var stale = Entry("stale", old);
        var fresh = Entry("fresh", now.AddDays(-(ReferenceImageCache.StaleAfterDays - 1)));
        var keep = Entry("keep", old);
        var staging = Path.Combine(root, ".staging-leftover");
        File.WriteAllText(staging, "partial");
        File.SetLastWriteTimeUtc(staging, old);

        ReferenceImageCache.Prune(root, keep, now);

        Directory.Exists(stale).Should().BeFalse("an entry untouched past the limit is pruned");
        File.Exists(staging).Should().BeFalse("a leftover staging file is pruned like an entry");
        Directory.Exists(fresh).Should().BeTrue("an entry inside the limit stays");
        Directory.Exists(keep).Should().BeTrue("the entry about to be loaded is never pruned");

        var staleAgain = Entry("stale-again", old);
        var published = ReferenceImageCache.Publish(
            EmitMarkerAssembly(Path.Combine(_tempDir, "src"), "PruneProbe", "prune"), root);
        Directory.Exists(staleAgain).Should().BeFalse("publishing prunes");
        File.Exists(published).Should().BeTrue();
        Directory.Exists(fresh).Should().BeTrue();
    }

    /// <summary>
    /// The default root is the per-user application-data directory, never the shared temporary one.
    /// </summary>
    [Fact]
    public void TheDefaultRoot_IsPerUser_NotTheSharedTempDirectory()
    {
        var root = ReferenceImageCache.DefaultRoot();
        _output.WriteLine($"default root: {root ?? "(none)"}");
        root.Should().NotBeNull("a test host has a per-user data directory");

        root!.Should().StartWith(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));
        Path.GetFullPath(root).Should().NotStartWith(Path.GetFullPath(Path.GetTempPath()));
    }
}
