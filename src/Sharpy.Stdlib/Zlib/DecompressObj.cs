using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>
    /// Streaming decompression object, matching Python's zlib.decompressobj().
    /// </summary>
    [SharpyModuleType("zlib")]
    public sealed class DecompressObj
    {
        private MemoryStream? _buffer;
        private bool _finished;

        internal DecompressObj()
        {
            _buffer = new MemoryStream();
            _finished = false;
        }

        /// <summary>
        /// Decompress data and return a bytes object with at least part of the decompressed data.
        /// Unlike CPython's incremental output, this implementation buffers all input and
        /// returns decompressed data only when <see cref="Flush"/> is called.
        /// </summary>
        /// <param name="data">The compressed data to decompress.</param>
        /// <returns>Empty array (data is buffered until Flush).</returns>
        public byte[] Decompress(byte[] data)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            if (_finished)
            {
                throw new ValueError("decompressobj stream is already finished");
            }

            _buffer!.Write(data, 0, data.Length);
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Flush the decompressor and return any remaining decompressed data.
        /// </summary>
        /// <returns>The final decompressed bytes.</returns>
        public byte[] Flush()
        {
            if (_finished)
            {
                throw new ValueError("decompressobj stream is already finished");
            }

            _finished = true;
            byte[] input = _buffer!.ToArray();
            _buffer.Dispose();
            _buffer = null;

            return ZlibModule.DecompressBytes(input);
        }
    }
}
