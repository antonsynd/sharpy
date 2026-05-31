using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides incremental decompression like zlib.decompressobj.</summary>
    [SharpyModuleType("zlib")]
    public sealed class DecompressObj
    {
        private readonly int _wbits;
        private MemoryStream _buffer;
        private bool _finished;
        private Bytes _unconsumedTail;

        internal DecompressObj(int wbits)
        {
            _wbits = wbits;
            _buffer = new MemoryStream();
            _finished = false;
            _unconsumedTail = new Bytes(Array.Empty<byte>());
        }

        /// <summary>Buffers compressed data for later decompression.</summary>
        public Bytes Decompress(Bytes data, int maxLength = 0)
        {
            if (_finished)
            {
                throw new ZlibError("decompressobj stream is already finished");
            }

            byte[] bytes = data.ToArray();
            _buffer.Write(bytes, 0, bytes.Length);
            return new Bytes(Array.Empty<byte>());
        }

        /// <summary>Finishes decompression and returns the remaining output.</summary>
        public Bytes Flush(int length = 16384)
        {
            if (_finished)
            {
                throw new ZlibError("decompressobj stream is already finished");
            }

            _finished = true;
            byte[] input = _buffer.ToArray();
            _buffer.Dispose();

            if (_wbits < 0)
            {
                return ZlibModule.DecompressRaw(input);
            }

            if (_wbits > 16)
            {
                return ZlibModule.DecompressGzip(input);
            }

            return ZlibModule.DecompressZlib(input);
        }

        /// <summary>Gets compressed data that was not consumed.</summary>
        public Bytes UnconsumedTail => _unconsumedTail;

        /// <summary>Gets a value indicating whether the stream has been finished.</summary>
        public bool Eof => _finished;
    }
}
