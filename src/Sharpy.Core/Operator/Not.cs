using System.Collections.Generic;
using System.Collections;

namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return the logical negation of a boolean value.</summary>
        public static bool Not(bool value)
        {
            return !value;
        }

        /// <summary>Return true if the collection is empty.</summary>
        public static bool Not(System.Collections.ICollection collection)
        {
            return collection.Count == 0;
        }

        /// <summary>Return true if the collection is empty.</summary>
        public static bool Not<T>(ICollection<T> collection)
        {
            return collection.Count == 0;
        }
    }
}
