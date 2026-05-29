using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>
    /// Streaming compression object, matching Python's zlib.compressobj().
    /// </summary>
    [SharpyModuleType("zlib")]
    public sealed class CompressObj
    {
        private readonly CompressionLevel _level;
        private MemoryStream? _buffer;
        private bool _flushed;

        internal CompressObj(CompressionLevel level)
        {
            _level = level;
            _buffer = new MemoryStream();
            _flushed = false;
        }

        /// <summary>
        /// Compress data and return a bytes object with at least part of the compressed data.
        /// </summary>
        /// <param name="data">The data to compress.</param>
        /// <returns>Compressed bytes (may be empty if buffered internally).</returns>
        public byte[] Compress(byte[] data)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            if (_flushed)
            {
                throw new ValueError("compressobj is already flushed");
            }

            _buffer!.Write(data, 0, data.Length);
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Flush all pending input and return the remaining compressed output.
        /// </summary>
        /// <returns>The final compressed bytes.</returns>
        public byte[] Flush()
        {
            if (_flushed)
            {
                throw new ValueError("compressobj is already flushed");
            }

            _flushed = true;
            byte[] input = _buffer!.ToArray();
            _buffer.Dispose();
            _buffer = null;

            return ZlibModule.CompressBytes(input, _level);
        }
    }
}
