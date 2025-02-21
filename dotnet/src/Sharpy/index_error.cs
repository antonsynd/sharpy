using System;

namespace Sharpy
{
    public class IndexError : Exception
    {
        public IndexError(string message) : base(message) { }
    }
}
