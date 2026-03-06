using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// PurePath / Path implementation providing a Python pathlib-like API.
    /// </summary>
    public class Path : IEquatable<Path>
    {
        private readonly string _path;

        /// <summary>
        /// Create a Path from a string.
        /// </summary>
        public Path(string path)
        {
            _path = path ?? throw new TypeError("expected str, not NoneType");
        }

        /// <summary>
        /// Create a Path by joining multiple segments.
        /// </summary>
        public Path(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
                _path = ".";
            else
                _path = System.IO.Path.Combine(segments);
        }

        // --- Operators ---

        /// <summary>
        /// Join a Path with a string using the / operator.
        /// </summary>
        public static Path operator /(Path left, string right)
        {
            return new Path(System.IO.Path.Combine(left._path, right));
        }

        /// <summary>
        /// Join two Paths using the / operator.
        /// </summary>
        public static Path operator /(Path left, Path right)
        {
            return new Path(System.IO.Path.Combine(left._path, right._path));
        }

        // --- Properties ---

        /// <summary>
        /// The final component of this path.
        /// </summary>
        public string Name => System.IO.Path.GetFileName(_path);

        /// <summary>
        /// The final component, without its suffix.
        /// </summary>
        public string Stem => System.IO.Path.GetFileNameWithoutExtension(_path);

        /// <summary>
        /// The file extension of the final component.
        /// </summary>
        public string Suffix => System.IO.Path.GetExtension(_path);

        /// <summary>
        /// A list of the path's file extensions.
        /// </summary>
        public List<string> Suffixes
        {
            get
            {
                var result = new List<string>();
                string name = Name;
                int firstDot = name.IndexOf('.');
                if (firstDot < 0)
                    return result;

                // Extract all suffixes from the name
                int pos = firstDot;
                while (pos < name.Length)
                {
                    int nextDot = name.IndexOf('.', pos + 1);
                    if (nextDot < 0)
                    {
                        result.Append(name.Substring(pos));
                        break;
                    }
                    else
                    {
                        result.Append(name.Substring(pos, nextDot - pos));
                        pos = nextDot;
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// The logical parent of the path.
        /// </summary>
        public Path Parent
        {
            get
            {
                string? dir = System.IO.Path.GetDirectoryName(_path);
                if (string.IsNullOrEmpty(dir))
                {
                    // For root paths like "/" return self; for relative like "a" return "."
                    if (System.IO.Path.IsPathRooted(_path))
                        return this;
                    return new Path(".");
                }
                return new Path(dir);
            }
        }

        /// <summary>
        /// An immutable sequence of the path's components.
        /// </summary>
        public List<string> Parts
        {
            get
            {
                var result = new List<string>();
                if (string.IsNullOrEmpty(_path))
                    return result;

                string root = System.IO.Path.GetPathRoot(_path) ?? "";
                if (!string.IsNullOrEmpty(root))
                {
                    result.Append(root);
                }

                string rest = string.IsNullOrEmpty(root)
                    ? _path
                    : _path.Substring(root.Length);

                if (!string.IsNullOrEmpty(rest))
                {
                    string[] segments = rest.Split(
                        new[] { '/', '\\' },
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (var seg in segments)
                    {
                        result.Append(seg);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// A string representing the root, if any.
        /// </summary>
        public string Root
        {
            get
            {
                string root = System.IO.Path.GetPathRoot(_path) ?? "";
                return root;
            }
        }

        /// <summary>
        /// The concatenation of the drive and root.
        /// </summary>
        public string Anchor => Root;

        /// <summary>
        /// Whether the path is absolute.
        /// </summary>
        public bool IsAbsolute => System.IO.Path.IsPathRooted(_path);

        // --- Query methods ---

        /// <summary>
        /// Whether this path exists on the filesystem.
        /// </summary>
        public bool Exists()
        {
            return File.Exists(_path) || Directory.Exists(_path);
        }

        /// <summary>
        /// Whether this path is an existing regular file.
        /// </summary>
        public bool IsFile()
        {
            return File.Exists(_path);
        }

        /// <summary>
        /// Whether this path is an existing directory.
        /// </summary>
        public bool IsDir()
        {
            return Directory.Exists(_path);
        }

        // --- File I/O ---

        /// <summary>
        /// Read the text contents of the file.
        /// </summary>
        public string ReadText(string encoding = "utf-8")
        {
            if (!File.Exists(_path))
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            return File.ReadAllText(_path, GetEncoding(encoding));
        }

        /// <summary>
        /// Write text to the file, overwriting existing content.
        /// </summary>
        public void WriteText(string data, string encoding = "utf-8")
        {
            File.WriteAllText(_path, data, GetEncoding(encoding));
        }

        /// <summary>
        /// Read the binary contents of the file.
        /// </summary>
        public byte[] ReadBytes()
        {
            if (!File.Exists(_path))
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            return File.ReadAllBytes(_path);
        }

        /// <summary>
        /// Write bytes to the file, overwriting existing content.
        /// </summary>
        public void WriteBytes(byte[] data)
        {
            File.WriteAllBytes(_path, data);
        }

        // --- Directory operations ---

        /// <summary>
        /// Create the directory. If parents is true, create parent directories as needed.
        /// </summary>
        public void Mkdir(bool parents = false, bool exist_ok = false)
        {
            if (Directory.Exists(_path))
            {
                if (!exist_ok)
                    throw new FileExistsError("File exists: '" + _path + "'");
                return;
            }

            if (parents)
            {
                Directory.CreateDirectory(_path);
            }
            else
            {
                string? parent = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                    throw new FileNotFoundError("No such file or directory: '" + _path + "'");
                Directory.CreateDirectory(_path);
            }
        }

        /// <summary>
        /// Remove this directory. The directory must be empty.
        /// </summary>
        public void Rmdir()
        {
            if (!Directory.Exists(_path))
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");
            try
            {
                Directory.Delete(_path, false);
            }
            catch (IOException ex)
            {
                throw new OSError("Directory not empty: '" + _path + "'", ex);
            }
        }

        /// <summary>
        /// Iterate over the files in this directory.
        /// </summary>
        public IEnumerable<Path> Iterdir()
        {
            if (!Directory.Exists(_path))
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");

            foreach (var entry in Directory.GetFileSystemEntries(_path))
            {
                yield return new Path(entry);
            }
        }

        /// <summary>
        /// Glob the given relative pattern in this directory, yielding matching paths.
        /// Supports simple patterns like "*.txt" and "**/*.txt".
        /// </summary>
        public IEnumerable<Path> Glob(string pattern)
        {
            if (!Directory.Exists(_path))
                throw new FileNotFoundError("No such file or directory: '" + _path + "'");

            bool recursive = pattern.StartsWith("**/");
            string searchPattern = recursive ? pattern.Substring(3) : pattern;
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var entry in Directory.GetFileSystemEntries(_path, searchPattern, option))
            {
                yield return new Path(entry);
            }
        }

        // --- Mutation ---

        /// <summary>
        /// Rename this file or directory to the given target.
        /// </summary>
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

        /// <summary>
        /// Remove this file. If missing_ok is false, raises FileNotFoundError if the file does not exist.
        /// </summary>
        public void Unlink(bool missing_ok = false)
        {
            if (!File.Exists(_path))
            {
                if (Directory.Exists(_path))
                    throw new IsADirectoryError("Is a directory: '" + _path + "'");
                if (!missing_ok)
                    throw new FileNotFoundError("No such file or directory: '" + _path + "'");
                return;
            }
            File.Delete(_path);
        }

        /// <summary>
        /// Rename this file or directory to the given target, replacing if it exists.
        /// </summary>
        public Path Replace(string target)
        {
            if (File.Exists(target))
                File.Delete(target);
            return Rename(target);
        }

        // --- Navigation ---

        /// <summary>
        /// Make the path absolute, resolving any symlinks.
        /// </summary>
        public Path Resolve()
        {
            return new Path(System.IO.Path.GetFullPath(_path));
        }

        /// <summary>
        /// Return a new path with the file name changed.
        /// </summary>
        public Path WithName(string name)
        {
            if (string.IsNullOrEmpty(Name))
                throw new ValueError("Path has an empty name");
            string? dir = System.IO.Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(dir))
                return new Path(name);
            return new Path(System.IO.Path.Combine(dir, name));
        }

        /// <summary>
        /// Return a new path with the stem changed.
        /// </summary>
        public Path WithStem(string stem)
        {
            return WithName(stem + Suffix);
        }

        /// <summary>
        /// Return a new path with the suffix changed.
        /// </summary>
        public Path WithSuffix(string suffix)
        {
            if (string.IsNullOrEmpty(Name))
                throw new ValueError("Path has an empty name");
            return WithName(Stem + suffix);
        }

        /// <summary>
        /// Return a relative path from this path to other.
        /// </summary>
        public Path RelativeTo(Path other)
        {
            string thisNorm = System.IO.Path.GetFullPath(_path);
            string otherNorm = System.IO.Path.GetFullPath(other._path);

            if (!thisNorm.StartsWith(otherNorm, StringComparison.Ordinal))
                throw new ValueError("'" + _path + "' is not in the subpath of '" + other._path + "'");

            string relative = thisNorm.Substring(otherNorm.Length);
            if (relative.StartsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                relative = relative.Substring(1);
            if (relative.StartsWith(System.IO.Path.AltDirectorySeparatorChar.ToString()))
                relative = relative.Substring(1);

            return new Path(string.IsNullOrEmpty(relative) ? "." : relative);
        }

        // --- Object overrides ---

        public override string ToString()
        {
            return _path;
        }

        public override bool Equals(object? obj)
        {
            return obj is Path other && Equals(other);
        }

        public bool Equals(Path? other)
        {
            if (other is null)
                return false;
            return string.Equals(_path, other._path, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(_path);
        }

        public static bool operator ==(Path? left, Path? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Path? left, Path? right)
        {
            return !(left == right);
        }

        private static Encoding GetEncoding(string name)
        {
            switch (name.ToLowerInvariant())
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
                    try
                    {
                        return Encoding.GetEncoding(name);
                    }
                    catch (ArgumentException)
                    {
                        throw new ValueError("unknown encoding: '" + name + "'");
                    }
            }
        }
    }
}
