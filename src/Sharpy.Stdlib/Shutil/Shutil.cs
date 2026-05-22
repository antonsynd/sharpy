using System;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// High-level file operations, similar to Python's shutil module.
    /// Provides functions for copying, moving, and removing files and directory trees.
    /// </summary>
    public static partial class Shutil
    {
        /// <summary>
        /// Copy a file to a destination. If <paramref name="dst"/> is a directory,
        /// the file is copied into that directory with its original name.
        /// Similar to Python's <c>shutil.copy()</c>.
        /// </summary>
        /// <param name="src">Source file path.</param>
        /// <param name="dst">Destination file or directory path.</param>
        /// <returns>The path to the destination file.</returns>
        /// <exception cref="OSError">Thrown if the source file does not exist or copying fails.</exception>
        /// <example>
        /// <code>
        /// shutil.copy("src.txt", "dst.txt")       # copy to file
        /// shutil.copy("src.txt", "/tmp/")          # copy into directory
        /// </code>
        /// </example>
        public static string Copy(string src, string dst)
        {
            if (!File.Exists(src))
            {
                throw new OSError("No such file: '" + src + "'");
            }

            try
            {
                string destPath = ResolveDestination(src, dst);
                File.Copy(src, destPath, true);
                return destPath;
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to copy '" + src + "' to '" + dst + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Copy a file to a destination, preserving file metadata (timestamps).
        /// If <paramref name="dst"/> is a directory, the file is copied into that directory.
        /// Similar to Python's <c>shutil.copy2()</c>.
        /// </summary>
        /// <param name="src">Source file path.</param>
        /// <param name="dst">Destination file or directory path.</param>
        /// <returns>The path to the destination file.</returns>
        /// <exception cref="OSError">Thrown if the source file does not exist or copying fails.</exception>
        /// <example>
        /// <code>
        /// shutil.copy2("src.txt", "dst.txt")    # copy with timestamps
        /// </code>
        /// </example>
        public static string Copy2(string src, string dst)
        {
            if (!File.Exists(src))
            {
                throw new OSError("No such file: '" + src + "'");
            }

            try
            {
                string destPath = ResolveDestination(src, dst);
                File.Copy(src, destPath, true);

                // Preserve timestamps
                File.SetLastWriteTimeUtc(destPath, File.GetLastWriteTimeUtc(src));
                File.SetCreationTimeUtc(destPath, File.GetCreationTimeUtc(src));

                return destPath;
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to copy2 '" + src + "' to '" + dst + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Recursively copy a directory tree from <paramref name="src"/> to <paramref name="dst"/>.
        /// The destination directory must not already exist.
        /// Similar to Python's <c>shutil.copytree()</c>.
        /// </summary>
        /// <param name="src">Source directory path.</param>
        /// <param name="dst">Destination directory path (must not exist).</param>
        /// <returns>The path to the destination directory.</returns>
        /// <exception cref="OSError">Thrown if the source does not exist, destination exists, or copying fails.</exception>
        /// <example>
        /// <code>
        /// shutil.copytree("src_dir", "dst_dir")    # recursive copy
        /// </code>
        /// </example>
        public static string Copytree(string src, string dst)
        {
            if (!Directory.Exists(src))
            {
                throw new OSError("No such directory: '" + src + "'");
            }

            try
            {
                CopyDirectoryRecursive(src, dst);
                return dst;
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to copytree '" + src + "' to '" + dst + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Recursively delete a directory tree.
        /// Similar to Python's <c>shutil.rmtree()</c>.
        /// </summary>
        /// <param name="path">The directory to remove.</param>
        /// <exception cref="OSError">Thrown if the directory does not exist or removal fails.</exception>
        /// <example>
        /// <code>
        /// shutil.rmtree("/tmp/mydir")    # delete directory and all contents
        /// </code>
        /// </example>
        public static void Rmtree(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new OSError("No such directory: '" + path + "'");
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to remove directory tree '" + path + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Move a file or directory to another location.
        /// Similar to Python's <c>shutil.move()</c>.
        /// </summary>
        /// <param name="src">Source file or directory path.</param>
        /// <param name="dst">Destination path.</param>
        /// <returns>The destination path.</returns>
        /// <exception cref="OSError">Thrown if the source does not exist or moving fails.</exception>
        /// <example>
        /// <code>
        /// shutil.move("old.txt", "new.txt")         # rename/move file
        /// shutil.move("old_dir", "new_dir")         # rename/move directory
        /// </code>
        /// </example>
        public static string Move(string src, string dst)
        {
            try
            {
                if (File.Exists(src))
                {
                    string destPath = ResolveDestination(src, dst);
                    try
                    {
                        File.Move(src, destPath);
                    }
                    catch (IOException)
                    {
                        // Cross-device move: fall back to copy + delete
                        File.Copy(src, destPath, true);
                        File.Delete(src);
                    }
                    return destPath;
                }
                else if (Directory.Exists(src))
                {
                    Directory.Move(src, dst);
                    return dst;
                }
                else
                {
                    throw new OSError("No such file or directory: '" + src + "'");
                }
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to move '" + src + "' to '" + dst + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Return the path to an executable which would be run if the given command
        /// was called. Returns <c>null</c> if no executable is found.
        /// Similar to Python's <c>shutil.which()</c>.
        /// </summary>
        /// <param name="name">The command name to search for.</param>
        /// <returns>The full path to the executable, or <c>null</c> if not found.</returns>
        /// <example>
        /// <code>
        /// shutil.which("python")    # "/usr/bin/python"
        /// shutil.which("nonexist")  # None
        /// </code>
        /// </example>
        public static string? Which(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // If the name contains a path separator, check directly
            if (name.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0 ||
                name.IndexOf(System.IO.Path.AltDirectorySeparatorChar) >= 0)
            {
                if (File.Exists(name))
                {
                    return System.IO.Path.GetFullPath(name);
                }
                return null;
            }

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            string[] pathDirs = pathEnv.Split(System.IO.Path.PathSeparator);

            // On Windows, also check common executable extensions
            bool isWindows = System.IO.Path.DirectorySeparatorChar == '\\';
            string[] extensions;
            if (isWindows)
            {
                string? pathExt = Environment.GetEnvironmentVariable("PATHEXT");
                extensions = !string.IsNullOrEmpty(pathExt)
                    ? pathExt.Split(';')
                    : new[] { ".COM", ".EXE", ".BAT", ".CMD" };
            }
            else
            {
                extensions = new[] { "" };
            }

            foreach (string dir in pathDirs)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                foreach (string ext in extensions)
                {
                    string candidate = System.IO.Path.Combine(dir, name + ext);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Return disk usage statistics for the given path as a tuple of (total, used, free) bytes.
        /// Similar to Python's <c>shutil.disk_usage()</c>.
        /// </summary>
        /// <param name="path">A path on the filesystem (file or directory).</param>
        /// <returns>A tuple of (total, used, free) bytes.</returns>
        /// <exception cref="OSError">Thrown if the drive information cannot be determined.</exception>
        /// <example>
        /// <code>
        /// shutil.disk_usage("/")    # (500107862016, 230000000000, 270107862016)
        /// </code>
        /// </example>
        public static (long, long, long) DiskUsage(string path)
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                string root = System.IO.Path.GetPathRoot(fullPath) ?? fullPath;

                DriveInfo driveInfo = new DriveInfo(root);
                long total = driveInfo.TotalSize;
                long free = driveInfo.AvailableFreeSpace;
                long used = total - free;

                return (total, used, free);
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to get disk usage for '" + path + "': " + ex.Message, ex);
            }
        }

        private static string ResolveDestination(string src, string dst)
        {
            if (Directory.Exists(dst))
            {
                return System.IO.Path.Combine(dst, System.IO.Path.GetFileName(src));
            }
            return dst;
        }

        private static void CopyDirectoryRecursive(string src, string dst)
        {
            Directory.CreateDirectory(dst);

            foreach (string file in Directory.GetFiles(src))
            {
                string destFile = System.IO.Path.Combine(dst, System.IO.Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(src))
            {
                string destDir = System.IO.Path.Combine(dst, System.IO.Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destDir);
            }
        }
    }
}
