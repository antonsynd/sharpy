using System;
using System.IO;

namespace Sharpy
{
    [SharpyModule("os.path")]
    public static partial class OsPath
    {
        /// <summary>
        /// Join one or more path components.
        /// </summary>
        public static string Join(string a, string b)
        {
            return System.IO.Path.Combine(a, b);
        }

        /// <summary>
        /// Join one or more path components.
        /// </summary>
        public static string Join(params string[] parts)
        {
            return System.IO.Path.Combine(parts);
        }

        /// <summary>
        /// Test whether a path exists.
        /// </summary>
        public static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// Test whether a path is a regular file.
        /// </summary>
        public static bool Isfile(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Test whether a path is a directory.
        /// </summary>
        public static bool Isdir(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Test whether a path is absolute.
        /// </summary>
        public static bool Isabs(string path)
        {
            return System.IO.Path.IsPathRooted(path);
        }

        /// <summary>
        /// Return the base name of pathname path.
        /// </summary>
        public static string Basename(string path)
        {
            return System.IO.Path.GetFileName(path);
        }

        /// <summary>
        /// Return the directory name of pathname path.
        /// </summary>
        public static string Dirname(string path)
        {
            return System.IO.Path.GetDirectoryName(path) ?? "";
        }

        /// <summary>
        /// Split the pathname path into a pair (head, tail).
        /// </summary>
        public static (string head, string tail) Split(string path)
        {
            return (System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileName(path));
        }

        /// <summary>
        /// Split the pathname path into a pair (root, ext).
        /// </summary>
        public static (string root, string ext) Splitext(string path)
        {
            string ext = System.IO.Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
                return (path, "");
            string root = path.Substring(0, path.Length - ext.Length);
            return (root, ext);
        }

        /// <summary>
        /// Return a normalized absolutized version of the pathname path.
        /// </summary>
        public static string Abspath(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>
        /// Return the canonical path of the specified filename.
        /// Best effort — no symlink resolution on netstandard2.0.
        /// </summary>
        public static string Realpath(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>
        /// Normalize a pathname by collapsing redundant separators and up-level references.
        /// </summary>
        public static string Normpath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ".";

            // Use GetFullPath for absolute paths, manual normalization for relative
            if (System.IO.Path.IsPathRooted(path))
            {
                return System.IO.Path.GetFullPath(path);
            }

            // For relative paths, normalize manually
            char sep = System.IO.Path.DirectorySeparatorChar;
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.List<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                    continue;
                if (part == ".." && stack.Count > 0 && stack[stack.Count - 1] != "..")
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    stack.Add(part);
                }
            }

            string result = string.Join(sep.ToString(), stack);
            return result.Length == 0 ? "." : result;
        }

        /// <summary>
        /// Return the size, in bytes, of path.
        /// </summary>
        public static long Getsize(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            return new FileInfo(path).Length;
        }

        /// <summary>
        /// Expand ~ and ~user constructions.
        /// </summary>
        public static string Expanduser(string path)
        {
            if (!path.StartsWith("~"))
                return path;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                home = Environment.GetEnvironmentVariable("HOME") ?? path;

            if (path == "~")
                return home;
            if (path.StartsWith("~/") || path.StartsWith("~\\"))
                return home + path.Substring(1);

            // ~user form — not supported, return as-is
            return path;
        }
    }
}
