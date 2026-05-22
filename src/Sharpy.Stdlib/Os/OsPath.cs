using System;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Path manipulation utilities, similar to Python's os.path module.
    /// Wraps System.IO.Path for cross-platform path operations.
    /// </summary>
    [SharpyModule("os.path")]
    public static class OsPath
    {
        /// <summary>Join two or more path components.</summary>
        /// <param name="a">The first path component.</param>
        /// <param name="b">The second path component.</param>
        /// <returns>The joined path.</returns>
        /// <example>
        /// <code>
        /// os.path.join("/home", "user")    # "/home/user"
        /// os.path.join("a", "b")           # "a/b"
        /// </code>
        /// </example>
        public static string Join(string a, string b)
        {
            return System.IO.Path.Combine(a, b);
        }

        /// <summary>Join multiple path components.</summary>
        public static string Join(string a, string b, string c)
        {
            return System.IO.Path.Combine(a, b, c);
        }

        /// <summary>Join multiple path components.</summary>
        public static string Join(string a, string b, string c, string d)
        {
            return System.IO.Path.Combine(a, b, c, d);
        }

        /// <summary>Test whether a path exists.</summary>
        /// <param name="path">The path to test.</param>
        /// <returns><c>true</c> if the path exists.</returns>
        /// <example>
        /// <code>
        /// os.path.exists("/tmp")         # True
        /// os.path.exists("/no/such")     # False
        /// </code>
        /// </example>
        public static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>Test whether a path is a regular file.</summary>
        public static bool Isfile(string path)
        {
            return File.Exists(path);
        }

        /// <summary>Test whether a path is a directory.</summary>
        public static bool Isdir(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>Test whether a path is absolute.</summary>
        public static bool Isabs(string path)
        {
            return System.IO.Path.IsPathRooted(path);
        }

        /// <summary>Return the base name of pathname path (final component).</summary>
        /// <param name="path">The pathname.</param>
        /// <returns>The final component of the path.</returns>
        /// <example>
        /// <code>
        /// os.path.basename("/home/user/file.txt")    # "file.txt"
        /// os.path.basename("/home/user/")             # ""
        /// </code>
        /// </example>
        public static string Basename(string path)
        {
            return System.IO.Path.GetFileName(path);
        }

        /// <summary>Return the directory name of pathname path.</summary>
        public static string Dirname(string path)
        {
            return System.IO.Path.GetDirectoryName(path) ?? "";
        }

        /// <summary>Split a pathname into (head, tail) where tail is the last component.</summary>
        public static (string head, string tail) Split(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path) ?? "";
            var name = System.IO.Path.GetFileName(path);
            return (dir, name);
        }

        /// <summary>Split the extension from a pathname: (root, ext).</summary>
        public static (string root, string ext) Splitext(string path)
        {
            var ext = System.IO.Path.GetExtension(path);
            var root = path;
            if (ext.Length > 0)
            {
                root = path.Substring(0, path.Length - ext.Length);
            }
            return (root, ext);
        }

        /// <summary>Return an absolute version of the path.</summary>
        public static string Abspath(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>Return the canonical path, resolving symlinks.</summary>
        public static string Realpath(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>Normalize a pathname by collapsing redundant separators and up-level references.</summary>
        public static string Normpath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ".";

            char sep = System.IO.Path.DirectorySeparatorChar;
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.List<string>();
            bool isAbsolute = path[0] == '/' || path[0] == '\\' ||
                              (path.Length >= 2 && path[1] == ':');

            foreach (string part in parts)
            {
                if (part == ".")
                    continue;
                if (part == "..")
                {
                    if (stack.Count > 0 && stack[stack.Count - 1] != "..")
                        stack.RemoveAt(stack.Count - 1);
                    else if (!isAbsolute)
                        stack.Add("..");
                }
                else
                {
                    stack.Add(part);
                }
            }

            string result;
            if (isAbsolute)
            {
                string prefix = (path.Length >= 2 && path[1] == ':')
                    ? path.Substring(0, 2) + sep
                    : sep.ToString();
                result = prefix + string.Join(sep.ToString(), stack);
            }
            else
            {
                result = stack.Count > 0 ? string.Join(sep.ToString(), stack) : ".";
            }

            return result;
        }

        /// <summary>Return the size, in bytes, of a file.</summary>
        public static long Getsize(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            return new FileInfo(path).Length;
        }

        /// <summary>Expand ~ and ~user to the user's home directory.</summary>
        public static string Expanduser(string path)
        {
            if (path.StartsWith("~"))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (path.Length == 1)
                {
                    return home;
                }
                if (path[1] == System.IO.Path.DirectorySeparatorChar || path[1] == System.IO.Path.AltDirectorySeparatorChar)
                {
                    return home + path.Substring(1);
                }
            }
            return path;
        }
    }
}
