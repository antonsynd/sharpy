using System.Linq;
using System;
namespace Sharpy.Core
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return True if class is a subclass of classinfo. A class is considered
        /// a subclass of itself.
        /// </summary>
        /// <param name="cls">The class to check</param>
        /// <param name="classInfo">The base class to check against</param>
        /// <returns>True if cls is a subclass of classInfo, False otherwise</returns>
        public static bool Issubclass(Type cls, Type classInfo)
        {
            if (cls is null)
            {
                throw TypeError.ArgNone("issubclass", "cls");
            }

            if (classInfo is null)
            {
                throw TypeError.ArgNone("issubclass", "classinfo");
            }

            return classInfo.IsAssignableFrom(cls);
        }

        /// <summary>
        /// Return True if class is a subclass of any of the types in classInfo.
        /// </summary>
        /// <param name="cls">The class to check</param>
        /// <param name="classInfo">A tuple of types to check against</param>
        /// <returns>True if cls is a subclass of any type in classInfo, False otherwise</returns>
        public static bool Issubclass(Type cls, params Type[] classInfo)
        {
            if (cls is null)
            {
                throw TypeError.ArgNone("issubclass", "cls");
            }

            if (classInfo is null || classInfo.Length == 0)
            {
                throw TypeError.ArgNone("issubclass", "classinfo");
            }

            return classInfo.Any(type => type is not null && type.IsAssignableFrom(cls));
        }
    }
}
