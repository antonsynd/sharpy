using System;
namespace Sharpy
{
    /// <summary>
    /// Represents an immutable sequence of bytes (similar to Python's bytes type).
    /// </summary>
    public readonly struct Bytes
    {
        private readonly byte[] _data;

        /// <summary>Create a Bytes instance from a byte array.</summary>
        public Bytes(byte[] data)
        {
            _data = data ?? System.Array.Empty<byte>();
        }

        /// <summary>Gets the number of bytes.</summary>
        public int Length => _data.Length;

        /// <summary>Gets the byte at the specified index.</summary>
        public byte this[int index] => _data[index];

        /// <summary>Return the underlying byte array.</summary>
        public byte[] ToArray() => _data;

        /// <summary>Returns a string representation of the bytes.</summary>
        public override string ToString()
        {
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            return $"b'{BitConverter.ToString(_data).Replace("-", " ")}'";
#pragma warning restore CA1307
        }
    }
}
