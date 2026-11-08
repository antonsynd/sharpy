using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents an immutable sequence of bytes (similar to Python's bytes type).
    /// Supports negative indexing, slicing, and Python-style methods.
    /// </summary>
    public readonly partial struct Bytes : IEquatable<Bytes>, IComparable<Bytes>, ISized, IBoolConvertible, IEnumerable<int>
    {
        private readonly byte[] _storage;

        /// <summary>
        /// Null-safe view of the backing array. <c>default(Bytes)</c> (and the parameterless
        /// struct ctor that <c>bytes()</c> lowers to) leaves <see cref="_storage"/> null, so all
        /// reads go through this property to behave like an empty byte sequence.
        /// </summary>
        private byte[] _data => _storage ?? System.Array.Empty<byte>();

        /// <summary>Create a Bytes instance from a byte array (copies the array).</summary>
        public Bytes(byte[] data)
        {
            _storage = data != null && data.Length > 0
                ? (byte[])data.Clone()
                : System.Array.Empty<byte>();
        }

        /// <summary>
        /// Create a zero-filled Bytes instance of the given length (Python's bytes(n)).
        /// </summary>
        public Bytes(int length)
        {
            if (length < 0)
            {
                throw new ValueError("negative count");
            }

            _storage = length > 0 ? new byte[length] : System.Array.Empty<byte>();
        }

        /// <summary>
        /// Create a Bytes instance from an iterable of ints (Python's bytes([...])).
        /// Each value must be in range(0, 256); otherwise a ValueError is thrown.
        /// </summary>
        public Bytes(IEnumerable<int> values)
        {
            if (values == null)
            {
                _storage = System.Array.Empty<byte>();
                return;
            }

            var buffer = new System.Collections.Generic.List<byte>();
            foreach (int value in values)
            {
                if (value < 0 || value > 255)
                {
                    throw new ValueError("bytes must be in range(0, 256)");
                }

                buffer.Add((byte)value);
            }

            _storage = buffer.Count > 0 ? buffer.ToArray() : System.Array.Empty<byte>();
        }

        /// <summary>
        /// Wrap an already-owned byte array without copying (internal use only).
        /// Callers must not retain or mutate <paramref name="ownedData"/> after calling this.
        /// </summary>
        internal static Bytes Wrap(byte[] ownedData)
        {
            return new Bytes(ownedData, wrap: true);
        }

        private Bytes(byte[] data, bool wrap)
        {
            _storage = data ?? System.Array.Empty<byte>();
        }

        /// <summary>Gets the number of bytes.</summary>
        public int Length => _data.Length;

        /// <summary>Gets the byte value at the specified index as an int. Supports negative indexing.</summary>
        public int this[int index]
        {
            get
            {
                byte[] data = _data;
                int n = data.Length;

                if ((uint)index < (uint)n)
                {
                    return data[index];
                }

                // Slow path: a negative index in [-n, 0) counts from the end (empty bytes never matches).
                if (index < 0 && index >= -n)
                {
                    return data[index + n];
                }

                throw Sharpy.Index.BytesOutOfRange();
            }
        }

        /// <summary>Return a copy of the underlying byte array.</summary>
        public byte[] ToArray() => (byte[])_data.Clone();

        /// <summary>Returns a string representation of the bytes.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder("b'");
            foreach (byte b in _data)
            {
                if (b >= 32 && b < 127 && b != (byte)'\\' && b != (byte)'\'')
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append("\\x");
                    sb.Append(b.ToString("x2"));
                }
            }
            sb.Append('\'');
            return sb.ToString();
        }

        /// <summary>Return the hex string representation of the bytes.</summary>
        public string Hex(string? sep = null, int bytesPerSep = 1)
        {
            if (_data.Length == 0)
            {
                return "";
            }

            if (sep == null || sep.Length == 0)
            {
                var sb = new StringBuilder(_data.Length * 2);
                foreach (byte b in _data)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }

            if (bytesPerSep < 1)
            {
                bytesPerSep = 1;
            }

            // Python groups from the right, so the first group may be smaller
            int firstGroupSize = _data.Length % bytesPerSep;
            if (firstGroupSize == 0)
            {
                firstGroupSize = bytesPerSep;
            }

            var result = new StringBuilder();
            for (int i = 0; i < _data.Length; i++)
            {
                if (i > 0 && (i == firstGroupSize || (i - firstGroupSize) % bytesPerSep == 0))
                {
                    result.Append(sep);
                }
                result.Append(_data[i].ToString("x2"));
            }
            return result.ToString();
        }

        /// <summary>
        /// Decode the bytes to a string using the specified encoding. An undecodable byte run raises
        /// <see cref="UnicodeDecodeError"/> under <c>errors="strict"</c>, becomes U+FFFD under
        /// <c>"replace"</c> and is dropped under <c>"ignore"</c>; any other handler name raises
        /// <see cref="LookupError"/> when an error occurs (#2477).
        /// Python: <c>bytes.decode(encoding='utf-8', errors='strict')</c>
        /// </summary>
        public string Decode(string encoding = "utf-8", string errors = "strict")
        {
            return UnicodeCodec.Decode(this, _data, encoding, errors);
        }

        #region ISized

        /// <summary>Gets the number of bytes for len() dispatch.</summary>
        int ISized.Count => _data.Length;

        #endregion

        #region IBoolConvertible

        /// <summary>Returns true if the bytes sequence is non-empty.</summary>
        bool IBoolConvertible.IsTrue => _data.Length > 0;

        #endregion

        #region IEnumerable

        /// <summary>Iterates byte values as integers (Python semantics).</summary>
        public IEnumerator<int> GetEnumerator()
        {
            foreach (byte b in _data)
            {
                yield return b;
            }
        }

        /// <summary>Non-generic enumerator.</summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
