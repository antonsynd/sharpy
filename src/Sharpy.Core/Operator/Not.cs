using System.Collections.Generic;
using System.Collections;

namespace Sharpy
{
    public static partial class Operator
    {
        public static bool Not(bool value)
        {
            return !value;
        }

        public static bool Not(System.Collections.ICollection collection)
        {
            return collection.Count == 0;
        }

        public static bool Not<T>(ICollection<T> collection)
        {
            return collection.Count == 0;
        }
    }
}
