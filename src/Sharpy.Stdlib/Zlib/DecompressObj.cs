using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
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

        public Bytes UnconsumedTail => _unconsumedTail;

        public bool Eof => _finished;
    }
}
