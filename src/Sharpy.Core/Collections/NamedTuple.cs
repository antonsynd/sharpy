using System;

namespace Sharpy
{
    public static partial class Collections
    {
        /// <summary>
        /// Creates a new namedtuple type with the given name and fields.
        /// This is a compile-time construct — the compiler detects the namedtuple
        /// pattern and generates a record class. This method is never called at runtime.
        /// </summary>
        /// <param name="name">The name of the namedtuple type.</param>
        /// <param name="fields">The field names for the namedtuple.</param>
        /// <returns>The generated type (compile-time only).</returns>
        public static Type Namedtuple(string name, List<string> fields)
        {
            throw new NotImplementedException(
                "namedtuple is a compile-time construct and should not be called at runtime");
        }
    }
}
