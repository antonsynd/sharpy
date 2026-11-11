using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Immutable copies of reference assemblies, for <see cref="ReferenceLoadContext"/> to load when a path
/// it already mapped in this process now holds another file (#2114's in-process cell): the runtime would
/// answer the path with the old image, so the current file is loaded from a copy no live context holds.
///
/// <para><b>Where.</b> A per-user directory, <c>&lt;LocalApplicationData&gt;/sharpy/reference-images</c>
/// (<c>$XDG_DATA_HOME</c> or <c>~/.local/share</c> on Linux, <c>~/Library/Application Support</c> on
/// macOS, <c>%LOCALAPPDATA%</c> on Windows). Never a shared, predictable path such as
/// <c>/tmp/&lt;name&gt;</c>, where another local user could create the directory first and plant the
/// file the compiler then loads. On Unix the root and its <c>sharpy/</c> parent are refused when either
/// is a symbolic link, when a pre-existing one grants any access to group or others (it is refused, not
/// narrowed: running as root, a chmod would succeed on another user's directory and adopt it), when the
/// data directory above them is writable by group or others, or when a pre-existing root holds anything
/// but well-formed entries; a directory created here is created owner-only (700). .NET offers no owner
/// check without platform interop, so a pre-existing 700 directory owned by ANOTHER user inside a data
/// directory only that user can write (a root process pointed at another user's data home) is the one
/// arrangement not refused. A refused or unusable root sends the copy to a private temporary directory
/// (<see cref="Directory.CreateTempSubdirectory"/>: an unpredictable name, mode 700 on Unix), one per
/// content per process, deleted at process exit, and reported once through the caller's notice.</para>
///
/// <para><b>What.</b> An entry is <c>&lt;root&gt;/&lt;sha256 of the content&gt;/&lt;file name&gt;</c>:
/// the directory names the bytes, and the file keeps the reference's name so dependency probing finds
/// it. A file found there is loaded only after its content hashes to its directory's name; a mismatch
/// is deleted and copied again. Nothing is trusted by name.</para>
///
/// <para><b>Bounded, without taking a live user's file.</b> Each publish deletes the entries nobody
/// published, reused or read for <see cref="StaleAfterDays"/> days — the newest of the entry's and its
/// file's write and access times — re-reading those times immediately before each delete, and never
/// an entry this process has published (it may have it loaded). Each publish also refreshes every
/// entry this process holds, so a long-lived host that keeps publishing keeps its entries fresh. What
/// remains: a process that published an entry and then published nothing for <see cref="StaleAfterDays"/>
/// days, on a file system that does not update access times on reads of a mapped file, can lose that
/// entry's file to another process's prune. On Unix its mapped image stays valid; what is lost is the
/// file at <c>Assembly.Location</c>, so identities that would hash it fall back to the image's module
/// version id (<see cref="Caching.AssemblyIdentity"/>), and a load that finds the file gone publishes it
/// again (<see cref="ReferenceLoadContext"/>). On Windows a mapped file cannot be deleted and the prune
/// skips it. Publishing happens only when a reference changed under a live process, so neither a build
/// that is up to date nor one whose references are unchanged lists the directory.</para>
/// </summary>
[ThreadSafe]
internal static class ReferenceImageCache
{
    /// <summary>Entries neither published, reused nor read for this many days are pruned.</summary>
    internal const int StaleAfterDays = 14;

    private const string StagingPrefix = ".staging-";

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode GroupOrOtherAccess =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    private const UnixFileMode GroupOrOtherWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    private static readonly Regex EntryName = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Content hash -> the private temporary directory holding this process's fallback copy of those
    /// bytes, so a repeated publish of the same content reuses one copy. Deleted at process exit.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> s_privateImages = new(StringComparer.Ordinal);

    /// <summary>
    /// Entry directories this process has published (and so may have loaded): never pruned by this
    /// process, and refreshed on each of its publishes.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> s_heldEntries = new(StringComparer.Ordinal);

    /// <summary>How many private fallback copies this process holds.</summary>
    internal static int PrivateImageCount => s_privateImages.Count;

    /// <summary>A root used in place of the per-user one by the current async flow (tests only).</summary>
    private static readonly AsyncLocal<string?> s_rootOverride = new();

