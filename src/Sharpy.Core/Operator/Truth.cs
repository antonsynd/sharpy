using System.Collections.Generic;
using System.Collections;

namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return the truth value of a boolean.</summary>
        public static bool Truth(bool value)
        {
            return value;
        }

        /// <summary>Return true if the collection is non-empty.</summary>
        public static bool Truth(System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        /// <summary>Return true if the collection is non-empty.</summary>
        public static bool Truth<T>(ICollection<T> collection)
        {
            return collection.Count > 0;
        }
    }
}
