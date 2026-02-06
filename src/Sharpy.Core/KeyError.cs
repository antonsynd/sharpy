using System;

namespace Sharpy
{
    public class KeyError : Exception
    {
        public KeyError(string message) : base(message) { }
    }
}
