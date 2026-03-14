using System;
using System.Collections.Generic;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// OS-level operations, similar to Python's os module.
    /// Wraps System.IO and System.Environment for file, directory, and environment operations.
    /// </summary>
    public static partial class Os
    {
        // ===== Path Separator Constants =====

        /// <summary>Path separator character for the current OS.</summary>
        public static readonly string Sep = System.IO.Path.DirectorySeparatorChar.ToString();

        /// <summary>Line separator for the current OS.</summary>
        public static readonly string Linesep = Environment.NewLine;

        /// <summary>OS name: "posix" on Unix/macOS, "nt" on Windows.</summary>
        public static readonly string Name = Environment.OSVersion.Platform == PlatformID.Win32NT ? "nt" : "posix";

        /// <summary>Alternative path separator, or empty string if none.</summary>
        public static readonly string Altsep = System.IO.Path.AltDirectorySeparatorChar == System.IO.Path.DirectorySeparatorChar
            ? ""
            : System.IO.Path.AltDirectorySeparatorChar.ToString();

        /// <summary>Separator used in PATH environment variable.</summary>
        public static readonly string Pathsep = System.IO.Path.PathSeparator.ToString();

        // ===== File Operations =====

        /// <summary>Remove (delete) a file.</summary>
        /// <param name="path">The path to the file to remove.</param>
        /// <exception cref="FileNotFoundError">Thrown if the file does not exist.</exception>
        /// <exception cref="IsADirectoryError">Thrown if the path is a directory.</exception>
        /// <exception cref="PermissionError">Thrown if permission is denied.</exception>
        public static void Remove(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            if (Directory.Exists(path))
            {
                throw new IsADirectoryError("Is a directory: '" + path + "'");
            }
            try
            {
                File.Delete(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PermissionError("Permission denied: '" + path + "'", ex);
            }
        }

        /// <summary>Rename a file or directory.</summary>
        /// <param name="src">The current path.</param>
        /// <param name="dst">The new path.</param>
        /// <exception cref="FileNotFoundError">Thrown if <paramref name="src"/> does not exist.</exception>
        public static void Rename(string src, string dst)
        {
            if (File.Exists(src))
            {
                File.Move(src, dst);
            }
            else if (Directory.Exists(src))
            {
                Directory.Move(src, dst);
            }
            else
            {
                throw new FileNotFoundError("No such file or directory: '" + src + "'");
            }
        }

        // ===== Directory Operations =====

        /// <summary>Create a directory.</summary>
        /// <param name="path">The directory path to create.</param>
        /// <exception cref="FileExistsError">Thrown if the directory already exists.</exception>
        /// <exception cref="FileNotFoundError">Thrown if the parent directory does not exist.</exception>
        public static void Mkdir(string path)
        {
            if (Directory.Exists(path))
            {
                throw new FileExistsError("File exists: '" + path + "'");
            }
            var parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            Directory.CreateDirectory(path);
        }

        /// <summary>Create a directory and all intermediate directories.</summary>
        /// <param name="path">The directory path to create.</param>
        /// <param name="exist_ok">If <c>true</c>, do not raise an error if the directory already exists.</param>
        /// <exception cref="FileExistsError">Thrown if the directory exists and <paramref name="exist_ok"/> is <c>false</c>.</exception>
        public static void Makedirs(string path, bool exist_ok = false)
        {
            if (Directory.Exists(path))
            {
                if (!exist_ok)
                {
                    throw new FileExistsError("File exists: '" + path + "'");
                }
                return;
            }
            Directory.CreateDirectory(path);
        }

        /// <summary>Remove an empty directory.</summary>
        /// <param name="path">The directory path to remove.</param>
        /// <exception cref="FileNotFoundError">Thrown if the directory does not exist.</exception>
        /// <exception cref="IOError">Thrown if the directory is not empty.</exception>
        public static void Rmdir(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            try
            {
                Directory.Delete(path, false);
            }
            catch (IOException ex)
            {
                throw new IOError("Directory not empty: '" + path + "'", ex);
            }
        }

        /// <summary>List directory contents. Returns a list of entry names.</summary>
        /// <param name="path">Directory path to list. Defaults to current directory.</param>
        /// <returns>A list of file and directory names in the given path.</returns>
        /// <example>
        /// <code>
        /// os.listdir(".")         # ["file.txt", "subdir"]
        /// os.listdir("/tmp")      # ["a.log", "b.log"]
        /// </code>
        /// </example>
        public static List<string> Listdir(string path = ".")
        {
            if (!Directory.Exists(path))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            var result = new List<string>();
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                result.Append(System.IO.Path.GetFileName(entry));
            }
            return result;
        }

        /// <summary>Get the current working directory.</summary>
        /// <returns>The current working directory as a string.</returns>
        /// <example>
        /// <code>
        /// os.getcwd()    # "/home/user/project"
        /// </code>
        /// </example>
        public static string Getcwd()
        {
            return Directory.GetCurrentDirectory();
        }

        /// <summary>Change the current working directory.</summary>
        /// <param name="path">The directory to change to.</param>
        /// <exception cref="FileNotFoundError">Thrown if the directory does not exist.</exception>
        public static void Chdir(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            Directory.SetCurrentDirectory(path);
        }

        // ===== Environment Variables =====

        /// <summary>Get an environment variable, returning None if not set.</summary>
        /// <param name="key">The environment variable name.</param>
        /// <returns>The value, or <c>null</c> if not set.</returns>
        /// <example>
        /// <code>
        /// os.getenv("HOME")       # "/home/user"
        /// os.getenv("MISSING")    # None
        /// </code>
        /// </example>
        public static string? Getenv(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        /// <summary>Get an environment variable with a default value.</summary>
        /// <param name="key">The environment variable name.</param>
        /// <param name="default_">The value to return if the variable is not set.</param>
        /// <returns>The variable value, or <paramref name="default_"/> if not set.</returns>
        public static string Getenv(string key, string default_)
        {
            return Environment.GetEnvironmentVariable(key) ?? default_;
        }

        /// <summary>Set an environment variable.</summary>
        /// <param name="key">The environment variable name.</param>
        /// <param name="value">The value to set.</param>
        public static void Putenv(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        /// <summary>Get a snapshot of all environment variables.</summary>
        public static Dict<string, string> Environ
        {
            get
            {
                var dict = new Dict<string, string>();
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    if (entry.Key is string k && entry.Value is string v)
                    {
                        dict[k] = v;
                    }
                }
                return dict;
            }
        }

        // ===== File/Directory Info =====

        /// <summary>Get file or directory status, similar to Python's os.stat().</summary>
        /// <param name="path">The file or directory path.</param>
        /// <returns>A <see cref="StatResult"/> containing size, timestamps, and mode.</returns>
        /// <exception cref="FileNotFoundError">Thrown if the path does not exist.</exception>
        public static StatResult Stat(string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return new StatResult(
                    stSize: info.Length,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                return new StatResult(
                    stSize: 0,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            throw new FileNotFoundError("No such file or directory: '" + path + "'");
        }

        /// <summary>Check if a path exists (file or directory).</summary>
        /// <param name="path">The path to check.</param>
        /// <returns><c>true</c> if the path exists; otherwise <c>false</c>.</returns>
        public static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>Walk a directory tree, yielding (dirpath, dirnames, filenames) tuples.</summary>
        /// <param name="top">The root directory to walk.</param>
        /// <returns>An enumerable of (dirpath, dirnames, filenames) tuples for each directory.</returns>
        public static IEnumerable<(string dirpath, List<string> dirnames, List<string> filenames)> Walk(string top)
        {
            if (!Directory.Exists(top))
            {
                yield break;
            }

            var dirnames = new List<string>();
            var filenames = new List<string>();

            foreach (var dir in Directory.GetDirectories(top))
            {
                dirnames.Append(System.IO.Path.GetFileName(dir));
            }
            foreach (var file in Directory.GetFiles(top))
            {
                filenames.Append(System.IO.Path.GetFileName(file));
            }

            yield return (top, dirnames, filenames);

            // Recursively walk subdirectories
            foreach (string dirname in dirnames)
            {
                var subdir = System.IO.Path.Combine(top, dirname);
                foreach (var entry in Walk(subdir))
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// Result of os.stat(), similar to Python's os.stat_result.
    /// </summary>
    public sealed class StatResult
    {
        /// <summary>Size in bytes (0 for directories).</summary>
        public long StSize { get; }

        /// <summary>Time of last modification (Unix timestamp).</summary>
        public double StMtime { get; }

        /// <summary>Time of creation (Unix timestamp).</summary>
        public double StCtime { get; }

        /// <summary>Time of last access (Unix timestamp).</summary>
        public double StAtime { get; }

        /// <summary>File mode / attributes.</summary>
        public int StMode { get; }

        /// <summary>Create a new stat result.</summary>
        public StatResult(long stSize, double stMtime, double stCtime, double stAtime, int stMode)
        {
            StSize = stSize;
            StMtime = stMtime;
            StCtime = stCtime;
            StAtime = stAtime;
            StMode = stMode;
        }
    }
}
