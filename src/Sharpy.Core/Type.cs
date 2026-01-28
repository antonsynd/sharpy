using System;
namespace Sharpy.Core
{
    public static partial class Exports
    {
        /// <summary>
        /// Return the type of an object.
        /// </summary>
        /// <param name="obj">The object to get the type of</param>
        /// <returns>The type of the object</returns>
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
