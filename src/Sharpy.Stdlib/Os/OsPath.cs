// Generated from src/Sharpy.Stdlib/spy/os_path_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/os_path_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Common pathname manipulations (os.path equivalent).
    /// </summary>
    public static partial class OsPathModule
    {
        /// <summary>
        /// Join two pathname components, inserting '/' as needed.
        /// </summary>
        public static string Join(string a, string b)
        {
            return global::System.IO.Path.Combine(a, b);
        }

        /// <summary>
        /// Join three pathname components, inserting '/' as needed.
        /// </summary>
        public static string Join(string a, string b, string c)
        {
            return global::System.IO.Path.Combine(a, b, c);
        }

        /// <summary>
        /// Join four pathname components, inserting '/' as needed.
        /// </summary>
        public static string Join(string a, string b, string c, string d)
        {
            return global::System.IO.Path.Combine(a, b, c, d);
        }

        /// <summary>
        /// Normalize a pathname, eliminating double slashes and resolving '.'/'..' references.
        /// </summary>
        public static string Normpath(string path)
        {
            if (path == "")
            {
                return ".";
            }

            string sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar);
            bool isAbsolute = global::Sharpy.StringHelpers.GetItem(path, 0) == "/" || global::Sharpy.StringHelpers.GetItem(path, 0) == "\\" || (path.Length >= 2 && global::Sharpy.StringHelpers.GetItem(path, 1) == ":");
            Sharpy.List<string> stack = new Sharpy.List<string>()
            {
            };
            foreach (var __loopVar_0 in path.Replace("\\", "/").Split("/"))
            {
                var part = __loopVar_0;
                if (part == "" || part == ".")
                {
                    continue;
                }

                if (part == "..")
                {
                    if (global::Sharpy.Builtins.Len(stack) > 0 && stack[global::Sharpy.Builtins.Len(stack) - 1] != "..")
                    {
                        stack.Pop();
                    }
                    else if (!isAbsolute)
                    {
                        stack.Append("..");
                    }
                }
                else
                {
                    stack.Append(part);
                }
            }

            string body = sep.Join(stack);
            if (isAbsolute)
            {
                string prefix = (path.Length >= 2 && global::Sharpy.StringHelpers.GetItem(path, 1) == ":") ? path.Substring(0, 2) + sep : sep;
                return prefix + body;
            }

            if (global::Sharpy.Builtins.Len(stack) > 0)
            {
                return body;
            }

            return ".";
        }

        /// <summary>
        /// Test whether a path exists.
        /// </summary>
        public static bool Exists(string path)
        {
            return global::System.IO.File.Exists(path) || global::System.IO.Directory.Exists(path);
        }

        /// <summary>
        /// Test whether a path is a regular file.
        /// </summary>
        public static bool Isfile(string path)
        {
            return global::System.IO.File.Exists(path);
        }

        /// <summary>
        /// Return true if the pathname refers to an existing directory.
        /// </summary>
        public static bool Isdir(string path)
        {
            return global::System.IO.Directory.Exists(path);
        }

        /// <summary>
        /// Test whether a path is absolute.
        /// </summary>
        public static bool Isabs(string path)
        {
            return global::System.IO.Path.IsPathRooted(path);
        }

        /// <summary>
        /// Return the final component of a pathname.
        /// </summary>
        public static string Basename(string path)
        {
            return global::System.IO.Path.GetFileName(path);
        }

        /// <summary>
        /// Return the directory component of a pathname.
        /// </summary>
        public static string Dirname(string path)
        {
            var result = global::System.IO.Path.GetDirectoryName(path);
            if (result == null)
            {
                return "";
            }

            return result;
        }

        /// <summary>
        /// Split a pathname. Return tuple (head, tail) where tail is everything after the final slash.
        /// </summary>
        public static global::System.ValueTuple<string, string> Split(string path)
        {
            var dirPart = global::System.IO.Path.GetDirectoryName(path);
            if (dirPart == null)
            {
                dirPart = "";
            }

            string namePart = global::System.IO.Path.GetFileName(path);
            return (dirPart, namePart);
        }

        /// <summary>
        /// Split the extension from a pathname.
        /// </summary>
        public static global::System.ValueTuple<string, string> Splitext(string path)
        {
            string ext = global::System.IO.Path.GetExtension(path);
            string root = path;
            if (ext.Length > 0)
            {
                root = path.Substring(0, path.Length - ext.Length);
            }

            return (root, ext);
        }

        /// <summary>
        /// Return an absolute path.
        /// </summary>
        public static string Abspath(string path)
        {
            return global::System.IO.Path.GetFullPath(path);
        }

        /// <summary>
        /// Return the canonical path of the specified filename, eliminating any symbolic links.
        /// </summary>
        public static string Realpath(string path)
        {
            return global::System.IO.Path.GetFullPath(path);
        }

        /// <summary>
        /// Return the size of a file, reported by os.stat().
        /// </summary>
        public static long Getsize(string path)
        {
            if (!global::System.IO.File.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            global::System.IO.FileInfo info = new global::System.IO.FileInfo(path);
            return info.Length;
        }

        /// <summary>
        /// Expand ~ and ~user constructions.
        /// </summary>
        public static string Expanduser(string path)
        {
            if (path.StartsWith("~"))
            {
                string home = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.UserProfile);
                if (path.Length == 1)
                {
                    return home;
                }

                string sepStr = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar);
                string altSepStr = global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar);
                if (global::Sharpy.StringHelpers.GetItem(path, 1) == sepStr || global::Sharpy.StringHelpers.GetItem(path, 1) == altSepStr)
                {
                    return home + path.Substring(1);
                }
            }

            return path;
        }
    }
}
