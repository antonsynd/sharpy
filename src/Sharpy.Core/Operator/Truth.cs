using System.Collections.Generic;
using System.Collections;
namespace Sharpy
{
    public static partial class Operator
    {
        public static bool Truth(bool value)
        {
            return value;
        }

        public static bool Truth(System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        public static bool Truth<T>(ICollection<T> collection)
        {
            return collection.Count > 0;
        }
    }
}
