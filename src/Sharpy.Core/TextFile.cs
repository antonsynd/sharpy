using System;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A Python-like file object supporting read, write, and context manager (IDisposable) patterns.
    /// </summary>
    public class TextFile : IDisposable
    {
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _disposed;

        /// <summary>The file path.</summary>
        public string Name { get; }

        /// <summary>The mode string ("r", "w", "a", or "x").</summary>
        public string Mode { get; }

        /// <summary>Whether the file has been closed.</summary>
        public bool Closed { get; private set; }

        private bool IsReadMode => Mode == "r";

        internal TextFile(string path, string mode, Encoding encoding)
        {
            Name = path;
            Mode = mode;

            switch (mode)
            {
                case "r":
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundError("No such file or directory: '" + path + "'");
                    }
                    if (Directory.Exists(path))
                    {
                        throw new IsADirectoryError("Is a directory: '" + path + "'");
                    }
                    _reader = new StreamReader(path, encoding);
                    break;
                case "w":
                    EnsureNotDirectory(path);
                    _writer = new StreamWriter(path, false, encoding);
                    break;
                case "a":
                    EnsureNotDirectory(path);
                    _writer = new StreamWriter(path, true, encoding);
                    break;
                case "x":
                    if (File.Exists(path))
                    {
                        throw new FileExistsError("File exists: '" + path + "'");
                    }
                    EnsureNotDirectory(path);
                    // FileMode.CreateNew ensures atomicity
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                    _writer = new StreamWriter(stream, encoding);
                    break;
                default:
                    throw new ValueError("invalid mode: '" + mode + "'");
            }
        }

        private static void EnsureNotDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                throw new IsADirectoryError("Is a directory: '" + path + "'");
            }
        }

        private void EnsureOpen()
        {
            if (Closed)
            {
                throw new ValueError("I/O operation on closed file.");
            }
        }

        private void EnsureReadable()
        {
            EnsureOpen();
            if (!IsReadMode)
            {
                throw new IOError("not readable");
            }
        }

        private void EnsureWritable()
        {
            EnsureOpen();
            if (IsReadMode)
            {
                throw new IOError("not writable");
            }
        }

        /// <summary>Read the entire remaining contents of the file.</summary>
        public string Read()
        {
            EnsureReadable();
            return _reader!.ReadToEnd();
        }

        /// <summary>Read at most <paramref name="size"/> characters.</summary>
        public string Read(int size)
        {
            EnsureReadable();
            if (size < 0)
            {
                return _reader!.ReadToEnd();
            }
            var buffer = new char[size];
            int count = _reader!.Read(buffer, 0, size);
            return new string(buffer, 0, count);
        }

        /// <summary>
        /// Read a single line, including the trailing newline character if present.
        /// Returns an empty string at EOF.
        /// </summary>
        public string Readline()
        {
            EnsureReadable();
            var sb = new StringBuilder();
            int ch;
            while ((ch = _reader!.Read()) != -1)
            {
                sb.Append((char)ch);
                if (ch == '\n')
                {
                    break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Read all remaining lines. Each line includes its trailing newline if present.
        /// </summary>
        public List<string> Readlines()
        {
            EnsureReadable();
            var lines = new List<string>();
            string line;
            while ((line = Readline()).Length > 0)
            {
                lines.Append(line);
            }
            return lines;
        }

        /// <summary>
        /// Write a string to the file. Returns the number of characters written.
        /// </summary>
        public int Write(string s)
        {
            EnsureWritable();
            _writer!.Write(s);
            return s.Length;
        }

        /// <summary>
        /// Write an iterable of strings to the file. No separator is added.
        /// </summary>
        public void Writelines(System.Collections.Generic.IEnumerable<string> lines)
        {
            EnsureWritable();
            foreach (var line in lines)
            {
                _writer!.Write(line);
            }
        }

        /// <summary>Flush the write buffer.</summary>
        public void Flush()
        {
            EnsureOpen();
            if (_writer != null)
            {
                _writer.Flush();
            }
        }

        /// <summary>Close the file.</summary>
        public void Close()
        {
            if (!Closed)
            {
                Closed = true;
                if (_reader != null)
                {
                    _reader.Dispose();
                    _reader = null;
                }
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }
            }
        }

        /// <summary>Dispose the file (called by 'with' / 'using' statements).</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Close();
            }
        }
    }
}
