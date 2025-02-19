using System;

namespace Sharpy.Stdlib
{
    public class ValueError : Exception
    {
        public ValueError(string message) : base(message) { }
    }
}
