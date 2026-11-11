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
/// file the compiler then loads. A privileged process (<see cref="Environment.IsPrivilegedProcess"/>)
/// never uses it: it could write into a directory another user planted. On Unix the root and its
/// <c>sharpy/</c> parent are refused when either is a symbolic link or a pre-existing one grants any
/// access to group or others (refused, not narrowed: a chmod by a privileged process would adopt another
/// user's directory); every existing directory above them, up to <c>/</c>, is refused when group or
/// others can write it and it is not sticky (ssh's StrictModes rule: whoever can write a directory can
/// rename our 700 directory out of it and put their own in its place); a pre-existing root is refused
/// when it holds a visible file or directory that is not a cache entry (dotfiles such as
/// <c>.DS_Store</c> or an editor's swap file are ignored). A directory created here is created
/// owner-only (700). .NET offers no owner check without platform interop, so what remains is a
/// pre-existing 700 <c>sharpy/</c> or root owned by ANOTHER user inside an ancestor chain only they and
/// we can write (in practice: another user's own data directory pointed at by our
/// <c>XDG_DATA_HOME</c>); a non-privileged compiler cannot write such a directory and falls back. A
/// refused or unusable root sends the copy to a private temporary directory
/// (<see cref="Directory.CreateTempSubdirectory"/>: an unpredictable name, mode 700 on Unix), one per
/// content per process, written by rename, deleted at process exit, and announced once per process
/// (<see cref="TakeFallbackNotice"/>, logged by whichever registry loads a reference next) with what was
/// refused and how to repair it.</para>
///
/// <para><b>What.</b> An entry is <c>&lt;root&gt;/&lt;sha256 of the content&gt;/&lt;file name&gt;</c>:
/// the directory names the bytes, and the file keeps the reference's name so dependency probing finds
/// it. A file found there is loaded only after its content hashes to its directory's name; a mismatch
/// is deleted and copied again. Nothing is trusted by name.</para>
///
/// <para><b>Bounded, without taking a live user's file.</b> Each publish deletes the staging files older
/// than <see cref="StagingStaleAfterDays"/> day and the entries nobody published, reused or read for
/// <see cref="StaleAfterDays"/> days — the newest of the entry's and its
/// file's write and access times — re-reading those times immediately before each delete, and never
/// an entry this process has published (it may have it loaded). Each publish also refreshes every
/// entry this process holds, so a long-lived host that keeps publishing keeps its entries fresh. What
/// remains: a process that published an entry and then published nothing for <see cref="StaleAfterDays"/>
/// days, on a file system that does not update access times on reads of a mapped file, can lose that
/// entry's file to another process's prune. On Unix its mapped image stays valid; what is lost is the
/// file at <c>Assembly.Location</c>; the overload index is keyed by the image's module version id, not
/// that file (<see cref="Caching.AssemblyIdentity"/>), and a load that finds the file gone publishes it
/// again (<see cref="ReferenceLoadContext"/>). On Windows a mapped file cannot be deleted and the prune
/// skips it. Publishing happens only when a reference changed under a live process, so neither a build
/// that is up to date nor one whose references are unchanged lists the directory.</para>
/// </summary>
[ThreadSafe]
internal static class ReferenceImageCache
{
    /// <summary>Entries neither published, reused nor read for this many days are pruned.</summary>
    internal const int StaleAfterDays = 14;

    /// <summary>Staging files (an interrupted publish) older than this many days are pruned.</summary>
    internal const int StagingStaleAfterDays = 1;

    private const string StagingPrefix = ".staging-";

    // s_fallbackNotice's state once the notice was handed out.
    private const string NoticeDelivered = "";

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode GroupOrOtherAccess =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;


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

    /// <summary>
    /// The fallback notice: null until the first fallback, then its text until
    /// <see cref="TakeFallbackNotice"/> hands it out, then <see cref="NoticeDelivered"/> for good.
    /// </summary>
    private static string? s_fallbackNotice;

    /// <summary>True once this process has fallen back to a private copy (and so raised the notice).</summary>
    internal static bool FallbackNoticeRaised => Volatile.Read(ref s_fallbackNotice) != null;

    /// <summary>A root used in place of the per-user one by the current async flow (tests only).</summary>
    private static readonly AsyncLocal<string?> s_rootOverride = new();

