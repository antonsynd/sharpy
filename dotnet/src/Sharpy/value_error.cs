using System;

namespace Sharpy
{
    public class ValueError : Exception
    {
        public ValueError(string message) : base(message) { }
    }
}
