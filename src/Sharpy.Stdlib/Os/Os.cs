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
    /// <summary>
    /// Miscellaneous operating system interfaces.
    /// </summary>
    public static partial class OsModule
    {
        public static string Sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar);
        public static string Linesep = global::System.Environment.NewLine;
        public static string Name = Sep == "\\" ? "nt" : "posix";
        public static string Pathsep = global::Sharpy.Builtins.Str(global::System.IO.Path.PathSeparator);
        public static string Altsep = global::System.IO.Path.AltDirectorySeparatorChar == global::System.IO.Path.DirectorySeparatorChar ? "" : global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar);
        /// <summary>
        /// Remove a file (same as unlink).
        /// </summary>
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

        /// <summary>
        /// Rename a file or directory.
        /// </summary>
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

        /// <summary>
        /// Create a directory.
        /// </summary>
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

        /// <summary>
        /// Super-mkdir; create a leaf directory and all intermediate ones.
        /// </summary>
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

        /// <summary>
        /// Remove a directory.
        /// </summary>
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

        /// <summary>
        /// Return a list containing the names of the entries in the directory.
        /// </summary>
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

        /// <summary>
        /// Return a string representing the current working directory.
        /// </summary>
        public static string Getcwd()
        {
            return global::System.IO.Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Change the current working directory to the specified path.
        /// </summary>
        public static void Chdir(string path)
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
            }

            global::System.IO.Directory.SetCurrentDirectory(path);
        }

        /// <summary>
        /// Get an environment variable, return None if it doesn't exist.
        /// </summary>
        public static string? Getenv(string key)
        {
            var result = global::System.Environment.GetEnvironmentVariable(key);
            if (result == null)
            {
                return default;
            }

            return result;
        }

        /// <summary>
        /// Get an environment variable, return default if it doesn't exist.
        /// </summary>
        public static string Getenv(string key, string @default)
        {
            var result = global::System.Environment.GetEnvironmentVariable(key);
            if (result == null)
            {
                return @default;
            }

            return result;
        }

        /// <summary>
        /// Change or add an environment variable.
        /// </summary>
        public static void Putenv(string key, string value)
        {
            global::System.Environment.SetEnvironmentVariable(key, value);
        }

        /// <summary>
        /// Test whether a path exists.
        /// </summary>
        public static bool PathExists(string path)
        {
            return global::System.IO.File.Exists(path) || global::System.IO.Directory.Exists(path);
        }

        /// <summary>
        /// Perform the equivalent of a stat() system call on the given path.
        /// </summary>
        public static global::Sharpy.StatResult Stat(string path)
        {
            if (global::System.IO.File.Exists(path))
            {
                global::System.IO.FileInfo finfo = new global::System.IO.FileInfo(path);
                global::System.DateTimeOffset fw = new global::System.DateTimeOffset(finfo.LastWriteTimeUtc);
                global::System.DateTimeOffset fc = new global::System.DateTimeOffset(finfo.CreationTimeUtc);
                global::System.DateTimeOffset fa = new global::System.DateTimeOffset(finfo.LastAccessTimeUtc);
                return new global::Sharpy.StatResult(finfo.Length, fw.ToUnixTimeSeconds() + fw.Millisecond / 1000.0d, fc.ToUnixTimeSeconds() + fc.Millisecond / 1000.0d, fa.ToUnixTimeSeconds() + fa.Millisecond / 1000.0d, global::System.Convert.ToInt32(finfo.Attributes));
            }

            if (global::System.IO.Directory.Exists(path))
            {
                global::System.IO.DirectoryInfo dinfo = new global::System.IO.DirectoryInfo(path);
                global::System.DateTimeOffset dw = new global::System.DateTimeOffset(dinfo.LastWriteTimeUtc);
                global::System.DateTimeOffset dc = new global::System.DateTimeOffset(dinfo.CreationTimeUtc);
                global::System.DateTimeOffset da = new global::System.DateTimeOffset(dinfo.LastAccessTimeUtc);
                return new global::Sharpy.StatResult(0, dw.ToUnixTimeSeconds() + dw.Millisecond / 1000.0d, dc.ToUnixTimeSeconds() + dc.Millisecond / 1000.0d, da.ToUnixTimeSeconds() + da.Millisecond / 1000.0d, global::System.Convert.ToInt32(dinfo.Attributes));
            }

            throw new global::Sharpy.FileNotFoundError("No such file or directory: '" + path + "'");
        }

        /// <summary>
        /// Directory tree generator yielding (dirpath, dirnames, filenames) for each directory in the tree rooted at top.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<global::System.ValueTuple<string, Sharpy.List<string>, Sharpy.List<string>>> Walk(string top)
        {
            if (!global::System.IO.Directory.Exists(top))
            {
                yield break;
            }

            Sharpy.List<string> pending = new Sharpy.List<string>()
            {
                top
            };
            while (global::Sharpy.Builtins.Len(pending) > 0)
            {
                string current = pending.Pop();
                Sharpy.List<string> dirnames = new Sharpy.List<string>()
                {
                };
                Sharpy.List<string> filenames = new Sharpy.List<string>()
                {
                };
                foreach (var __loopVar_1 in global::System.IO.Directory.GetDirectories(current))
                {
                    var dirEntry = __loopVar_1;
                    dirnames.Append(global::System.IO.Path.GetFileName(dirEntry));
                }

                foreach (var __loopVar_2 in global::System.IO.Directory.GetFiles(current))
                {
                    var fileEntry = __loopVar_2;
                    filenames.Append(global::System.IO.Path.GetFileName(fileEntry));
                }

                yield return (current, dirnames, filenames);
                int i = global::Sharpy.Builtins.Len(dirnames) - 1;
                while (i >= 0)
                {
                    pending.Append(global::System.IO.Path.Combine(current, dirnames[i]));
                    i = i - 1;
                }
            }
        }
    }
}
