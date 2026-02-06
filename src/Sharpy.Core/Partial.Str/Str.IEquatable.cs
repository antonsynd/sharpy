using System;
namespace Sharpy
{
    public readonly partial struct Str : IEquatable<Str>
    {
        /// <summary>
        /// Implements the __eq__ dunder method for string equality with object.
        /// Required by IEquatable interface.
        /// </summary>
        public bool __Eq__(object obj)
        {
            if (obj is Str str)
            {
                return _s == str._s;
            }
            if (obj is string s)
            {
                return _s == s;
            }
            return false;
        }

        /// <summary>
        /// Implements the __eq__ dunder method for string equality.
        /// </summary>
        public bool __Eq__(Str other)
        {
            return _s == other._s;
        }

        /// <summary>
        /// Implements IEquatable interface for use with generic collections.
        /// </summary>
        public bool Equals(Str other)
        {
            return __Eq__(other);
        }

        // Note: Equals(object?) override is already in Str.cs
    }
}
