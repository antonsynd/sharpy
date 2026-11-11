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
            bool autoDetect = (mode == "r");

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

            if (autoDetect)
            {
                // python opens the archive first (`open(name, "rb")`); its refusal is the classifier's.
                using var fs = OpenForRead(name);
                byte[] magic = new byte[2];
                if (fs.Read(magic, 0, 2) == 2 && magic[0] == 0x1f && magic[1] == 0x8b)
                {
                    return new TarFile(name, "r:gz");
                }
            }

            return new TarFile(name, mode);
        }

        /// <summary>Return whether the file is a tar archive (optionally gzip-compressed).</summary>
        /// <remarks>A file that cannot be opened raises its <c>OSError</c>; content that is not a tar archive is <c>False</c>.</remarks>
        public static bool IsTarfile(string name)
        {
            // python's is_tarfile: an OSError opening the file propagates; only content decides False.
            using Stream stream = OpenForRead(name);
            if (HoldsTarEntry(stream))
            {
                return true;
            }
            stream.Position = 0;
            using var gzStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            return HoldsTarEntry(gzStream);
        }

        private static FileStream OpenForRead(string name)
        {
            PathErrors.CheckNeedsFileToRead(name);
            try
            {
                return new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw PathErrors.TranslateNeedsFileToRead(ex, name);
            }
        }

        /// <summary>Whether an already-open stream starts with a tar entry; any content failure is "no" (file CONTENT state, not path state).</summary>
        private static bool HoldsTarEntry(Stream stream)
        {
            try
            {
                using var reader = new TarReader(stream, leaveOpen: true);
                return reader.GetNextEntry() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
