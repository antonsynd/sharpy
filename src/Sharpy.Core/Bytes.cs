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
    public readonly partial struct Bytes : IEquatable<Bytes>, ISized, IBoolConvertible, IEnumerable<int>
    {
        private readonly byte[] _data;

        /// <summary>Create a Bytes instance from a byte array (copies the array).</summary>
        public Bytes(byte[] data)
        {
            _data = data != null && data.Length > 0
                ? (byte[])data.Clone()
                : System.Array.Empty<byte>();
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
            _data = data ?? System.Array.Empty<byte>();
        }

        /// <summary>Gets the number of bytes.</summary>
        public int Length => _data.Length;

        /// <summary>Gets the byte value at the specified index as an int. Supports negative indexing.</summary>
        public int this[int index]
        {
            get
            {
                if (_data.Length == 0)
                {
                    throw new IndexError("index out of range");
                }

                if (index < 0)
                {
                    index = _data.Length + index;
                }

                if (index < 0 || index >= _data.Length)
                {
                    throw new IndexError("index out of range");
                }

                return _data[index];
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

        /// <summary>Decode the bytes to a string using the specified encoding.</summary>
        public string Decode(string encoding = "utf-8")
        {
#pragma warning disable CA1307
            switch (encoding.ToLowerInvariant().Replace("-", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    return Encoding.UTF8.GetString(_data);
                case "ascii":
                    return Encoding.ASCII.GetString(_data);
                case "latin1":
                case "iso88591":
                    return Encoding.GetEncoding("iso-8859-1").GetString(_data);
                case "utf16":
                case "utf16le":
                    return Encoding.Unicode.GetString(_data);
                case "utf16be":
                    return Encoding.BigEndianUnicode.GetString(_data);
                case "utf32":
                    return Encoding.UTF32.GetString(_data);
                default:
                    throw new ValueError("unknown encoding: " + encoding);
            }
        }

        /// <summary>Create a Bytes instance from a hex string.</summary>
        public static Bytes Fromhex(string hexString)
        {
            if (hexString == null)
            {
                throw new ValueError("non-hexadecimal number found in fromhex() arg");
            }

#pragma warning disable CA1307
            var clean = hexString.Replace(" ", "");
#pragma warning restore CA1307

            if (clean.Length % 2 != 0)
            {
                throw new ValueError("non-hexadecimal number found in fromhex() arg at position " + clean.Length);
            }

            var data = new byte[clean.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                var hexByte = clean.Substring(i * 2, 2);
                try
                {
                    data[i] = Convert.ToByte(hexByte, 16);
                }
                catch (FormatException)
                {
                    throw new ValueError("non-hexadecimal number found in fromhex() arg at position " + (i * 2));
                }
            }

            return Bytes.Wrap(data);
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
