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
        public static string Getcwd()
        {
            return Directory.GetCurrentDirectory();
        }

        /// <summary>Change the current working directory.</summary>
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
        public static string? Getenv(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        /// <summary>Get an environment variable with a default value.</summary>
        public static string Getenv(string key, string default_)
        {
            return Environment.GetEnvironmentVariable(key) ?? default_;
        }

        /// <summary>Set an environment variable.</summary>
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

        /// <summary>Check if a path exists (file or directory).</summary>
        public static bool Path_exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>Walk a directory tree, yielding (dirpath, dirnames, filenames) tuples.</summary>
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
            for (int i = 0; i < ((System.Collections.Generic.ICollection<string>)dirnames).Count; i++)
            {
                var subdir = System.IO.Path.Combine(top, dirnames[i]);
                foreach (var entry in Walk(subdir))
                {
                    yield return entry;
                }
            }
        }
    }
}
