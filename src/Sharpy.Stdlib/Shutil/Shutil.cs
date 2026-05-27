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

        public static string _ResolveDestination(string src, string dst)
        {
            if (global::System.IO.Directory.Exists(dst))
            {
                return global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(src));
            }

            return dst;
        }

        public static void _CopyDirectoryRecursive(string src, string dst)
        {
            global::System.IO.Directory.CreateDirectory(dst);
            foreach (var __loopVar_0 in global::System.IO.Directory.GetFiles(src))
            {
                var filePath = __loopVar_0;
                string destFile = global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(filePath));
                global::System.IO.File.Copy(filePath, destFile, true);
            }

            foreach (var __loopVar_1 in global::System.IO.Directory.GetDirectories(src))
            {
                var dirPath = __loopVar_1;
                string destDir = global::System.IO.Path.Combine(dst, global::System.IO.Path.GetFileName(dirPath));
                _CopyDirectoryRecursive(dirPath, destDir);
            }
        }
    }
}
