// Generated from src/Sharpy.Stdlib/spy/os_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/os_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class OsModule
    {
        public static string Sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar);
        public static string Linesep = global::System.Environment.NewLine;
        public static string Name = Sep == "\\" ? "nt" : "posix";
        public static string Pathsep = global::Sharpy.Builtins.Str(global::System.IO.Path.PathSeparator);
        public static string Altsep = global::System.IO.Path.AltDirectorySeparatorChar == global::System.IO.Path.DirectorySeparatorChar ? "" : global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar);
        public static void Remove(string path)
        {
            if (!global::System.IO.File.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            if (global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.IsADirectoryError("Is a directory: '" + path + "'");
            }

            try
            {
                global::System.IO.File.Delete(path);
            }
            catch (global::System.UnauthorizedAccessException)
            {
                throw new global::Sharpy.PermissionError("Permission denied: '" + path + "'");
            }
        }

        public static void Rename(string src, string dst)
        {
            if (global::System.IO.File.Exists(src))
            {
                global::System.IO.File.Move(src, dst);
            }
            else if (global::System.IO.Directory.Exists(src))
            {
                global::System.IO.Directory.Move(src, dst);
            }
            else
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + src + "'");
            }
        }

        public static void Mkdir(string path)
        {
            if (global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.FileExistsError("File exists: '" + path + "'");
            }

            var parent = global::System.IO.Path.GetDirectoryName(path);
            if (parent != null)
            {
                if (global::Sharpy.Builtins.Len(parent) > 0 && !global::System.IO.Directory.Exists(parent))
                {
                    throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
                }
            }

            global::System.IO.Directory.CreateDirectory(path);
        }

        public static void Makedirs(string path, bool existOk = false)
        {
            if (global::System.IO.Directory.Exists(path))
            {
                if (!existOk)
                {
                    throw new global::Sharpy.FileExistsError("File exists: '" + path + "'");
                }

                return;
            }

            global::System.IO.Directory.CreateDirectory(path);
        }

        public static void Rmdir(string path)
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            try
            {
                global::System.IO.Directory.Delete(path, false);
            }
            catch (Exception)
            {
                throw new global::Sharpy.IOError("Directory not empty: '" + path + "'");
            }
        }

        public static Sharpy.List<string> Listdir(string path = ".")
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            Sharpy.List<string> result = new Sharpy.List<string>()
            {
            };
            foreach (var __loopVar_0 in global::System.IO.Directory.GetFileSystemEntries(path))
            {
                var entry = __loopVar_0;
                result.Append(global::System.IO.Path.GetFileName(entry));
            }

            return result;
        }

        public static string Getcwd()
        {
            return global::System.IO.Directory.GetCurrentDirectory();
        }

        public static void Chdir(string path)
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            global::System.IO.Directory.SetCurrentDirectory(path);
        }

        public static Optional<string> Getenv(string key)
        {
            var result = global::System.Environment.GetEnvironmentVariable(key);
            if (result == null)
            {
                return Optional<string>.None;
            }

            return result;
        }

        public static void Putenv(string key, string value)
        {
            global::System.Environment.SetEnvironmentVariable(key, value);
        }

        public static bool PathExists(string path)
        {
            return global::System.IO.File.Exists(path) || global::System.IO.Directory.Exists(path);
        }
    }
}
