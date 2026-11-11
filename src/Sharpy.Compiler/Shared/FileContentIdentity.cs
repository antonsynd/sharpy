using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The identity of an input file for every key that decides whether something already computed from
/// it may be reused: the incremental cache's facts, the reference load contexts' share key and
/// first-load check, and the Roslyn metadata-reference cache (#2114). One definition: full path,
/// size, write time and the SHA-256 of the content. Size and write time alone are not identity — a
/// file replaced by other bytes of the same size with its write time preserved (<c>cp -p</c>,
/// <c>rsync -t</c>, a restored build cache) would be taken for the old one.
///
/// <para><b>One scope per build.</b> An instance memoises each path's identity for the build that
/// created it, so a file is hashed once per build however many keys read it, and every key of the
/// build agrees on what the file was. An instance is never reused across builds: a memo that outlives
/// a build would answer a later build from size and write time alone, which is the hole.</para>
///
/// <para><b>Unreadable.</b> A file that exists but whose content cannot be read keeps its disk identity
/// and carries <see cref="UnreadableMarker"/>; an identity with the marker proves nothing, so a key
/// holding one is never taken as unchanged (<see cref="IsUnreadable"/>).</para>
///
/// <para><b>The runtime's own files.</b> A file in the running runtime's directory is identified by
/// its disk identity alone (<see cref="IsRuntimeFile"/>): the runtime's exact version fixes its bytes,
/// and hashing the shared framework on every compile would cost tens of milliseconds for nothing.</para>
/// </summary>
[ThreadSafe]
internal sealed class FileContentIdentity
{
    /// <summary>Appended to an identity whose content could not be read.</summary>
    internal const string UnreadableMarker = ":unreadable";

    private readonly ConcurrentDictionary<string, string> _memo = new(StringComparer.Ordinal);

    /// <summary>
    /// The identity of <paramref name="path"/> for this build (computed on first ask, then memoised).
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

    /// <summary>True when an identity (or a key holding identities) carries an unreadable file.</summary>
    public static bool IsUnreadable(string? identityOrKey)
        => identityOrKey != null && identityOrKey.Contains(UnreadableMarker, StringComparison.Ordinal);

    /// <summary>
    /// True for a file in the running runtime's directory (the directory CoreLib was loaded from).
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
