using System.Reflection;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using AssemblyIdentity = Sharpy.Compiler.Discovery.Caching.AssemblyIdentity;
using Sharpy.Compiler.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// The copies <see cref="ReferenceLoadContext"/> loads a replaced reference from
/// (<see cref="ReferenceImageCache"/>): a per-user, owner-only, content-addressed directory whose files
/// are loaded only after their bytes hash to their entry's name, that refuses a root it cannot trust,
/// and whose stale entries are pruned without taking one a live process holds. Before, the copies went
/// to <c>/tmp/sharpy-reference-images/&lt;key&gt;</c>, a predictable path in a world-writable directory,
/// and a file already there was returned by name; nothing ever deleted an entry.
/// </summary>
public class ReferenceImageCacheTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ReferenceImageCacheTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_refimages_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_tempDir, OwnerOnly);
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

    /// <summary>A trusted root laid out as the per-user one is: &lt;data home&gt;/sharpy/reference-images.</summary>
    private string Root => Path.Combine(_tempDir, "sharpy", "reference-images");

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

    private string Source(string name, string who) => EmitMarkerAssembly(Path.Combine(_tempDir, "src_" + name), name, who);

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

    private static bool IsUnder(string path, string directory)
        => Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

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

        ReferenceImageCache.TrustRefusal(Root).Should().BeNull("precondition: the root is trusted");
        var entry = Path.Combine(Root, FileContentIdentity.Sha256Of(genuine));
        Directory.CreateDirectory(entry);
        var planted = Path.Combine(entry, Path.GetFileName(genuine));
        File.Copy(impostor, planted);

        var published = ReferenceImageCache.Publish(genuine, Root);

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
        var v1 = EmitMarkerAssembly(Path.Combine(_tempDir, "v1"), "ContentProbe", "one");
        var v2 = EmitMarkerAssembly(Path.Combine(_tempDir, "v2"), "ContentProbe", "two");

        var first = ReferenceImageCache.Publish(v1, Root);
        var again = ReferenceImageCache.Publish(v1, Root);
        var other = ReferenceImageCache.Publish(v2, Root);

        IsUnder(first, Root).Should().BeTrue("non-vacuity: the trusted root is used, not the fallback");
        again.Should().Be(first);
        Path.GetFileName(Path.GetDirectoryName(first)).Should().Be(FileContentIdentity.Sha256Of(v1));
        other.Should().NotBe(first);
        LoadedMarker(first).Should().Be("one");
        LoadedMarker(other).Should().Be("two");
        Directory.EnumerateFiles(Root).Should().BeEmpty("no staging file is left behind");
    }

    /// <summary>
    /// On Unix a root that does not exist is created owner-only, with its <c>sharpy/</c> parent.
    /// </summary>
    [SkippableFact]
    public void ACreatedRoot_AndItsParent_AreOwnerOnly()
    {
        Skip.If(OperatingSystem.IsWindows(), "Unix file modes; Windows relies on the per-user profile ACL");
        if (OperatingSystem.IsWindows())
            return;

        var published = ReferenceImageCache.Publish(Source("ModeProbe", "mode"), Root);

        IsUnder(published, Root).Should().BeTrue("non-vacuity: the root was used");
        File.GetUnixFileMode(Root).Should().Be(OwnerOnly);
        File.GetUnixFileMode(Path.GetDirectoryName(Root)!).Should().Be(OwnerOnly, "the sharpy/ parent too");
        File.GetUnixFileMode(Path.GetDirectoryName(published)!).Should().Be(OwnerOnly, "each entry");
    }

    public static IEnumerable<object[]> UntrustedRoots => new[]
    {
        new object[] { "root_wide_mode" },
        new object[] { "parent_wide_mode" },
        new object[] { "root_symlink" },
        new object[] { "parent_symlink" },
        new object[] { "data_home_writable" },
        new object[] { "foreign_entry" },
    };

    /// <summary>
    /// A root that cannot be trusted is refused, never adopted or narrowed (running as root, a chmod
    /// would succeed on another user's directory): a pre-existing root or <c>sharpy/</c> parent with any
    /// access for group or others, either one a symbolic link, a data directory others can write, or a
    /// root holding something that is not a cache entry. The copy then goes to a private directory and
    /// still holds the genuine bytes; the refused directory's mode is left as it was.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(UntrustedRoots))]
    public void AnUntrustedRoot_IsRefused_AndTheCopyIsPrivate(string arrangement)
    {
        Skip.If(OperatingSystem.IsWindows(), "Unix modes and links; Windows relies on the per-user profile ACL");
        if (OperatingSystem.IsWindows())
            return;

        var parent = Path.GetDirectoryName(Root)!;
        var wide = (UnixFileMode)Convert.ToInt32("777", 8);
        switch (arrangement)
        {
            case "root_wide_mode":
                Directory.CreateDirectory(Root);
                File.SetUnixFileMode(Root, wide);
                break;
            case "parent_wide_mode":
                Directory.CreateDirectory(parent);
                File.SetUnixFileMode(parent, wide);
                break;
            case "root_symlink":
                Directory.CreateDirectory(parent, OwnerOnly);
                var elsewhere = Path.Combine(_tempDir, "elsewhere");
                Directory.CreateDirectory(elsewhere, OwnerOnly);
                Directory.CreateSymbolicLink(Root, elsewhere);
                break;
            case "parent_symlink":
                var otherParent = Path.Combine(_tempDir, "other-parent");
                Directory.CreateDirectory(otherParent, OwnerOnly);
                Directory.CreateSymbolicLink(parent, otherParent);
                break;
            case "data_home_writable":
                File.SetUnixFileMode(_tempDir, wide);
                break;
            case "foreign_entry":
                Directory.CreateDirectory(Root, OwnerOnly);
                Directory.CreateDirectory(parent, OwnerOnly);
                File.SetUnixFileMode(parent, OwnerOnly);
                File.WriteAllText(Path.Combine(Root, "Planted.dll"), "not an entry");
                break;
        }

        var modeBefore = Directory.Exists(Root) ? File.GetUnixFileMode(Root) : (UnixFileMode?)null;
        ReferenceImageCache.TrustRefusal(Root).Should().NotBeNull($"[{arrangement}] the root is refused");

        var published = ReferenceImageCache.Publish(Source("Untrusted" + arrangement.Replace("_", ""), arrangement), Root);

        IsUnder(published, Root).Should().BeFalse($"[{arrangement}] nothing is published under a refused root");
        Path.GetFileName(Path.GetDirectoryName(published)!).Should().StartWith("sharpy-reference-image-");
        LoadedMarker(published).Should().Be(arrangement);
        if (modeBefore is { } mode && new DirectoryInfo(Root).LinkTarget == null)
            File.GetUnixFileMode(Root).Should().Be(mode, $"[{arrangement}] a refused root is not narrowed");
    }

    /// <summary>
    /// What is refused is only what can be abused: a dotfile in the root (<c>.DS_Store</c>, an editor's
    /// swap file) and a data directory only the user's group can write (a 775 <c>~/.local/share</c> under
    /// umask 002 on private-group distributions) do not refuse the cache. Positive controls: the
    /// untrusted-root theory above (a visible foreign file, a data directory every user can write).
    /// </summary>
    [SkippableFact]
    public void ADotfileInTheRoot_AndAGroupWritableDataHome_AreNotRefused()
    {
        Skip.If(OperatingSystem.IsWindows(), "Unix modes; Windows relies on the per-user profile ACL");
        if (OperatingSystem.IsWindows())
            return;

        // CreateDirectory(path, mode) applies the mode to the leaf only, so each level is created on its own.
        Directory.CreateDirectory(Path.GetDirectoryName(Root)!, OwnerOnly);
        Directory.CreateDirectory(Root, OwnerOnly);
        File.WriteAllText(Path.Combine(Root, ".DS_Store"), "finder");
        File.WriteAllText(Path.Combine(Root, ".swp"), "editor");
        File.SetUnixFileMode(_tempDir, (UnixFileMode)Convert.ToInt32("775", 8));

        ReferenceImageCache.TrustRefusal(Root).Should().BeNull();
        IsUnder(ReferenceImageCache.Publish(Source("TolerantProbe", "tolerant"), Root), Root).Should().BeTrue();
        File.Exists(Path.Combine(Root, ".DS_Store")).Should().BeTrue("a dotfile is neither judged nor pruned");
    }

    /// <summary>
    /// The private fallback keeps one copy per content for the process (a repeated publish reuses it
    /// rather than leaking another temporary directory), and its notice is handed out at most once per
    /// process, after which every ask answers null.
    /// </summary>
    [Fact]
    public void TheFallback_ReusesOneCopyPerContent_AndItsNoticeIsHandedOutOnce()
    {
        var unusable = Path.Combine(_tempDir, "not-a-directory");
        File.WriteAllText(unusable, "occupied");
        var source = Source("FallbackProbe", "fallback");

        var first = ReferenceImageCache.Publish(source, unusable);
        var again = ReferenceImageCache.Publish(source, unusable);

        first.Should().NotStartWith(unusable);
        again.Should().Be(first, "the same content reuses its private copy");
        LoadedMarker(first).Should().Be("fallback");
        Directory.EnumerateFiles(Path.GetDirectoryName(first)!).Should().ContainSingle("no staging file is left");

        ReferenceImageCache.FallbackNoticeRaised.Should().BeTrue("a fallback raises the notice");
        var notices = new[] { ReferenceImageCache.TakeFallbackNotice(), ReferenceImageCache.TakeFallbackNotice() };
        _output.WriteLine(string.Join("\n", notices.Where(n => n != null)));
        notices[1].Should().BeNull("the notice is handed out once");
        if (notices[0] is { } text)
            text.Should().Contain("not used").And.Contain("Replaced references are copied");
    }

    /// <summary>
    /// Concurrent fallback publishes of one content all get the same complete copy: the file is renamed
    /// into place, so no thread hashes or loads a half-written one.
    /// </summary>
    [Fact]
    public void ConcurrentFallbackPublishes_AllSeeOneCompleteCopy()
    {
        var unusable = Path.Combine(_tempDir, "not-a-directory");
        File.WriteAllText(unusable, "occupied");
        var source = Source("ConcurrentProbe", "concurrent");
        var expected = File.ReadAllBytes(source);

        var paths = Enumerable.Range(0, 16).AsParallel().WithDegreeOfParallelism(8)
            .Select(_ => ReferenceImageCache.Publish(source, unusable)).ToList();

        paths.Distinct().Should().ContainSingle();
        File.ReadAllBytes(paths[0]).Should().Equal(expected);
    }

    /// <summary>
    /// (c) Pruning deletes entries (and leftover staging files) unused for longer than
    /// <see cref="ReferenceImageCache.StaleAfterDays"/> — by the newest of write and access times —
    /// keeps fresh ones, never deletes the entry about to be loaded, and never deletes one this process
    /// published (it may have it loaded), even when it is as old as a stale one.
    /// </summary>
    [Fact]
    public void Pruning_RemovesStaleEntries_KeepsFreshOnes_TheOneBeingLoaded_AndHeldOnes()
    {
        ReferenceImageCache.TrustRefusal(Root).Should().BeNull("precondition: the root is trusted");
        var now = DateTime.UtcNow;
        var old = now.AddDays(-(ReferenceImageCache.StaleAfterDays + 1));

        // Published first: publishing prunes too, and the arranged entries must meet only the prune below.
        var held = ReferenceImageCache.Publish(Source("HeldProbe", "held"), Root);
        var heldEntry = Path.GetDirectoryName(held)!;
        IsUnder(held, Root).Should().BeTrue("non-vacuity: the trusted root is used");
        Directory.SetLastWriteTimeUtc(heldEntry, old);
        File.SetLastWriteTimeUtc(held, old);
        File.SetLastAccessTimeUtc(held, old);

        string Entry(string name, DateTime writeTime, DateTime? accessTime = null)
        {
            var entry = Path.Combine(Root, name);
            Directory.CreateDirectory(entry);
            var file = Path.Combine(entry, "X.dll");
            File.WriteAllText(file, name);
            File.SetLastWriteTimeUtc(file, writeTime);
            File.SetLastAccessTimeUtc(file, accessTime ?? writeTime);
            Directory.SetLastWriteTimeUtc(entry, writeTime);
            return entry;
        }

        var stale = Entry(new string('a', 64), old);
        var fresh = Entry(new string('b', 64), now.AddDays(-(ReferenceImageCache.StaleAfterDays - 1)));
        var recentlyRead = Entry(new string('c', 64), old, accessTime: now.AddDays(-1));
        var keep = Entry(new string('d', 64), old);
        string Staging(string name, DateTime time)
        {
            var path = Path.Combine(Root, ".staging-" + name);
            File.WriteAllText(path, "partial");
            File.SetLastWriteTimeUtc(path, time);
            File.SetLastAccessTimeUtc(path, time);
            return path;
        }
        var staging = Staging("two-days", now.AddDays(-2));
        var activeStaging = Staging("an-hour", now.AddHours(-1));

        ReferenceImageCache.Prune(Root, keep, now);

        Directory.Exists(stale).Should().BeFalse("an entry unused past the limit is pruned");
        File.Exists(staging).Should().BeFalse("a staging file older than a day is an interrupted publish");
        File.Exists(activeStaging).Should().BeTrue("a recent staging file may be a publish in progress");
        Directory.Exists(fresh).Should().BeTrue("an entry inside the limit stays");
        Directory.Exists(recentlyRead).Should().BeTrue("an entry read inside the limit stays");
        Directory.Exists(keep).Should().BeTrue("the entry about to be loaded is never pruned");
        File.Exists(held).Should().BeTrue("an entry this process published (and may have loaded) is never pruned by it");
    }

    /// <summary>
    /// The overload index's identity of an assembly is the LOADED image's (its module version id), not
    /// the file now at its location: a file replaced after the load would file the old image's index
    /// under the new bytes (#2114), and a file gone (a cache copy pruned while mapped) gave an empty hash
    /// and the colliding key <c>{name}-{version}-no-hash</c>.
    /// </summary>
    [Fact]
    public void TheIndexIdentity_IsTheLoadedImage_NotTheFileNowAtItsLocation()
    {
        var v1 = Source("ImageProbe", "one");
        var v2 = EmitMarkerAssembly(Path.Combine(_tempDir, "v2"), "ImageProbe", "two");
        var copy = Path.Combine(_tempDir, "image-copy", "ImageProbe.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(v1, copy);
        var context = new AssemblyLoadContext("refimage-image-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(copy);
            var loaded = assembly.ManifestModule.ModuleVersionId.ToString("N");
            AssemblyIdentity.FromAssembly(assembly).ContentHash.Should().Be(loaded);

            File.Move(v2, copy, overwrite: true);
            AssemblyIdentity.FromAssembly(assembly).ContentHash.Should().Be(loaded,
                "replaced after the load: still the loaded image, not the new file");

            File.Delete(copy);
            var identity = AssemblyIdentity.FromAssembly(assembly);
            identity.ContentHash.Should().Be(loaded, "gone: still the loaded image");
            identity.ToCacheKey().Should().NotContain("no-hash");
        }
        finally
        {
            context.Unload();
        }
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
