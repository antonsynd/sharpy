using System;
namespace Sharpy
{
    /// <summary>
    /// Represents an immutable sequence of bytes (similar to Python's bytes type).
    /// </summary>
    public readonly struct Bytes
    {
        private readonly byte[] _data;

        public Bytes(byte[] data)
        {
            _data = data ?? System.Array.Empty<byte>();
        }

        public int Length => _data.Length;

        public byte this[int index] => _data[index];

        public byte[] ToArray() => _data;

        public override string ToString()
        {
#pragma warning disable CA1307 // string.Replace(string, string, StringComparison) not available in netstandard2.0
            return $"b'{BitConverter.ToString(_data).Replace("-", " ")}'";
#pragma warning restore CA1307
        }
    }
}
