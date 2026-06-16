using System;
using System.Formats.Tar;

namespace Sharpy
{
    /// <summary>
    /// Metadata about a tar archive member.
    /// Equivalent to Python's <c>tarfile.TarInfo</c>.
    /// </summary>
    [SharpyModuleType("tarfile", "TarInfo")]
    public sealed class TarInfo
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public double Mtime { get; set; }
        public int Mode { get; set; }
        public int Type { get; set; }
        public string Linkname { get; set; }
        public int Uid { get; set; }
        public int Gid { get; set; }
        public string Uname { get; set; }
        public string Gname { get; set; }

        public TarInfo(string name = "")
        {
            Name = name;
            Linkname = "";
            Uname = "";
            Gname = "";
        }

        public bool Isfile() => Type == TarfileModule.REGTYPE;
        public bool Isdir() => Type == TarfileModule.DIRTYPE;
        public bool Issym() => Type == TarfileModule.SYMTYPE;
        public bool Islnk() => Type == TarfileModule.LNKTYPE;

        public override string ToString() => "<TarInfo '" + Name + "'>";

        internal static TarInfo FromTarEntry(TarEntry entry)
        {
            var info = new TarInfo
            {
                Name = entry.Name,
                Size = entry.Length,
                Mtime = entry.ModificationTime.ToUnixTimeSeconds(),
                Mode = (int)entry.Mode,
                Linkname = entry.LinkName ?? "",
            };

            switch (entry.EntryType)
            {
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                    info.Type = TarfileModule.REGTYPE;
                    break;
                case TarEntryType.Directory:
                    info.Type = TarfileModule.DIRTYPE;
                    break;
                case TarEntryType.SymbolicLink:
                    info.Type = TarfileModule.SYMTYPE;
                    break;
                case TarEntryType.HardLink:
                    info.Type = TarfileModule.LNKTYPE;
                    break;
                default:
                    info.Type = TarfileModule.REGTYPE;
                    break;
            }

            return info;
        }
    }
}
