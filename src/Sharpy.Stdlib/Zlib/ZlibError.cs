using System;

namespace Sharpy
{
    [SharpyModuleType("zlib", "error")]
    public class ZlibError : Exception
    {
        public ZlibError(string message) : base(message) { }
    }
}
