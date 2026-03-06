using System;
using System.Text;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Open a file and return a TextFile object.
        /// Python: <c>open(path)</c>
        /// </summary>
        public static TextFile Open(string path)
        {
            return Open(path, "r");
        }

        /// <summary>
        /// Open a file with the given mode and return a TextFile object.
        /// Python: <c>open(path, mode)</c>
        /// </summary>
        public static TextFile Open(string path, string mode)
        {
            return Open(path, mode, "utf-8");
        }

        /// <summary>
        /// Open a file with the given mode and encoding, and return a TextFile object.
        /// Python: <c>open(path, mode, encoding=encoding)</c>
        /// </summary>
        public static TextFile Open(string path, string mode, string encoding)
        {
            if (path == null)
                throw new TypeError("open() argument 'file' cannot be None");
            if (mode == null)
                throw new TypeError("open() argument 'mode' cannot be None");
            if (encoding == null)
                throw new TypeError("open() argument 'encoding' cannot be None");

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
                case "latin-1":
                case "latin1":
                case "iso-8859-1":
                    enc = Encoding.GetEncoding("iso-8859-1");
                    break;
                case "utf-16":
                case "utf16":
                    enc = Encoding.Unicode;
                    break;
                default:
                    try
                    {
                        enc = Encoding.GetEncoding(encoding);
                    }
                    catch (ArgumentException)
                    {
                        throw new ValueError("unknown encoding: " + encoding);
                    }
                    break;
            }

            return new TextFile(path, mode, enc);
        }
    }
}