    static ReferenceImageCache()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeletePrivateImages();
    }

    /// <summary>
    /// Sends this async flow's publishes to <paramref name="root"/> until the returned scope is disposed
    /// — for tests, which must not write into the user's own cache. Not reachable from outside the
    /// compiler, so no user setting can point the cache at a hostile path.
    /// </summary>
    internal static IDisposable OverrideRootForTests(string root)
    {
        var previous = s_rootOverride.Value;
        s_rootOverride.Value = root;
        return new RootOverrideScope(previous);
    }

    private sealed class RootOverrideScope(string? previous) : IDisposable
    {
        public void Dispose() => s_rootOverride.Value = previous;
    }

    /// <summary>
    /// The per-user root, or null when the platform names no per-user data directory.
    /// </summary>
    internal static string? DefaultRoot()
    {
        var baseDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrEmpty(baseDirectory)
            ? null
            : Path.Combine(baseDirectory, "sharpy", "reference-images");
    }

    /// <summary>
    /// A path holding exactly the bytes now at <paramref name="sourcePath"/>, under
    /// <paramref name="root"/> (the per-user root when null), to load in place of the source path.
    /// Falls back to a private temporary directory when the root cannot be used, telling
    /// <paramref name="notice"/> the first time this process does.
    /// </summary>
    internal static string Publish(string sourcePath, string? root = null, Action<string>? notice = null)
    {
        root ??= s_rootOverride.Value ?? DefaultRoot();
        string? refusal = "no per-user data directory";
        if (root != null)
        {
            try
            {
                refusal = TrustRefusal(root);
                if (refusal == null)
                    return PublishUnder(root, sourcePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                refusal = ex.Message;
            }
        }

        return PublishPrivate(sourcePath, root, refusal, notice);
    }

    private static string PublishUnder(string root, string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        RefreshHeldEntries();

        // Copy first and hash the copy: the entry is named by the bytes it holds, whatever happens to the
        // source meanwhile.
        var staging = Path.Combine(root, StagingPrefix + Guid.NewGuid().ToString("N"));
        File.Copy(sourcePath, staging);
        try
        {
            // The copy may carry the source's old write time; a concurrent prune must not take it.
            File.SetLastWriteTimeUtc(staging, DateTime.UtcNow);
            var hash = FileContentIdentity.Sha256Of(staging);
            var entry = Path.Combine(root, hash);
            var copy = Path.Combine(entry, name);
            CreateOwnerOnly(entry);
            Directory.SetLastWriteTimeUtc(entry, DateTime.UtcNow);
            s_heldEntries.TryAdd(Path.GetFullPath(entry), 0);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (File.Exists(copy))
                {
                    if (string.Equals(FileContentIdentity.Sha256Of(copy), hash, StringComparison.Ordinal))
                    {
                        Prune(root, keep: entry);
                        return copy;
                    }

                    // A file that is not the bytes its directory names: never load it.
                    File.Delete(copy);
                }

                try
                {
                    File.Move(staging, copy);
                    Prune(root, keep: entry);
                    return copy;
                }
                catch (IOException) when (File.Exists(copy))
                {
                    // Another publisher got there first; its file is verified on the next pass.
                }
            }

            throw new IOException($"Could not publish a verified copy of '{sourcePath}' under '{entry}'.");
        }
        finally
        {
            TryDelete(staging);
        }
    }

    /// <summary>
    /// A copy in a directory this process created for that content alone: unpredictable, owner-only
    /// on Unix, not shared, reused for the same bytes, and deleted at process exit.
    /// </summary>
    private static string PublishPrivate(string sourcePath, string? root, string? refusal, Action<string>? notice)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var hash = FileContentIdentity.Sha256Of(bytes);
        var name = Path.GetFileName(sourcePath);
        var created = false;
        var directory = s_privateImages.GetOrAdd(hash, _ =>
        {
            created = true;
            return Directory.CreateTempSubdirectory("sharpy-reference-image-").FullName;
        });

        var copy = Path.Combine(directory, name);
        if (!File.Exists(copy) || !string.Equals(FileContentIdentity.Sha256Of(copy), hash, StringComparison.Ordinal))
            File.WriteAllBytes(copy, bytes);

        if (created && s_privateImages.Count == 1)
        {
            notice?.Invoke(
                $"Reference image cache '{root ?? "(none)"}' is not usable ({refusal}); replaced references "
                + "are copied to private temporary directories for this process instead.");
        }
        return copy;
    }

    private static void DeletePrivateImages()
    {
        foreach (var directory in s_privateImages.Values)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: a file still mapped (Windows) stays in the temporary directory.
            }
        }
    }

    /// <summary>
    /// Why <paramref name="root"/> may not hold copies the compiler loads, or null when it may (after
    /// creating what is missing, owner-only). Windows relies on the per-user profile's ACL.
    /// </summary>
    internal static string? TrustRefusal(string root)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(root);
            return null;
        }

        var full = Path.GetFullPath(root);
        var parent = Path.GetDirectoryName(full);
        var dataHome = parent == null ? null : Path.GetDirectoryName(parent);
        if (parent == null || dataHome == null)
            return $"'{full}' has no parent directory";

        if (Directory.Exists(dataHome))
        {
            if ((File.GetUnixFileMode(dataHome) & GroupOrOtherWrite) != 0)
                return $"'{dataHome}' is writable by group or others";
        }

        foreach (var directory in new[] { parent, full })
        {
            var info = new DirectoryInfo(directory);
            if (info.LinkTarget != null)
                return $"'{directory}' is a symbolic link";
            if (info.Exists)
            {
                if ((File.GetUnixFileMode(directory) & GroupOrOtherAccess) != 0)
                    return $"'{directory}' grants access to group or others";
            }
            else
            {
                CreateOwnerOnly(directory);
                if (new DirectoryInfo(directory).LinkTarget != null)
                    return $"'{directory}' is a symbolic link";
            }
        }

        foreach (var entry in SourceGlob.EnumerateArtifactDirectories(full))
        {
            if (!EntryName.IsMatch(Path.GetFileName(entry)))
                return $"'{full}' holds '{Path.GetFileName(entry)}', which is not a cache entry";
        }
        foreach (var file in SourceGlob.EnumerateArtifacts(full, "*"))
        {
            if (!Path.GetFileName(file).StartsWith(StagingPrefix, StringComparison.Ordinal))
                return $"'{full}' holds '{Path.GetFileName(file)}', which is not a cache entry";
        }

        return null;
    }

    private static void CreateOwnerOnly(string directory)
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, OwnerOnly);
    }

    /// <summary>Refreshes every entry this process has published, so another process's prune keeps it.</summary>
    private static void RefreshHeldEntries()
    {
        foreach (var entry in s_heldEntries.Keys)
        {
            try
            {
                if (Directory.Exists(entry))
                    Directory.SetLastWriteTimeUtc(entry, DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Gone or not ours to touch: the next publish of its content recreates it.
            }
        }
    }

    /// <summary>
    /// The newest of an entry directory's write time and its files' write and access times: the last
    /// time anyone published, reused or read it. Not the directory's own access time, which listing it
    /// (this scan) would refresh.
    /// </summary>
    private static DateTime LastUsedUtc(string entry)
    {
        if (!Directory.Exists(entry))
            return Max(File.GetLastWriteTimeUtc(entry), File.GetLastAccessTimeUtc(entry));

        var newest = Directory.GetLastWriteTimeUtc(entry);
        foreach (var file in SourceGlob.EnumerateArtifacts(entry, "*"))
            newest = Max(newest, Max(File.GetLastWriteTimeUtc(file), File.GetLastAccessTimeUtc(file)));
        return newest;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>
    /// Deletes every entry under <paramref name="root"/> (an entry directory or a leftover staging file)
    /// unused for more than <see cref="StaleAfterDays"/> days before <paramref name="nowUtc"/>
    /// (<see cref="LastUsedUtc"/>, read again immediately before the delete), except
    /// <paramref name="keep"/> and every entry this process holds. A delete that fails — the file is
    /// mapped on Windows, or another process removed it first — is ignored.
    /// </summary>
    internal static void Prune(string root, string keep, DateTime? nowUtc = null)
    {
        var cutoff = (nowUtc ?? DateTime.UtcNow).AddDays(-StaleAfterDays);
        var keepFull = Path.GetFullPath(keep);
        List<string> entries;
        try
        {
            entries = SourceGlob.EnumerateArtifactDirectories(root)
                .Concat(SourceGlob.EnumerateArtifacts(root, StagingPrefix + "*"))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var stale = entries.Where(entry =>
        {
            var full = Path.GetFullPath(entry);
            if (string.Equals(full, keepFull, StringComparison.Ordinal) || s_heldEntries.ContainsKey(full))
                return false;
            try
            {
                return LastUsedUtc(entry) < cutoff;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }).ToList();

        foreach (var entry in stale)
        {
            try
            {
                // Read again just before deleting: a publish, reuse or read since the scan keeps it.
                if (LastUsedUtc(entry) >= cutoff)
                    continue;
                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use, or already gone: a later prune gets it.
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A staging file left behind is pruned like a stale entry.
        }
    }
}
