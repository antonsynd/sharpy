using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static T Min<T>(Iterable<LessThanComparable<T>> s)
        {
            throw new ValueError("Min() iterable argument is empty");
        }

        public static T Min<T>(Iterable<GreaterThanComparable<T>> s)
        {
            throw new ValueError("Min() iterable argument is empty");
        }
    }
}
