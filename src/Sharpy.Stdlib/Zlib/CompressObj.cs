using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides incremental compression like zlib.compressobj.</summary>
    [SharpyModuleType("zlib")]
    public sealed class CompressObj
    {
        private readonly CompressionLevel _level;
        private readonly int _wbits;
        private MemoryStream _buffer;
        private bool _flushed;

        internal CompressObj(CompressionLevel level, int wbits)
        {
            _level = level;
            _wbits = wbits;
            _buffer = new MemoryStream();
            _flushed = false;
        }

        /// <summary>Buffers data for later compression.</summary>
        public Bytes Compress(Bytes data)
        {
            if (_flushed)
            {
                throw new ZlibError("compressobj is already flushed");
            }

            byte[] bytes = data.ToArray();
            _buffer.Write(bytes, 0, bytes.Length);
            return new Bytes(Array.Empty<byte>());
        }

        /// <summary>Finishes compression and returns the compressed output.</summary>
        public Bytes Flush(int mode = 4)
        {
            if (_flushed)
            {
                throw new ZlibError("compressobj is already flushed");
            }

            _flushed = true;
            byte[] input = _buffer.ToArray();
            _buffer.Dispose();

            return ZlibModule.CompressBytes(input, _level);
        }
    }
}
