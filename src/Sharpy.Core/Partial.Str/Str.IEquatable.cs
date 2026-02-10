using System;
namespace Sharpy
{
    public readonly partial struct Str : IEquatable<Str>
    {
        /// <summary>
        /// Implements IEquatable interface for use with generic collections.
        /// </summary>
        public bool Equals(Str other)
        {
            return _s == other._s;
        }

        // Note: Equals(object?) override is already in Str.cs
    }
}
