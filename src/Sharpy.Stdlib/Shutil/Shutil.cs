// Generated from src/Sharpy.Stdlib/spy/shutil_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/shutil_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// High-level file operations (copy, move, remove trees).
    /// </summary>
    public static partial class ShutilModule
    {
        /// <summary>
        /// Copy data and mode bits ("cp src dst"). Return the file's destination.
        /// </summary>
        public static string Copy(string src, string dst)
        {
            if (!global::System.IO.File.Exists(src))
            {
                throw new global::Sharpy.OSError("No such file: '" + src + "'");
            }

            string destPath = _ResolveDestination(src, dst);
            try
            {
                global::System.IO.File.Copy(src, destPath, true);
            }
            catch (Exception)
            {
                throw new global::Sharpy.OSError("Failed to copy '" + src + "' to '" + dst + "'");
            }

            return destPath;
        }

        /// <summary>
        /// Copy data and metadata. Return the file's destination.
        /// </summary>
        public static string Copy2(string src, string dst)
        {
            if (!global::System.IO.File.Exists(src))
            {
                throw new global::Sharpy.OSError("No such file: '" + src + "'");
            }

            string destPath = _ResolveDestination(src, dst);
            try
            {
                global::System.IO.File.Copy(src, destPath, true);
                global::System.IO.File.SetLastWriteTimeUtc(destPath, global::System.IO.File.GetLastWriteTimeUtc(src));
                global::System.IO.File.SetCreationTimeUtc(destPath, global::System.IO.File.GetCreationTimeUtc(src));
            }
            catch (Exception)
            {
                throw new global::Sharpy.OSError("Failed to copy2 '" + src + "' to '" + dst + "'");
            }

            return destPath;
        }

        /// <summary>
        /// Recursively copy a directory tree and return the destination directory.
        /// </summary>
        public static string Copytree(string src, string dst)
        {
            if (!global::System.IO.Directory.Exists(src))
            {
                throw new global::Sharpy.OSError("No such directory: '" + src + "'");
            }

            try
            {
                _CopyDirectoryRecursive(src, dst);
            }
            catch (Exception)
            {
                throw new global::Sharpy.OSError("Failed to copytree '" + src + "' to '" + dst + "'");
            }

            return dst;
        }

        /// <summary>
        /// Recursively delete a directory tree.
        /// </summary>
        public static void Rmtree(string path)
        {
            if (!global::System.IO.Directory.Exists(path))
            {
                throw new global::Sharpy.OSError("No such directory: '" + path + "'");
            }

            try
            {
                global::System.IO.Directory.Delete(path, true);
            }
            catch (Exception)
            {
                throw new global::Sharpy.OSError("Failed to remove directory tree '" + path + "'");
            }
        }

        /// <summary>
        /// Recursively move a file or directory to another location.
        /// </summary>
        public static string Move(string src, string dst)
        {
            if (global::System.IO.File.Exists(src))
            {
                string destPath = _ResolveDestination(src, dst);
                try
                {
                    global::System.IO.File.Move(src, destPath);
                }
                catch (global::System.IO.IOException)
                {
                    global::System.IO.File.Copy(src, destPath, true);
                    global::System.IO.File.Delete(src);
                }

                return destPath;
            }

            if (global::System.IO.Directory.Exists(src))
            {
                global::System.IO.Directory.Move(src, dst);
                return dst;
            }

            throw new global::Sharpy.OSError("No such file or directory: '" + src + "'");
        }

        /// <summary>
        /// Return the path to an executable which would be run if name were called, or None if not found.
        /// </summary>
        public static string? Which(string name)
        {
            if (name == "")
            {
                return default;
            }

            string sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar);
            string altSep = global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar);
            if (name.Find(sep) >= 0 || name.Find(altSep) >= 0)
            {
                if (global::System.IO.File.Exists(name))
                {
                    return global::System.IO.Path.GetFullPath(name);
                }

                return default;
            }

            var rawPath = global::System.Environment.GetEnvironmentVariable("PATH");
            if (rawPath == null)
            {
                return default;
            }

            string pathEnv = rawPath;
            if (pathEnv.Length == 0)
            {
                return default;
            }

            bool isWindows = sep == "\\";
            Sharpy.List<string> extensions = new Sharpy.List<string>()
            {
                ""
            };
            if (isWindows)
            {
                var rawExt = global::System.Environment.GetEnvironmentVariable("PATHEXT");
                string extValue = rawExt != null ? rawExt : "";
                if (extValue.Length > 0)
                {
                    extensions = extValue.Split(";");
                }
                else
                {
                    extensions = new Sharpy.List<string>()
                    {
                        ".COM",
                        ".EXE",
                        ".BAT",
                        ".CMD"
                    };
                }
            }

            foreach (var __loopVar_0 in pathEnv.Split(global::Sharpy.Builtins.Str(global::System.IO.Path.PathSeparator)))
            {
                var dirPath = __loopVar_0;
                if (dirPath == "")
                {
                    continue;
                }

                foreach (var __loopVar_1 in extensions)
                {
                    var ext = __loopVar_1;
                    string candidate = global::System.IO.Path.Combine(dirPath, name + ext);
                    if (global::System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return default;
        }

        /// <summary>
        /// Return disk usage statistics about the given path as a (total, used, free) tuple.
        /// </summary>
        public static global::System.ValueTuple<long, long, long> DiskUsage(string path)
        {
            global::System.IO.DriveInfo drive = new global::System.IO.DriveInfo(path);
            long total = drive.TotalSize;
            long free = drive.AvailableFreeSpace;
            long used = total - free;
            return (total, used, free);
        }

        /// <summary>
        /// Resolve the final destination path, appending the source filename if dst is a directory.
        /// </summary>
        internal static string _ResolveDestination(string src, string dst)
        {
            if (global::System.IO.Directory.Exists(dst))
            {
                return global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(src));
            }

            return dst;
        }

        /// <summary>
        /// Recursively copy a directory tree from src to dst.
        /// </summary>
        internal static void _CopyDirectoryRecursive(string src, string dst)
        {
            global::System.IO.Directory.CreateDirectory(dst);
            foreach (var __loopVar_2 in global::System.IO.Directory.GetFiles(src))
            {
                var filePath = __loopVar_2;
                string destFile = global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(filePath));
                global::System.IO.File.Copy(filePath, destFile, true);
            }

            foreach (var __loopVar_3 in global::System.IO.Directory.GetDirectories(src))
            {
                var dirPath = __loopVar_3;
                string destDir = global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(dirPath));
                _CopyDirectoryRecursive(dirPath, destDir);
            }
        }
    }
}
