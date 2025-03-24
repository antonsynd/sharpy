using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the length (the number of items) of an object. The argument
        /// may be a sequence (such as a string, bytes, tuple, list, or range)
        /// or a collection (such as a dictionary, set, or frozen set).
        /// </summary>
        public static uint Len(Sized sized)
        {
            if (sized is null) {
                throw new TypeError("Len() sized argument cannot be None");
            }

            return sized.__Len__();
        }

        public static uint Len(string s)
        {
            return (uint)s.Length;
        }
    }
}
