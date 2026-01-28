using System;

namespace Sharpy.Core
{
    public class KeyError : Exception
    {
        public KeyError(string message) : base(message) { }
    }
}
