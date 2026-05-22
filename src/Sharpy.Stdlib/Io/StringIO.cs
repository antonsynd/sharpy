using System;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// In-memory text stream using a string buffer, similar to Python's io.StringIO.
    /// Extends TextWriter so it can be used anywhere a TextWriter is expected (e.g., csv module).
    /// </summary>
    [SharpyModuleType("io")]
    public sealed class StringIO : TextWriter
    {
        private StringBuilder _buffer;
        private int _position;
        private bool _closed;

        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>
        /// Create a new StringIO instance with optional initial content.
        /// </summary>
        /// <param name="initial">Optional initial content for the buffer.</param>
        public StringIO(string? initial = null)
        {
            _buffer = new StringBuilder(initial ?? "");
            _position = 0;
            _closed = false;
        }

        /// <summary>
        /// Write a single character to the buffer at the current position.
        /// This override satisfies the TextWriter contract.
        /// </summary>
        public override void Write(char value)
        {
            ThrowIfClosed();

            if (_position == _buffer.Length)
            {
                _buffer.Append(value);
            }
            else if (_position > _buffer.Length)
            {
                int padding = _position - _buffer.Length;
                _buffer.Append(new string('\0', padding));
                _buffer.Append(value);
            }
            else
            {
                _buffer[_position] = value;
            }

            _position++;
        }

        /// <summary>
        /// Write a string to the buffer at the current position.
        /// Returns the number of characters written (Python semantics).
        /// Hides the base TextWriter.Write(string?) which returns void.
        /// When called through a TextWriter reference, the base Write(string?) delegates
        /// to Write(char) which correctly updates the buffer, but the return value
        /// (character count) is silently discarded. Direct StringIO callers get the count;
        /// TextWriter callers do not.
        /// </summary>
        /// <param name="s">The string to write.</param>
        /// <returns>The number of characters written.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public new int Write(string s)
        {
            ThrowIfClosed();
            if (s == null)
            {
                throw new TypeError("string argument expected, got 'NoneType'");
            }

            int length = s.Length;

            if (_position == _buffer.Length)
            {
                _buffer.Append(s);
            }
            else if (_position > _buffer.Length)
            {
                // Pad with null characters if position is past end
                int padding = _position - _buffer.Length;
                _buffer.Append(new string('\0', padding));
                _buffer.Append(s);
            }
            else
            {
                // Overwrite at position
                int overwriteLen = System.Math.Min(length, _buffer.Length - _position);
                for (int i = 0; i < overwriteLen; i++)
                {
                    _buffer[_position + i] = s[i];
                }

                if (length > overwriteLen)
                {
                    _buffer.Append(s.Substring(overwriteLen));
                }
            }

            _position += length;
            return length;
        }

        /// <summary>
        /// Read from the buffer starting at the current position.
        /// </summary>
        /// <param name="n">Number of characters to read. -1 reads all remaining.</param>
        /// <returns>The string read from the buffer.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public string Read(int n = -1)
        {
            ThrowIfClosed();

            if (_position >= _buffer.Length)
            {
                return "";
            }

            int remaining = _buffer.Length - _position;
            int count = (n < 0) ? remaining : System.Math.Min(n, remaining);
            string result = _buffer.ToString(_position, count);
            _position += count;
            return result;
        }

        /// <summary>
        /// Read a single line from the current position (up to and including the newline).
        /// </summary>
        /// <returns>The line read, including the trailing newline if present.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public string Readline()
        {
            ThrowIfClosed();

            if (_position >= _buffer.Length)
            {
                return "";
            }

            string content = _buffer.ToString();
            int newlineIndex = content.IndexOf('\n', _position);

            if (newlineIndex < 0)
            {
                // No newline found, read to end
                string result = content.Substring(_position);
                _position = _buffer.Length;
                return result;
            }
            else
            {
                // Include the newline character
                int count = newlineIndex - _position + 1;
                string result = content.Substring(_position, count);
                _position = newlineIndex + 1;
                return result;
            }
        }

        /// <summary>
        /// Set the stream position.
        /// </summary>
        /// <param name="pos">The new position.</param>
        /// <returns>The new absolute position.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed or position is negative.</exception>
        public int Seek(int pos)
        {
            ThrowIfClosed();
            if (pos < 0)
            {
                throw new ValueError("Negative seek position " + pos);
            }

            _position = pos;
            return _position;
        }

        /// <summary>
        /// Return the current stream position.
        /// </summary>
        /// <returns>The current position in the stream.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public int Tell()
        {
            ThrowIfClosed();
            return _position;
        }

        /// <summary>
        /// Return the entire contents of the buffer regardless of position.
        /// </summary>
        /// <returns>The complete buffer content.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public string Getvalue()
        {
            ThrowIfClosed();
            return _buffer.ToString();
        }

        /// <summary>
        /// Truncate the buffer at the given size, or at the current position if size is -1.
        /// </summary>
        /// <param name="size">The size to truncate to, or -1 for current position.</param>
        /// <returns>The new size of the buffer.</returns>
        /// <exception cref="ValueError">Thrown if the stream is closed.</exception>
        public int Truncate(int size = -1)
        {
            ThrowIfClosed();
            int truncateAt = (size < 0) ? _position : size;

            if (truncateAt < _buffer.Length)
            {
                _buffer.Remove(truncateAt, _buffer.Length - truncateAt);
            }
            else if (truncateAt > _buffer.Length)
            {
                // Extend with null characters
                _buffer.Append(new string('\0', truncateAt - _buffer.Length));
            }

            return _buffer.Length;
        }

        /// <summary>
        /// Mark the stream as closed. Further operations will raise ValueError.
        /// </summary>
        public override void Close()
        {
            _closed = true;
            base.Close();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _closed = true;
            base.Dispose(disposing);
        }

        private void ThrowIfClosed()
        {
            if (_closed)
            {
                throw new ValueError("I/O operation on closed file.");
            }
        }
    }
}