    /// <summary>Whether the current async flow counts as privileged (tests only; null: ask the OS).</summary>
    private static readonly AsyncLocal<bool?> s_privilegedOverride = new();

    static ReferenceImageCache()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeletePrivateImages();
    }

    /// <summary>
    /// Sends this async flow's publishes to <paramref name="root"/> until the returned scope is disposed
    /// — for tests, which must not write into the user's own cache. Not reachable from outside the
    /// compiler, so no user setting can point the cache at a hostile path.
    /// </summary>
    internal static IDisposable OverrideRootForTests(string root, bool privileged = false)
    {
        var previous = (s_rootOverride.Value, s_privilegedOverride.Value);
        s_rootOverride.Value = root;
        s_privilegedOverride.Value = privileged;
        return new RootOverrideScope(previous);
    }

    private sealed class RootOverrideScope((string? Root, bool? Privileged) previous) : IDisposable
    {
        public void Dispose()
        {
            s_rootOverride.Value = previous.Root;
            s_privilegedOverride.Value = previous.Privileged;
        }
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
    /// Falls back to a private temporary directory when the root cannot be used; the first fallback of
    /// the process raises the notice <see cref="TakeFallbackNotice"/> hands out.
    /// </summary>
    internal static string Publish(string sourcePath, string? root = null, bool? privileged = null)
    {
        root ??= s_rootOverride.Value ?? DefaultRoot();
        string? refusal = "no per-user data directory";
        if (privileged ?? s_privilegedOverride.Value ?? Environment.IsPrivilegedProcess)
        {
            // A privileged process can write a directory another user owns and planted, so it never
            // shares a cache: every copy is private to the process.
            refusal = "the compiler runs as a privileged process, which keeps no shared cache";
        }
        else if (root != null)
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

        RaiseFallbackNotice(root, refusal);
        return PublishPrivate(sourcePath);
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
    /// on Unix, not shared with other processes, reused for the same bytes, and deleted at process exit.
    /// The file is written beside its final name and renamed into place, so another thread never hashes
    /// or loads a half-written file, and a mapped copy is never rewritten in place.
    /// </summary>
    private static string PublishPrivate(string sourcePath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var hash = FileContentIdentity.Sha256Of(bytes);
        if (!s_privateImages.TryGetValue(hash, out var directory))
        {
            var created = Directory.CreateTempSubdirectory("sharpy-reference-image-").FullName;
            directory = s_privateImages.GetOrAdd(hash, created);
            if (!string.Equals(directory, created, StringComparison.Ordinal))
                TryDeleteDirectory(created);
        }

        var copy = Path.Combine(directory, Path.GetFileName(sourcePath));
        if (File.Exists(copy) && string.Equals(FileContentIdentity.Sha256Of(copy), hash, StringComparison.Ordinal))
            return copy;

        var staging = Path.Combine(directory, StagingPrefix + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(staging, bytes);
        try
        {
            File.Move(staging, copy, overwrite: true);
        }
        finally
        {
            TryDelete(staging);
        }
        return copy;
    }

    /// <summary>Records the fallback notice the first time this process falls back.</summary>
    private static void RaiseFallbackNotice(string? root, string? refusal)
    {
        var text = root == null
            ? "No per-user data directory for the reference image cache; replaced references are copied to "
              + "private temporary directories for this process."
            : $"Reference image cache '{root}' not used: {refusal}. Replaced references are copied to private "
              + "temporary directories for this process instead.";
        Interlocked.CompareExchange(ref s_fallbackNotice, text, null);
    }

    /// <summary>
    /// The fallback notice, exactly once per process: its text the first time it is asked for after a
    /// fallback, null before any fallback and on every later ask.
    /// </summary>
    internal static string? TakeFallbackNotice()
    {
        var pending = Volatile.Read(ref s_fallbackNotice);
        if (pending == null || ReferenceEquals(pending, NoticeDelivered))
            return null;
        return ReferenceEquals(Interlocked.CompareExchange(ref s_fallbackNotice, NoticeDelivered, pending), pending)
            ? pending
            : null;
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

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An empty temporary directory left behind is harmless.
        }
    }

    /// <summary>
    /// Why <paramref name="root"/> may not hold copies the compiler loads, and how to repair it, or null
    /// when it may (after creating what is missing, owner-only). Windows relies on the per-user profile's
    /// ACL.
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

        // Every existing directory above the cache, up to the file-system root, must be writable by its
        // owner alone unless it is sticky (ssh's StrictModes rule): whoever else can write a directory
        // can rename what is in it, our 700 directory included, and put their own in its place.
        for (var ancestor = dataHome; ancestor != null; ancestor = Path.GetDirectoryName(ancestor))
        {
            if (!Directory.Exists(ancestor))
                continue;
            var mode = File.GetUnixFileMode(ancestor);
            if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0 && (mode & UnixFileMode.StickyBit) == 0)
                return $"'{ancestor}' is writable by group or others and not sticky, so they could replace the cache "
                       + $"(repair, if it is yours: chmod go-w '{ancestor}')";
        }

        foreach (var directory in new[] { parent, full })
        {
            var info = new DirectoryInfo(directory);
            if (info.LinkTarget != null)
                return $"'{directory}' is a symbolic link (repair: remove the link; it is recreated as a directory)";
            if (info.Exists)
            {
                if ((File.GetUnixFileMode(directory) & GroupOrOtherAccess) != 0)
                    return $"'{directory}' grants access to group or others (repair, if it is yours: chmod 700 '{directory}')";
            }
            else
            {
                CreateOwnerOnly(directory);
                if (new DirectoryInfo(directory).LinkTarget != null)
                    return $"'{directory}' is a symbolic link (repair: remove the link; it is recreated as a directory)";
            }
        }

        // Dotfiles (.DS_Store, an editor's swap file, staging files) are not judged; anything visible must
        // be an entry directory.
        foreach (var item in SourceGlob.EnumerateArtifactDirectories(full).Concat(SourceGlob.EnumerateArtifacts(full, "*")))
        {
            var name = Path.GetFileName(item);
            if (name.StartsWith('.'))
                continue;
            if (!Directory.Exists(item) || !EntryName.IsMatch(name))
                return $"'{full}' holds '{name}', which is not a cache entry (repair: remove it, or remove '{full}')";
        }

        return null;
    }

    /// <summary>
    /// Creates <paramref name="directory"/> and every missing directory above it owner-only, top down
    /// (<c>Directory.CreateDirectory(path, mode)</c> gives the mode to the last level only; the others
    /// would get the umask's, which under umask 002 is group-writable and so refused on the next run).
    /// </summary>
    private static void CreateOwnerOnly(string directory)
    {
        var missing = new Stack<string>();
        for (var level = Path.GetFullPath(directory); level != null && !Directory.Exists(level); level = Path.GetDirectoryName(level))
            missing.Push(level);
        while (missing.Count > 0)
            CreateOwnerOnlyLevel(missing.Pop());
    }

    private static void CreateOwnerOnlyLevel(string directory)
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
    /// Deletes every entry directory under <paramref name="root"/> unused for more than
    /// <see cref="StaleAfterDays"/> days, and every staging file older than
    /// <see cref="StagingStaleAfterDays"/> day, before <paramref name="nowUtc"/>
    /// (<see cref="LastUsedUtc"/>, read again immediately before the delete), except
    /// <paramref name="keep"/> and every entry this process holds. A delete that fails — the file is
    /// mapped on Windows, or another process removed it first — is ignored.
    /// </summary>
    internal static void Prune(string root, string keep, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var keepFull = Path.GetFullPath(keep);
        List<string> entries;
        try
        {
            entries = SourceGlob.EnumerateArtifactDirectories(root)
                .Where(directory => EntryName.IsMatch(Path.GetFileName(directory)))
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
                return LastUsedUtc(entry) < CutoffFor(entry, now);
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
                if (LastUsedUtc(entry) >= CutoffFor(entry, now))
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

    /// <summary>A staging file is stale after a day, an entry after <see cref="StaleAfterDays"/>.</summary>
    private static DateTime CutoffFor(string entry, DateTime now)
        => now.AddDays(-(Path.GetFileName(entry).StartsWith(StagingPrefix, StringComparison.Ordinal)
            ? StagingStaleAfterDays
            : StaleAfterDays));

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
