using System;

namespace Sharpy
{
    /// <summary>
    /// Operator overloads for Bytes.
    /// Includes equality, concatenation, repetition, and containment.
    /// </summary>
    public readonly partial struct Bytes
    {
        #region Equality

        /// <summary>
        /// Determines whether two Bytes instances are equal by comparing byte values.
        /// </summary>
        public bool Equals(Bytes other)
        {
            if (_data.Length \!= other._data.Length)
            {
                return false;
            }

            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i] \!= other._data[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether this Bytes equals another object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is Bytes other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this Bytes instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (byte b in _data)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two Bytes instances are equal.
        /// </summary>
        public static bool operator ==(Bytes left, Bytes right) => left.Equals(right);

        /// <summary>
        /// Determines whether two Bytes instances are not equal.
        /// </summary>
        public static bool operator \!=(Bytes left, Bytes right) => \!left.Equals(right);

        #endregion

        #region Concatenation

        /// <summary>
        /// Concatenates two Bytes instances, returning a new Bytes.
        /// </summary>
        public static Bytes operator +(Bytes left, Bytes right)
        {
            var result = new byte[left._data.Length + right._data.Length];
            Array.Copy(left._data, 0, result, 0, left._data.Length);
            Array.Copy(right._data, 0, result, left._data.Length, right._data.Length);
            return new Bytes(result, true);
        }

        #endregion

        #region Repetition

        /// <summary>
        /// Repeats Bytes a specified number of times, returning new Bytes.
        /// </summary>
        public static Bytes operator *(Bytes left, int count)
        {
            if (count <= 0 || left._data.Length == 0)
            {
                return new Bytes(Array.Empty<byte>(), true);
            }

            var result = new byte[left._data.Length * count];
            for (int i = 0; i < count; i++)
            {
                Array.Copy(left._data, 0, result, i * left._data.Length, left._data.Length);
            }

            return new Bytes(result, true);
        }

        /// <summary>
        /// Repeats Bytes a specified number of times (int * Bytes).
        /// </summary>
        public static Bytes operator *(int count, Bytes right)
        {
            return right * count;
        }

        #endregion

        #region Containment

        /// <summary>
        /// Checks if a byte value is contained in this Bytes instance.
        /// </summary>
        public bool Contains(int value)
        {
            if (value < 0 || value > 255)
            {
                throw new ValueError("byte must be in range(0, 256)");
            }

            byte b = (byte)value;
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i] == b)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a sub-sequence of bytes is contained in this Bytes instance.
        /// </summary>
        public bool Contains(Bytes sub)
        {
            return Find(sub) >= 0;
        }

        #endregion

        #region Truthiness

        /// <summary>
        /// Returns true if the bytes sequence is non-empty.
        /// </summary>
        public static bool operator true(Bytes b) => b._data.Length > 0;

        /// <summary>
        /// Returns true if the bytes sequence is empty.
        /// </summary>
        public static bool operator false(Bytes b) => b._data.Length == 0;

        #endregion
    }
}
