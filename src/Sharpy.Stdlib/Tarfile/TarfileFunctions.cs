using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    public static partial class TarfileModule
    {
        public static readonly int REGTYPE = 0;
        public static readonly int DIRTYPE = 5;
        public static readonly int SYMTYPE = 2;
        public static readonly int LNKTYPE = 1;

        public static TarFile Open(string name, string mode = "r")
        {
            if (mode == "r" || mode == "w")
            {
                mode = mode + ":";
            }

            int colonIndex = mode.IndexOf(':');
            if (colonIndex < 0)
            {
                throw new ValueError("mode '" + mode + "' is not valid");
            }

            string baseMode = mode.Substring(0, colonIndex);
            if (baseMode != "r" && baseMode != "w")
            {
                throw new ValueError("mode '" + mode + "' is not valid");
            }

            if (baseMode == "r" && mode == "r:")
            {
                // Auto-detect: try gzip first
                if (File.Exists(name))
                {
                    try
                    {
                        using var fs = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                        byte[] magic = new byte[2];
                        if (fs.Read(magic, 0, 2) == 2 && magic[0] == 0x1f && magic[1] == 0x8b)
                        {
                            return new TarFile(name, "r:gz");
                        }
                    }
                    catch
                    {
                        // Fall through to plain tar
                    }
                }
            }

            return new TarFile(name, mode);
        }

        public static bool IsTarfile(string name)
        {
            if (!File.Exists(name))
                return false;

            try
            {
                using var stream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new TarReader(stream, leaveOpen: false);
                return reader.GetNextEntry() != null;
            }
            catch
            {
                try
                {
                    using var stream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var gzStream = new GZipStream(stream, CompressionMode.Decompress);
                    using var reader = new TarReader(gzStream, leaveOpen: false);
                    return reader.GetNextEntry() != null;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
