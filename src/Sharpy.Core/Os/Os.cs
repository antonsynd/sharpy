using System;
using System.IO;

namespace Sharpy
{
    public static partial class Os
    {
        /// <summary>
        /// The name of the operating system: "posix" or "nt".
        /// </summary>
        public static string Name =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ? "nt" : "posix";

        /// <summary>
        /// The path separator character for the current platform.
        /// </summary>
        public static string Sep => System.IO.Path.DirectorySeparatorChar.ToString();

        /// <summary>
        /// The line separator string for the current platform.
        /// </summary>
        public static string Linesep => Environment.NewLine;

        /// <summary>
        /// A snapshot of the current environment variables.
        /// </summary>
        public static Dict<string, string> Environ
        {
            get
            {
                var dict = new Dict<string, string>();
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    if (entry.Key is string key && entry.Value is string value)
                    {
                        dict[key] = value;
                    }
                }
                return dict;
            }
        }

        // --- File operations ---

        /// <summary>
        /// Remove (delete) the file path. Raises FileNotFoundError if the file does not exist.
        /// </summary>
        public static void Remove(string path)
        {
            if (Directory.Exists(path))
                throw new IsADirectoryError("Is a directory: '" + path + "'");
            if (!File.Exists(path))
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            File.Delete(path);
        }

        /// <summary>
        /// Rename the file or directory src to dst.
        /// </summary>
        public static void Rename(string src, string dst)
        {
            if (Directory.Exists(src))
            {
                Directory.Move(src, dst);
            }
            else if (File.Exists(src))
            {
                File.Move(src, dst);
            }
            else
            {
                throw new FileNotFoundError("No such file or directory: '" + src + "'");
            }
        }

        /// <summary>
        /// Create a directory. Raises FileNotFoundError if the parent directory does not exist.
        /// Raises FileExistsError if the directory already exists.
        /// </summary>
        public static void Mkdir(string path)
        {
            if (Directory.Exists(path))
                throw new FileExistsError("File exists: '" + path + "'");
            if (File.Exists(path))
                throw new FileExistsError("File exists: '" + path + "'");

            string? parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                throw new FileNotFoundError("No such file or directory: '" + path + "'");

            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Recursive directory creation. Like mkdir(), but creates intermediate directories as needed.
        /// </summary>
        public static void Makedirs(string path, bool exist_ok = false)
        {
            if (Directory.Exists(path))
            {
                if (!exist_ok)
                    throw new FileExistsError("File exists: '" + path + "'");
                return;
            }
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Remove (delete) the directory path. The directory must be empty.
        /// </summary>
        public static void Rmdir(string path)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                    throw new NotADirectoryError("Not a directory: '" + path + "'");
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            try
            {
                Directory.Delete(path, false);
            }
            catch (IOException ex)
            {
                throw new OSError("Directory not empty: '" + path + "'", ex);
            }
        }

        /// <summary>
        /// Return a list containing the names of the entries in the directory given by path.
        /// </summary>
        public static Sharpy.List<string> Listdir(string path = ".")
        {
            if (!Directory.Exists(path))
                throw new FileNotFoundError("No such file or directory: '" + path + "'");

            var result = new Sharpy.List<string>();
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                result.Append(System.IO.Path.GetFileName(entry));
            }
            return result;
        }

        /// <summary>
        /// Return a string representing the current working directory.
        /// </summary>
        public static string Getcwd()
        {
            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Change the current working directory to path.
        /// </summary>
        public static void Chdir(string path)
        {
            if (!Directory.Exists(path))
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            Directory.SetCurrentDirectory(path);
        }

        // --- Environment ---

        /// <summary>
        /// Return the value of the environment variable key, or null if it does not exist.
        /// </summary>
        public static string? Getenv(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        /// <summary>
        /// Return the value of the environment variable key, or defaultValue if it does not exist.
        /// </summary>
        public static string Getenv(string key, string defaultValue)
        {
            return Environment.GetEnvironmentVariable(key) ?? defaultValue;
        }

        /// <summary>
        /// Set the environment variable named key to the string value.
        /// </summary>
        public static void Putenv(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // --- Walk ---

        /// <summary>
        /// Generate the file names in a directory tree by walking the tree top-down.
        /// For each directory, yields (dirpath, dirnames, filenames).
        /// </summary>
        public static System.Collections.Generic.IEnumerable<(string dirpath, Sharpy.List<string> dirnames, Sharpy.List<string> filenames)> Walk(string top)
        {
            if (!Directory.Exists(top))
                yield break;

            var dirnames = new Sharpy.List<string>();
            var filenames = new Sharpy.List<string>();

            try
            {
                foreach (var dir in Directory.GetDirectories(top))
                {
                    dirnames.Append(System.IO.Path.GetFileName(dir));
                }
                foreach (var file in Directory.GetFiles(top))
                {
                    filenames.Append(System.IO.Path.GetFileName(file));
                }
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            yield return (top, dirnames, filenames);

            foreach (var dirname in dirnames)
            {
                string subdir = System.IO.Path.Combine(top, dirname);
                foreach (var entry in Walk(subdir))
                {
                    yield return entry;
                }
            }
        }

        // --- Stat ---

        /// <summary>
        /// Perform a stat system call on the given path.
        /// </summary>
        public static StatResult Stat(string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return new StatResult(
                    info.Length,
                    ToUnixTimestamp(info.LastWriteTimeUtc),
                    ToUnixTimestamp(info.LastAccessTimeUtc),
                    ToUnixTimestamp(info.CreationTimeUtc)
                );
            }
            else if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                return new StatResult(
                    0,
                    ToUnixTimestamp(info.LastWriteTimeUtc),
                    ToUnixTimestamp(info.LastAccessTimeUtc),
                    ToUnixTimestamp(info.CreationTimeUtc)
                );
            }
            else
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
        }

        private static double ToUnixTimestamp(System.DateTime utcTime)
        {
            return (utcTime - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
        }
    }

    /// <summary>
    /// Result of an os.stat() call.
    /// </summary>
    public class StatResult
    {
        public long StSize { get; }
        public double StMtime { get; }
        public double StAtime { get; }
        public double StCtime { get; }

        public StatResult(long stSize, double stMtime, double stAtime, double stCtime)
        {
            StSize = stSize;
            StMtime = stMtime;
            StAtime = stAtime;
            StCtime = stCtime;
        }

        public override string ToString()
        {
            return $"os.stat_result(st_size={StSize}, st_mtime={StMtime:F1}, st_atime={StAtime:F1}, st_ctime={StCtime:F1})";
        }
    }
}
