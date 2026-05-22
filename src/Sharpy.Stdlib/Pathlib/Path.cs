using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Object-oriented filesystem path, similar to Python's pathlib.Path.
    /// Immutable — all mutation methods return new Path instances.
    /// </summary>
    [SharpyModuleType("pathlib")]
    public sealed class Path : IEquatable<Path>
    {
        private readonly string _path;

        /// <summary>Create a path from a string.</summary>
        public Path(string path)
        {
            _path = path ?? throw new TypeError("Path() argument must be str, not None");
        }

        /// <summary>Create a path by joining segments.</summary>
        public Path(string first, string second)
        {
            _path = System.IO.Path.Combine(first, second);
        }

        /// <summary>Create a path by joining segments.</summary>
        public Path(string first, string second, string third)
        {
            _path = System.IO.Path.Combine(first, second, third);
        }

        // ===== Operator / for joining =====

        /// <summary>Join two paths with /.</summary>
        /// <param name="left">The base path.</param>
        /// <param name="right">The path segment to append.</param>
        /// <returns>A new path joining <paramref name="left"/> and <paramref name="right"/>.</returns>
        /// <example>
        /// <code>
        /// p = Path("/home") / "user" / "file.txt"
        /// print(p)    # "/home/user/file.txt"
        /// </code>
        /// </example>
        public static Path operator /(Path left, string right)
        {
            return new Path(System.IO.Path.Combine(left._path, right));
        }

        /// <summary>Join two paths with /.</summary>
        public static Path operator /(Path left, Path right)
        {
            return new Path(System.IO.Path.Combine(left._path, right._path));
        }

        // ===== Properties =====

        /// <summary>The final component of the path.</summary>
        public string Name => System.IO.Path.GetFileName(_path);

        /// <summary>The final component without its suffix.</summary>
        public string Stem => System.IO.Path.GetFileNameWithoutExtension(_path);

        /// <summary>The file extension (including the dot).</summary>
        public string Suffix => System.IO.Path.GetExtension(_path);

        /// <summary>All suffixes of the final component.</summary>
        public List<string> Suffixes
        {
            get
            {
                var result = new List<string>();
                var name = Name;
                var idx = name.IndexOf('.');
                if (idx >= 0)
                {
                    var remaining = name.Substring(idx);
                    while (remaining.Length > 0)
                    {
                        var nextDot = remaining.IndexOf('.', 1);
                        if (nextDot < 0)
                        {
                            result.Append(remaining);
                            break;
                        }
                        result.Append(remaining.Substring(0, nextDot));
                        remaining = remaining.Substring(nextDot);
                    }
                }
                return result;
            }
        }

        /// <summary>The logical parent of the path.</summary>
        public Path Parent
        {
            get
            {
                var dir = System.IO.Path.GetDirectoryName(_path);
                return new Path(string.IsNullOrEmpty(dir) ? "." : dir);
            }
        }

        /// <summary>The path components as a list.</summary>
        public List<string> Parts
        {
            get
            {
                var result = new List<string>();
                var p = _path;
                var root = System.IO.Path.GetPathRoot(p);
                if (!string.IsNullOrEmpty(root))
                {
                    result.Append(root);
                    p = p.Substring(root.Length);
                }
                if (p.Length > 0)
                {
                    foreach (var part in p.Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        result.Append(part);
                    }
                }
                return result;
            }
        }

        /// <summary>The root of the path (e.g., "/" on Unix).</summary>
        public string Root => System.IO.Path.GetPathRoot(_path) ?? "";

        /// <summary>The concatenation of drive and root (e.g., "/" on Unix, "C:\" on Windows).</summary>
        public string Anchor => Root;

        /// <summary>Whether the path is absolute.</summary>
        public bool IsAbsolute => System.IO.Path.IsPathRooted(_path);

        // ===== Query Methods =====

        /// <summary>Whether the path exists on the filesystem.</summary>
        /// <returns><c>true</c> if the path exists.</returns>
        /// <example>
        /// <code>
        /// Path("/tmp").exists()        # True
        /// Path("/no/such").exists()    # False
        /// </code>
        /// </example>
        public bool Exists()
        {
            return File.Exists(_path) || Directory.Exists(_path);
        }

        /// <summary>Whether the path points to a regular file.</summary>
        public bool IsFile()
        {
            return File.Exists(_path);
        }

        /// <summary>Whether the path points to a directory.</summary>
        public bool IsDir()
        {
            return Directory.Exists(_path);
        }

        // ===== File I/O =====

        /// <summary>Read the file as text.</summary>
        /// <param name="encoding">Text encoding (default: "utf-8").</param>
        /// <returns>The file contents as a string.</returns>
        /// <example>
        /// <code>
        /// p = Path("hello.txt")
        /// text = p.read_text()    # "Hello, world!"
        /// </code>
        /// </example>
        public string ReadText(string encoding = "utf-8")
        {
            return File.ReadAllText(_path, GetEncoding(encoding));
        }

        /// <summary>Write text to the file.</summary>
        public void WriteText(string data, string encoding = "utf-8")
        {
            File.WriteAllText(_path, data, GetEncoding(encoding));
        }

        /// <summary>Read the file as bytes.</summary>
        public byte[] ReadBytes()
        {
            return File.ReadAllBytes(_path);
        }

        /// <summary>Write bytes to the file.</summary>
        public void WriteBytes(byte[] data)
        {
            File.WriteAllBytes(_path, data);
        }

        // ===== Directory Operations =====

        /// <summary>Create the directory. Optionally create parents.</summary>
        public void Mkdir(bool parents = false, bool exist_ok = false)
        {
            if (Directory.Exists(_path))
            {
                if (!exist_ok)
                {
                    throw new FileExistsError("File exists: '" + _path + "'");
                }
                return;
            }
            if (parents)
            {
                Directory.CreateDirectory(_path);
            }
            else
            {
                var parent = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                {
                    throw new FileNotFoundError("No such file or directory: '" + _path + "'");
                }
                Directory.CreateDirectory(_path);
            }
        }

        /// <summary>Remove the directory (must be empty).</summary>
        public void Rmdir()
        {
            if (!Directory.Exists(_path))
            {
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            }
            Directory.Delete(_path, false);
        }

        /// <summary>Iterate over the directory entries.</summary>
        public IEnumerable<Path> Iterdir()
        {
            if (!Directory.Exists(_path))
            {
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            }
            foreach (var entry in Directory.GetFileSystemEntries(_path))
            {
                yield return new Path(entry);
            }
        }

        /// <summary>Glob for matching paths relative to this directory.</summary>
        public IEnumerable<Path> Glob(string pattern)
        {
            if (!Directory.Exists(_path))
            {
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            }
            foreach (var entry in Directory.GetFileSystemEntries(_path, pattern))
            {
                yield return new Path(entry);
            }
        }

        // ===== Mutation (returns new Path) =====

        /// <summary>Rename the file or directory.</summary>
        public Path Rename(string target)
        {
            if (File.Exists(_path))
            {
                File.Move(_path, target);
            }
            else if (Directory.Exists(_path))
            {
                Directory.Move(_path, target);
            }
            else
            {
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            }
            return new Path(target);
        }

        /// <summary>Remove the file.</summary>
        public void Unlink(bool missing_ok = false)
        {
            if (!File.Exists(_path))
            {
                if (!missing_ok)
                {
                    throw new FileNotFoundError("No such file or directory: '" + _path + "'");
                }
                return;
            }
            File.Delete(_path);
        }

        /// <summary>Rename, replacing the target if it exists.</summary>
        public Path Replace(string target)
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            return Rename(target);
        }

        // ===== Navigation =====

        /// <summary>Make the path absolute, resolving any symlinks.</summary>
        public Path Resolve()
        {
            return new Path(System.IO.Path.GetFullPath(_path));
        }

        /// <summary>Return a new path with the name changed.</summary>
        public Path WithName(string name)
        {
            var parent = System.IO.Path.GetDirectoryName(_path);
            return new Path(string.IsNullOrEmpty(parent) ? name : System.IO.Path.Combine(parent, name));
        }

        /// <summary>Return a new path with the stem changed.</summary>
        public Path WithStem(string stem)
        {
            return WithName(stem + Suffix);
        }

        /// <summary>Return a new path with the suffix changed.</summary>
        public Path WithSuffix(string suffix)
        {
            return WithName(Stem + suffix);
        }

        /// <summary>Return a relative path from this path to other.</summary>
        public Path RelativeTo(string other)
        {
            var fullThis = System.IO.Path.GetFullPath(_path);
            var fullOther = System.IO.Path.GetFullPath(other);
            if (!fullThis.StartsWith(fullOther, StringComparison.Ordinal))
            {
                throw new ValueError("'" + _path + "' is not relative to '" + other + "'");
            }
            var relative = fullThis.Substring(fullOther.Length);
            if (relative.Length > 0 && (relative[0] == System.IO.Path.DirectorySeparatorChar || relative[0] == System.IO.Path.AltDirectorySeparatorChar))
            {
                relative = relative.Substring(1);
            }
            return new Path(relative.Length == 0 ? "." : relative);
        }

        // ===== String Conversion =====

        /// <summary>Return the string representation of the path.</summary>
        public override string ToString()
        {
            return _path;
        }

        // ===== Equality =====

        /// <summary>Check equality with another Path.</summary>
        public bool Equals(Path? other)
        {
            if (other is null)
                return false;
            return _path == other._path;
        }

        /// <summary>Check equality.</summary>
        public override bool Equals(object? obj)
        {
            return obj is Path other && Equals(other);
        }

        /// <summary>Get hash code.</summary>
        public override int GetHashCode()
        {
            return _path.GetHashCode();
        }

        /// <summary>Determines whether two paths are equal.</summary>
        public static bool operator ==(Path? left, Path? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        /// <summary>Determines whether two paths are not equal.</summary>
        public static bool operator !=(Path? left, Path? right)
        {
            return !(left == right);
        }

        // ===== Static Constructors =====

        /// <summary>Return the current working directory.</summary>
        public static Path Cwd()
        {
            return new Path(Directory.GetCurrentDirectory());
        }

        /// <summary>Return the user's home directory.</summary>
        public static Path Home()
        {
            return new Path(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        // ===== File Operations =====

        /// <summary>
        /// Create the file if it doesn't exist, or update its timestamp if it does.
        /// </summary>
        /// <param name="existOk">If false, raise FileExistsError when the file already exists.</param>
        public void Touch(bool existOk = true)
        {
            if (File.Exists(_path))
            {
                if (!existOk)
                {
                    throw new FileExistsError("File exists: '" + _path + "'");
                }
                File.SetLastWriteTimeUtc(_path, System.DateTime.UtcNow);
            }
            else
            {
                // Ensure parent directory exists
                var dir = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    throw new FileNotFoundError("No such file or directory: '" + _path + "'");
                }
                File.Create(_path).Dispose();
            }
        }

        /// <summary>Return file or directory stats.</summary>
        public StatResult Stat()
        {
            if (File.Exists(_path))
            {
                var info = new FileInfo(_path);
                return new StatResult(
                    stSize: info.Length,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            if (Directory.Exists(_path))
            {
                var info = new DirectoryInfo(_path);
                return new StatResult(
                    stSize: 0,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            throw new FileNotFoundError("No such file or directory: '" + _path + "'");
        }

        /// <summary>Whether the path is a symbolic link.</summary>
        public bool IsSymlink()
        {
            try
            {
                var attrs = File.GetAttributes(_path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ===== Recursive Glob =====

        /// <summary>Recursively glob for matching paths relative to this directory.</summary>
        public IEnumerable<Path> Rglob(string pattern)
        {
            if (!Directory.Exists(_path))
            {
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            }
            foreach (var entry in Directory.EnumerateFileSystemEntries(_path, pattern, SearchOption.AllDirectories))
            {
                yield return new Path(entry);
            }
        }

        // ===== Pattern Matching =====

        /// <summary>Match this path's name against a glob pattern.</summary>
        public bool Match(string pattern)
        {
            // Python's Path.match() matches the entire path component-wise from the right.
            // For simple patterns (no separator), match against the final component (Name).
            var name = Name;
            return GlobMatch(name, pattern);
        }

        // ===== User Expansion =====

        /// <summary>Expand ~ at the start to the user's home directory.</summary>
        public Path Expanduser()
        {
            if (_path == "~" || _path.StartsWith("~" + System.IO.Path.DirectorySeparatorChar) || _path.StartsWith("~" + System.IO.Path.AltDirectorySeparatorChar))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home))
                {
                    throw new RuntimeError("Could not determine home directory");
                }
                if (_path == "~")
                {
                    return new Path(home);
                }
                return new Path(System.IO.Path.Combine(home, _path.Substring(2)));
            }
            return new Path(_path);
        }

        // ===== Helpers =====

        /// <summary>Simple glob-style pattern matching (supports * and ?).</summary>
        private static bool GlobMatch(string text, string pattern)
        {
            int ti = 0, pi = 0;
            int starTi = -1, starPi = -1;
            while (ti < text.Length)
            {
                if (pi < pattern.Length && (pattern[pi] == '?' || pattern[pi] == text[ti]))
                {
                    ti++;
                    pi++;
                }
                else if (pi < pattern.Length && pattern[pi] == '*')
                {
                    starPi = pi;
                    starTi = ti;
                    pi++;
                }
                else if (starPi >= 0)
                {
                    pi = starPi + 1;
                    starTi++;
                    ti = starTi;
                }
                else
                {
                    return false;
                }
            }
            while (pi < pattern.Length && pattern[pi] == '*')
            {
                pi++;
            }
            return pi == pattern.Length;
        }

        private static Encoding GetEncoding(string encoding)
        {
            switch (encoding.ToLowerInvariant())
            {
                case "utf-8":
                case "utf8":
                    return new UTF8Encoding(false);
                case "ascii":
                    return Encoding.ASCII;
                case "utf-16":
                case "utf16":
                    return Encoding.Unicode;
                case "latin-1":
                case "latin1":
                case "iso-8859-1":
                    return Encoding.GetEncoding("iso-8859-1");
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }
    }
}
