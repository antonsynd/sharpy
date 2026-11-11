using System.Security.Cryptography;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Immutable copies of reference assemblies, for <see cref="ReferenceLoadContext"/> to load when a path
/// it already mapped in this process now holds another file (#2114's in-process cell): the runtime would
/// answer the path with the old image, so the current file is loaded from a copy no live context holds.
///
/// <para><b>Where.</b> A per-user directory, <c>&lt;LocalApplicationData&gt;/sharpy/reference-images</c>
/// (<c>$XDG_DATA_HOME</c> or <c>~/.local/share</c> on Linux, <c>~/Library/Application Support</c> on
/// macOS, <c>%LOCALAPPDATA%</c> on Windows), created owner-only on Unix and refused when another user
/// could write it. Never a shared, predictable path such as <c>/tmp/&lt;name&gt;</c>, where another local
/// user could create the directory first and plant the file the compiler then loads. When the per-user
/// directory cannot be had, each copy goes to a fresh private temporary directory instead
/// (<see cref="Directory.CreateTempSubdirectory"/>: an unpredictable name, mode 700 on Unix).</para>
///
/// <para><b>What.</b> An entry is <c>&lt;root&gt;/&lt;sha256 of the content&gt;/&lt;file name&gt;</c>: the
/// directory names the bytes, and the file keeps the reference's name so dependency probing finds it.
/// A file found there is loaded only after its content hashes to its directory's name; a mismatch is
/// deleted and copied again. Nothing is trusted by name.</para>
///
/// <para><b>Bounded.</b> Each publish deletes the entries nobody published or reused for
/// <see cref="StaleAfterDays"/> days (an entry's directory write time is refreshed on reuse), except the
/// one being returned; an entry another process still holds open is left for a later prune. Publishing
/// happens only when a reference changed under a live process, so neither a build that is up to date nor
/// one whose references are unchanged lists the directory.</para>
/// </summary>
[ThreadSafe]
internal static class ReferenceImageCache
{
    /// <summary>Entries neither published nor reused for this many days are pruned.</summary>
    internal const int StaleAfterDays = 14;

    private const string StagingPrefix = ".staging-";

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode GroupOrOtherAccess =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

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
    /// Falls back to a private temporary directory when the root cannot be used.
    /// </summary>
    internal static string Publish(string sourcePath, string? root = null)
    {
        root ??= DefaultRoot();
        if (root != null)
        {
            try
            {
                return PublishUnder(root, sourcePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The per-user root is missing, read-only, or not this user's alone.
            }
        }

        return PublishPrivate(sourcePath);
    }

    /// <summary>The SHA-256 of a file's content, lower-case hex.</summary>
    internal static string ContentHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string PublishUnder(string root, string sourcePath)
    {
        EnsurePrivateDirectory(root);
        var name = Path.GetFileName(sourcePath);

        // Copy first and hash the copy: the entry is named by the bytes it holds, whatever happens to the
        // source meanwhile.
        var staging = Path.Combine(root, StagingPrefix + Guid.NewGuid().ToString("N"));
        File.Copy(sourcePath, staging);
        try
        {
            // The copy may carry the source's old write time; a concurrent prune must not take it.
            File.SetLastWriteTimeUtc(staging, DateTime.UtcNow);
            var hash = ContentHash(staging);
            var entry = Path.Combine(root, hash);
            var copy = Path.Combine(entry, name);
            EnsurePrivateDirectory(entry);
            Directory.SetLastWriteTimeUtc(entry, DateTime.UtcNow);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (File.Exists(copy))
                {
                    if (string.Equals(ContentHash(copy), hash, StringComparison.Ordinal))
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
    /// A copy in a directory created for this call alone: unpredictable, owner-only on Unix, and not
    /// shared, so it needs no verification and is not pruned.
    /// </summary>
    private static string PublishPrivate(string sourcePath)
    {
        var directory = Directory.CreateTempSubdirectory("sharpy-reference-image-").FullName;
        var copy = Path.Combine(directory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, copy);
        return copy;
    }

    /// <summary>
    /// Creates <paramref name="directory"/> owner-only on Unix, and refuses one that already exists with
    /// access for anyone else and cannot be narrowed (it is not this user's).
    /// </summary>
    private static void EnsurePrivateDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, OwnerOnly);
        if ((File.GetUnixFileMode(directory) & GroupOrOtherAccess) != 0)
            File.SetUnixFileMode(directory, OwnerOnly);
    }

    /// <summary>
    /// Deletes every entry under <paramref name="root"/> (an entry directory or a leftover staging file)
    /// last written more than <see cref="StaleAfterDays"/> days before <paramref name="nowUtc"/>, except
    /// <paramref name="keep"/>. A delete that fails — another process holds the file, or removed it
    /// first — is ignored.
    /// </summary>
    internal static void Prune(string root, string keep, DateTime? nowUtc = null)
    {
        var cutoff = (nowUtc ?? DateTime.UtcNow).AddDays(-StaleAfterDays);
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(root).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (string.Equals(Path.GetFullPath(entry), Path.GetFullPath(keep), StringComparison.Ordinal))
                continue;
            try
            {
                if (Directory.Exists(entry))
                {
                    if (Directory.GetLastWriteTimeUtc(entry) < cutoff)
                        Directory.Delete(entry, recursive: true);
                }
                else if (File.GetLastWriteTimeUtc(entry) < cutoff)
                {
                    File.Delete(entry);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use by another compiler, or already gone: a later prune gets it.
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
