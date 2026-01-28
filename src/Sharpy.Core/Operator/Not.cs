using System.Collections.Generic;
using System.Collections;
namespace Sharpy.Operator
{
    public static partial class Exports
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
