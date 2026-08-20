using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>Construct an empty bytes object.</summary>
        public static Bytes Bytes()
        {
            return new Bytes(0);
        }

        /// <summary>Construct a bytes object of the given size, filled with zero bytes.</summary>
        public static Bytes Bytes(int size)
        {
            return new Bytes(size);
        }

        /// <summary>Construct a bytes object from an iterable of ints.</summary>
        public static Bytes Bytes(IEnumerable<int> source)
        {
            return new Bytes(source);
        }
    }
}
