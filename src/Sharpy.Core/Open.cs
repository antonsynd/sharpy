using System;
using System.Text;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Open a file and return a file object.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>A TextFile in read mode with UTF-8 encoding</returns>
        public static TextFile Open(string path)
        {
            return Open(path, "r", "utf-8");
        }

        /// <summary>
        /// Open a file and return a file object.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="mode">File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)</param>
        /// <returns>A TextFile with UTF-8 encoding</returns>
        public static TextFile Open(string path, string mode)
        {
            return Open(path, mode, "utf-8");
        }

        /// <summary>
        /// Open a file and return a file object.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="mode">File mode: "r" (read), "w" (write), "a" (append), "x" (exclusive create)</param>
        /// <param name="encoding">Text encoding name (e.g., "utf-8", "ascii")</param>
        /// <returns>A TextFile with the specified mode and encoding</returns>
        public static TextFile Open(string path, string mode, string encoding)
        {
            if (path is null)
            {
                throw new TypeError("open() argument 'file' must be str, not None");
            }

            Encoding enc;
            switch (encoding.ToLowerInvariant())
            {
                case "utf-8":
                case "utf8":
                    enc = new UTF8Encoding(false);
                    break;
                case "ascii":
                    enc = Encoding.ASCII;
                    break;
                case "utf-16":
                case "utf16":
                case "utf-16-le":
                    enc = Encoding.Unicode;
                    break;
                case "utf-16-be":
                    enc = Encoding.BigEndianUnicode;
                    break;
                case "latin-1":
                case "latin1":
                case "iso-8859-1":
                    enc = Encoding.GetEncoding("iso-8859-1");
                    break;
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }

            try
            {
                return new TextFile(path, mode, enc);
            }
            catch (UnauthorizedAccessException ex) when (!(ex is PermissionError))
            {
                throw new PermissionError("Permission denied: '" + path + "'", ex);
            }
        }
    }
}
