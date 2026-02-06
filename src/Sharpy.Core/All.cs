using System.Collections.Generic;
namespace Sharpy.Core
{
    using System.Linq;

    public static partial class Builtins
    {
        /// <summary>
        /// Return True if all elements of the iterable are true (or if the iterable is empty).
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterable</typeparam>
        /// <param name="iterable">The iterable to check</param>
        /// <returns>True if all elements are truthy, False otherwise</returns>
        public static bool All<T>(IEnumerable<T> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.ArgNone("all", "iterable");
            }

            return iterable.All(item => Bool(item));
        }
    }
}
