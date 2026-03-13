using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the type of an object.
        /// </summary>
        /// <param name="obj">The object to get the type of</param>
        /// <returns>The type of the object</returns>
        /// <example>
        /// <code>
        /// type(42)        # &lt;class 'int'&gt;
        /// type("hello")   # &lt;class 'str'&gt;
        /// type([1, 2])    # &lt;class 'list'&gt;
        /// </code>
        /// </example>
        public static Type Type(object? obj)
        {
            if (obj is null)
            {
                // In Python, type(None) returns NoneType. In C#, we use typeof(object) as
                // there's no direct NoneType equivalent since null represents None.
                return typeof(object);
            }

            return obj.GetType();
        }
    }
}
