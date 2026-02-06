using System.Collections.Generic;
namespace Sharpy.Core
{
    using System.Linq;

    public static partial class Builtins
    {
        /// <summary>
        /// Return True if any element of the iterable is true. If the iterable is empty, return False.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to check</param>
        /// <returns>True if any element is truthy, False otherwise</returns>
        public static bool Any<T>(IEnumerable<T> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("any", "iterable");
            }

            return iterable.Any(item => Bool(item));
        }
    }
}
