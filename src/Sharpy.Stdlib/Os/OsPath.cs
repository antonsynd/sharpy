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
    public static partial class OsPathModule
    {
        public static string Join(string a, string b)
        {
            return global::System.IO.Path.Combine(a, b);
        }

        public static bool Exists(string path)
        {
            return global::System.IO.File.Exists(path) || global::System.IO.Directory.Exists(path);
        }

        public static bool Isfile(string path)
        {
            return global::System.IO.File.Exists(path);
        }

        public static bool Isdir(string path)
        {
            return global::System.IO.Directory.Exists(path);
        }

        public static bool Isabs(string path)
        {
            return global::System.IO.Path.IsPathRooted(path);
        }

        public static string Basename(string path)
        {
            return global::System.IO.Path.GetFileName(path);
        }

        public static string Dirname(string path)
        {
            var result = global::System.IO.Path.GetDirectoryName(path);
            if (result == null)
            {
                return "";
            }

            return result;
        }

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

        public static string Abspath(string path)
        {
            return global::System.IO.Path.GetFullPath(path);
        }

        public static string Realpath(string path)
        {
            return global::System.IO.Path.GetFullPath(path);
        }

        public static long Getsize(string path)
        {
            if (!global::System.IO.File.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            global::System.IO.FileInfo info = new global::System.IO.FileInfo(path);
            return info.Length;
        }

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
