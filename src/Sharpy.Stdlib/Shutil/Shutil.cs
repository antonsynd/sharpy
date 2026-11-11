// Generated from src/Sharpy.Stdlib/spy/shutil_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/shutil_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.ShutilModule
{
    /// <summary>
    /// High-level file operations (copy, move, remove trees).
    /// </summary>
    public static partial class ShutilModuleModule
    {
        /// <summary>
        /// Copy data and mode bits ("cp src dst"). Return the file's destination.
        /// </summary>
        public static string Copy(string src, string dst)
        {
            string destPath = global::Sharpy.ShutilModule.ShutilModuleModule._ResolveDestination(src, dst);
            global::Sharpy.ShutilModule.ShutilModuleModule._Copyfile(src, destPath);
            return destPath;
        }

        /// <summary>
        /// Copy data and metadata. Return the file's destination.
        /// </summary>
        public static string Copy2(string src, string dst)
        {
            string destPath = global::Sharpy.ShutilModule.ShutilModuleModule._ResolveDestination(src, dst);
            global::Sharpy.ShutilModule.ShutilModuleModule._Copyfile(src, destPath);
            try
            {
                global::System.IO.File.SetLastWriteTimeUtc(destPath, global::System.IO.File.GetLastWriteTimeUtc(src));
                global::System.IO.File.SetCreationTimeUtc(destPath, global::System.IO.File.GetCreationTimeUtc(src));
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsAny(e, destPath);
            }

            return destPath;
        }

        /// <summary>
        /// Recursively copy a directory tree and return the destination directory.
        /// </summary>
        public static string Copytree(string src, string dst)
        {
            Sharpy.List<string> entries = new Sharpy.List<string>()
            {
            };
            try
            {
                foreach (var __loopVar_0 in global::System.IO.Directory.GetFileSystemEntries(src))
                {
                    var entry = __loopVar_0;
                    entries.Append(entry);
                }
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsDir(e, src);
            }

            global::Sharpy.ShutilModule.ShutilModuleModule._Makedirs(dst);
            foreach (var __loopVar_1 in entries)
            {
                var srcName = __loopVar_1;
                string dstName = global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(srcName));
                if (global::System.IO.Directory.Exists(srcName))
                {
                    global::Sharpy.ShutilModule.ShutilModuleModule.Copytree(srcName, dstName);
                }
                else
                {
                    global::Sharpy.ShutilModule.ShutilModuleModule.Copy2(srcName, dstName);
                }
            }

            return dst;
        }

        /// <summary>
        /// Recursively delete a directory tree.
        /// </summary>
        public static void Rmtree(string path)
        {
            try
            {
                global::System.IO.Directory.Delete(path, true);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsDir(e, path);
            }
        }

        /// <summary>
        /// Recursively move a file or directory to another location.
        /// </summary>
        public static string Move(string src, string dst)
        {
            string realDst = dst;
            if (global::System.IO.Directory.Exists(dst))
            {
                realDst = global::System.IO.Path.Combine(dst, global::Sharpy.ShutilModule.ShutilModuleModule._Basename(src));
            }

            try
            {
                global::Sharpy.PathErrors.Rename(src, realDst);
            }
            catch (global::Sharpy.OSError)
            {
                if (global::System.IO.Directory.Exists(src))
                {
                    if (global::Sharpy.ShutilModule.ShutilModuleModule._Destinsrc(src, dst))
                    {
                        throw;
                    }

                    global::Sharpy.ShutilModule.ShutilModuleModule.Copytree(src, realDst);
                    global::Sharpy.ShutilModule.ShutilModuleModule.Rmtree(src);
                }
                else
                {
                    global::Sharpy.ShutilModule.ShutilModuleModule.Copy2(src, realDst);
                    global::Sharpy.ShutilModule.ShutilModuleModule._Unlink(src);
                }
            }

            return realDst;
        }

        /// <summary>
        /// Return the path to an executable which would be run if name were called, or None if not found.
        /// </summary>
        public static string? Which(string name)
        {
            if (name == "")
            {
                return null;
            }

            string sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar.ToString());
            string altSep = global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar.ToString());
            if (global::Sharpy.StringExtensions.Find(name, sep) >= 0 || global::Sharpy.StringExtensions.Find(name, altSep) >= 0)
            {
                if (global::System.IO.File.Exists(name))
                {
                    return global::System.IO.Path.GetFullPath(name);
                }

                return null;
            }

            var rawPath = global::System.Environment.GetEnvironmentVariable("PATH");
            if (rawPath is null)
            {
                return null;
            }

            string pathEnv = rawPath!;
            if (pathEnv.Length == 0)
            {
                return null;
            }

            bool isWindows = sep == "\\";
            Sharpy.List<string> extensions = new Sharpy.List<string>()
            {
                ""
            };
            if (isWindows)
            {
                var rawExt = global::System.Environment.GetEnvironmentVariable("PATHEXT");
                string extValue = rawExt is not null ? rawExt! : "";
                if (extValue.Length > 0)
                {
                    extensions = new Sharpy.List<string>(global::Sharpy.StringExtensions.Split(extValue, ";"));
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

            foreach (var __loopVar_2 in global::Sharpy.StringExtensions.Split(pathEnv, global::Sharpy.Builtins.Str(global::System.IO.Path.PathSeparator.ToString())))
            {
                var dirPath = __loopVar_2;
                if (dirPath == "")
                {
                    continue;
                }

                foreach (var __loopVar_3 in extensions)
                {
                    var ext = __loopVar_3;
                    string candidate = global::System.IO.Path.Combine(dirPath, name + ext);
                    if (global::System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Return disk usage statistics about the given path as a (total, used, free) tuple.
        /// </summary>
        public static global::System.ValueTuple<long, long, long> DiskUsage(string path)
        {
            try
            {
                global::System.IO.DriveInfo drive = new global::System.IO.DriveInfo(path);
                long total = drive.TotalSize;
                long free = drive.AvailableFreeSpace;
                long used = total - free;
                return (total, used, free);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsAny(e, path);
            }
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
        /// CPython's copyfile steps: open src to read, then dst to write, then copy the bytes.
        /// </summary>
        internal static void _Copyfile(string src, string dst)
        {
            global::Sharpy.PathErrors.CheckNeedsFileToRead(src);
            global::Sharpy.PathErrors.CheckNeedsFileToWrite(dst);
            try
            {
                global::System.IO.File.Copy(src, dst, true);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsFileToWrite(e, dst);
            }
        }

        /// <summary>
        /// CPython's os.makedirs(path) steps: create a missing parent chain, then mkdir the leaf.
        /// </summary>
        internal static void _Makedirs(string path)
        {
            string name = global::Sharpy.ShutilModule.ShutilModuleModule._StripTrailingSeparators(path);
            string? head = global::System.IO.Path.GetDirectoryName(name);
            if (head is not null)
            {
                if (head!.Length > 0 && !global::System.IO.File.Exists(head!) && !global::System.IO.Directory.Exists(head!))
                {
                    global::Sharpy.ShutilModule.ShutilModuleModule._Makedirs(head!);
                    if (global::System.IO.Path.GetFileName(name) == ".")
                    {
                        return;
                    }
                }
            }

            global::Sharpy.PathErrors.CheckCreatesDir(name);
            try
            {
                global::System.IO.Directory.CreateDirectory(name);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateCreatesDir(e, name);
            }
        }

        /// <summary>
        /// os.unlink(path).
        /// </summary>
        internal static void _Unlink(string path)
        {
            global::Sharpy.PathErrors.CheckRemovesFile(path);
            try
            {
                global::System.IO.File.Delete(path);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateRemovesFile(e, path);
            }
        }

        /// <summary>
        /// The path without trailing separators (python's path.split skips an empty tail); a root stays itself.
        /// </summary>
        internal static string _StripTrailingSeparators(string path)
        {
            string stripped = path.TrimEnd(global::System.IO.Path.DirectorySeparatorChar, global::System.IO.Path.AltDirectorySeparatorChar);
            if (stripped.Length == 0)
            {
                return path;
            }

            return stripped;
        }

        /// <summary>
        /// CPython's shutil._basename: the last component, ignoring trailing separators.
        /// </summary>
        internal static string _Basename(string path)
        {
            return global::System.IO.Path.GetFileName(global::Sharpy.ShutilModule.ShutilModuleModule._StripTrailingSeparators(path));
        }

        /// <summary>
        /// CPython's shutil._destinsrc: whether dst lies inside the directory src.
        /// </summary>
        internal static bool _Destinsrc(string src, string dst)
        {
            string sep = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar.ToString());
            string fullSrc = global::System.IO.Path.GetFullPath(src);
            string fullDst = global::System.IO.Path.GetFullPath(dst);
            if (!fullSrc.EndsWith(sep))
            {
                fullSrc = fullSrc + sep;
            }

            if (!fullDst.EndsWith(sep))
            {
                fullDst = fullDst + sep;
            }

            return fullDst.StartsWith(fullSrc);
        }
    }
}
