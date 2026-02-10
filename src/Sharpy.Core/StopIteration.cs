using System;

namespace Sharpy
{
    public class StopIteration : Exception
    {
        public StopIteration() : base("StopIteration") { }
        public StopIteration(string message) : base(message) { }
    }
}
