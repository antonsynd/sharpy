using System;

namespace Sharpy.Stdlib
{
    public class IndexError : Exception
    {
        public IndexError(string message) : base(message) { }
    }
}
