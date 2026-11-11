namespace Sharpy
{
    /// <summary>
    /// What a filesystem operation requires of the path(s) it is given — the second key of the one
    /// classifier that maps (path state × requirement) to python's <c>OSError</c> subclass
    /// (plan-638e8b D1, #2476).
    /// </summary>
    /// <remarks>
    /// INTERNAL on purpose (D1 predicate 4): a public Core enum is a bare builtin in every Sharpy
    /// program and reserves its spelling (a user <c>class PathRequirement</c> would be SPY0212); this
    /// enum is read only by Core's helper and, by reflection, by <c>Sharpy.Stdlib.Tests</c>
    /// (<c>InternalsVisibleTo</c>), whose G1 generator enumerates it with <c>Enum.GetValues</c> (R1).
    /// The member list belongs to the code: no plan or test carries a count of it.
    /// </remarks>
    internal enum PathRequirement
    {
        /// <summary>An existing regular file to read (<c>open(p)</c>, <c>Path.read_text</c>).</summary>
        NeedsFileToRead,

        /// <summary>A regular file to write, created when missing, its parent an existing directory (<c>open(p, "w")</c>).</summary>
        NeedsFileToWrite,

        /// <summary>A path that must not exist yet, created as a file (<c>open(p, "x")</c>).</summary>
        CreatesExclusively,

        /// <summary>An existing non-directory entry to unlink (<c>os.remove</c>, <c>Path.unlink</c>).</summary>
        RemovesFile,

        /// <summary>A path that must not exist yet, created as a directory under an existing parent (<c>os.mkdir</c>).</summary>
        CreatesDir,

        /// <summary>An existing, empty directory to remove (<c>os.rmdir</c>).</summary>
        RemovesDir,

        /// <summary>An existing directory to list, walk or enter (<c>os.listdir</c>, <c>os.chdir</c>).</summary>
        NeedsDir,

        /// <summary>An existing entry of any kind (<c>os.stat</c>, <c>os.path.getsize</c>).</summary>
        NeedsAny,

        /// <summary>
        /// The two-path rename (<c>os.rename</c>, <c>os.replace</c>): ONE POSIX call whose verdict is a
        /// function of the PAIR of states, not of each position (D1 predicate 2).
        /// </summary>
        Rename,
    }
}
