using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The identity of an input file for the incremental cache's up-to-date keys (#2114): full path,
/// size, write time and the SHA-256 of the content. Size and write time alone are not identity — a
/// file replaced by other bytes of the same size with its write time preserved (<c>cp -p</c>,
/// <c>rsync -t</c>, a restored build cache) would be taken for the old one. Also the compiler's one
/// file hasher (<see cref="Sha256Of(string)"/>).
///
/// <para><b>Memo.</b> An instance memoises each path's identity for its owner's lifetime — one
/// incremental-cache instance, i.e. one check within one build — so a file read by several of that
/// instance's keys is hashed once. Never held by anything that outlives a build: a memo that did would
/// answer a later build from size and write time alone, which is the hole.</para>
///
/// <para><b>Unreadable.</b> A file whose content (or description) cannot be read keeps its disk identity
/// and ends with <see cref="UnreadableMarker"/>; an identity that ends so proves nothing, so a key
/// holding one is never taken as unchanged (<see cref="IsUnreadable"/>, which reads each identity's
/// suffix, not a substring, so a path that merely contains the marker's text is not unreadable).</para>
///
/// <para><b>The runtime's own files.</b> A file whose directory is exactly the running runtime's (the
/// directory CoreLib was loaded from) is identified by its disk identity alone
/// (<see cref="IsRuntimeFile"/>): the runtime's exact version fixes its bytes. In a self-contained
/// publish that directory is also <c>sharpyc</c>'s own, so a user assembly placed beside
/// <c>sharpyc</c> there is identified by disk identity only.</para>
/// </summary>
[ThreadSafe]
internal sealed class FileContentIdentity
{
    /// <summary>The suffix of an identity whose content could not be read.</summary>
    internal const string UnreadableMarker = ":sha256=unreadable";

    private readonly ConcurrentDictionary<string, string> _memo = new(StringComparer.Ordinal);

    /// <summary>
    /// The identity of <paramref name="path"/> (computed on first ask, then memoised by this instance).
    /// </summary>
    public string Of(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path + UnreadableMarker;
        }

        return _memo.GetOrAdd(full, static p => Compute(p));
    }

    /// <summary>
    /// The identity of <paramref name="path"/> now, uncached: the disk identity plus
    /// <c>:sha256=&lt;hex&gt;</c>, or plus <see cref="UnreadableMarker"/> when the content cannot be
    /// read; the bare path when the file does not exist; the disk identity alone for a runtime file.
    /// </summary>
    public static string Compute(string path)
    {
        string disk;
        try
        {
            disk = DiskIdentity(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            return path + UnreadableMarker;
        }

        if (!File.Exists(path) || IsRuntimeFile(path))
            return disk;

        try
        {
            return disk + ":sha256=" + Sha256Of(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return disk + UnreadableMarker;
        }
    }

    /// <summary>
    /// A file's disk identity — full path, length and last write time, or the bare path when it does
    /// not exist.
    /// </summary>
    public static string DiskIdentity(string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            ? info.FullName + ":" + info.Length.ToString(CultureInfo.InvariantCulture) + ":"
              + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)
            : path;
    }

    /// <summary>The SHA-256 of a file's content, lower-case hex. The compiler's one file hasher.</summary>
    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>The SHA-256 of <paramref name="bytes"/>, lower-case hex, as <see cref="Sha256Of(string)"/> spells it.</summary>
    public static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// True when a key holds an identity whose content could not be read: one of its lines (a
    /// <c>key=identity</c> line, or a bare identity) ends with <see cref="UnreadableMarker"/>.
    /// </summary>
    public static bool IsUnreadable(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        foreach (var line in key.Split('\n'))
        {
            if (line.EndsWith(UnreadableMarker, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True for a file whose directory is exactly the running runtime's (the directory CoreLib was
    /// loaded from; an ordinal comparison of the full parent path).
    /// </summary>
    public static bool IsRuntimeFile(string path)
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (string.IsNullOrEmpty(runtimeDirectory))
            return false;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.Equals(directory, runtimeDirectory, StringComparison.Ordinal);
    }
}
