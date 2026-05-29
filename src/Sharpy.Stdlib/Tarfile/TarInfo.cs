using System;

namespace Sharpy
{
    /// <summary>
    /// Represents metadata about a member of a tar archive, similar to Python's tarfile.TarInfo.
    /// </summary>
    [SharpyModuleType("tarfile")]
    public sealed class TarInfo
    {
        /// <summary>Gets the name (path) of the archive member.</summary>
        public string Name { get; }

        /// <summary>Gets the size in bytes.</summary>
        public long Size { get; }

        /// <summary>Gets the last modification time as a Unix timestamp.</summary>
        public double Mtime { get; }

        /// <summary>Gets whether this member is a file.</summary>
        public bool Isfile { get; }

        /// <summary>Gets whether this member is a directory.</summary>
        public bool Isdir { get; }

        /// <summary>Gets whether this member is a symbolic link.</summary>
        public bool Issym { get; }

        /// <summary>Gets the link target name, if this is a link.</summary>
        public string Linkname { get; }

        internal TarInfo(string name, long size, double mtime, bool isfile, bool isdir, bool issym, string linkname)
        {
            Name = name;
            Size = size;
            Mtime = mtime;
            Isfile = isfile;
            Isdir = isdir;
            Issym = issym;
            Linkname = linkname;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            string kind = Isdir ? "dir" : Issym ? "sym" : "file";
            return "<TarInfo '" + Name + "' " + kind + " size=" + Size + ">";
        }
    }
}
