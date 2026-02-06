namespace Sharpy.Core
{
    /// <summary>
    /// Operator overloads for List&lt;T&gt;.
    /// Includes equality, comparison, concatenation, repetition, and truthiness operators.
    /// </summary>
    public sealed partial class List<T>
    {
        #region Equality Operators

        /// <summary>
        /// Determines whether two lists are equal by comparing elements.
        /// </summary>
        /// <remarks>
        /// Returns true for both lists if they contain the same elements,
        /// even if they are not the same list reference.
        /// </remarks>
        public static bool operator ==(List<T>? left, List<T>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.Equals(right) ?? false;
        }

        /// <summary>
        /// Determines whether two lists are not equal.
        /// </summary>
        public static bool operator !=(List<T>? left, List<T>? right)
        {
            return !(left == right);
        }

        #endregion

        #region Comparison Operators (Lexicographical)

        /// <summary>
        /// Determines whether the left list is lexicographically less than the right list.
        /// </summary>
        public static bool operator <(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.__Lt__(right);
        }

        /// <summary>
        /// Determines whether the left list is lexicographically less than or equal to the right list.
        /// </summary>
        public static bool operator <=(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported("<=", "NoneType");
            }

            return left.__Le__(right);
        }

        /// <summary>
        /// Determines whether the left list is lexicographically greater than the right list.
        /// </summary>
        public static bool operator >(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            return left.__Gt__(right);
        }

        /// <summary>
        /// Determines whether the left list is lexicographically greater than or equal to the right list.
        /// </summary>
        public static bool operator >=(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported(">=", "NoneType");
            }

            return left.__Ge__(right);
        }

        /// <summary>
        /// Lexicographically compares this list with another (less than).
        /// </summary>
        internal bool __Lt__(List<T>? other)
        {
            if (other is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            var minLen = System.Math.Min(_list.Count, other._list.Count);

            for (int i = 0; i < minLen; i++)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                if (!Operator.Operator.Eq(leftElem, rightElem))
                {
                    return Operator.Operator.Lt(leftElem, rightElem);
                }
            }

            return _list.Count < other._list.Count;
        }

        /// <summary>
        /// Lexicographically compares this list with another (less than or equal).
        /// </summary>
        internal bool __Le__(List<T>? other)
        {
            if (other is null)
            {
                throw TypeError.OpNotSupported("<=", "NoneType");
            }

            var minLen = System.Math.Min(_list.Count, other._list.Count);

            for (int i = 0; i < minLen; i++)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                if (!Operator.Operator.Eq(leftElem, rightElem))
                {
                    return Operator.Operator.Lt(leftElem, rightElem);
                }
            }

            return _list.Count <= other._list.Count;
        }

        /// <summary>
        /// Lexicographically compares this list with another (greater than).
        /// </summary>
        internal bool __Gt__(List<T>? other)
        {
            if (other is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            var minLen = System.Math.Min(_list.Count, other._list.Count);

            for (int i = 0; i < minLen; i++)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                if (!Operator.Operator.Eq(leftElem, rightElem))
                {
                    return Operator.Operator.Gt(leftElem, rightElem);
                }
            }

            return _list.Count > other._list.Count;
        }

        /// <summary>
        /// Lexicographically compares this list with another (greater than or equal).
        /// </summary>
        internal bool __Ge__(List<T>? other)
        {
            if (other is null)
            {
                throw TypeError.OpNotSupported(">=", "NoneType");
            }

            var minLen = System.Math.Min(_list.Count, other._list.Count);

            for (int i = 0; i < minLen; i++)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                if (!Operator.Operator.Eq(leftElem, rightElem))
                {
                    return Operator.Operator.Gt(leftElem, rightElem);
                }
            }

            return _list.Count >= other._list.Count;
        }

        #endregion

        #region Concatenation Operators

        /// <summary>
        /// Concatenates two lists, returning a new list.
        /// </summary>
        public static List<T> operator +(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
            }

            if (right is null)
            {
                throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
            }

            var res = left.Copy();
            res.Extend(right);

            return res;
        }

        #endregion

        #region Repetition Operators

        /// <summary>
        /// Repeats a list a specified number of times, returning a new list.
        /// </summary>
        public static List<T> operator *(List<T>? left, int count)
        {
            if (left is null)
            {
                throw TypeError.CanOnlyNot("multiply", $"List<{typeof(T).Name}>", "NoneType", "with", "int");
            }

            var res = new List<T>();

            if (count <= 0)
            {
                return res;
            }

            for (int i = 0; i < count; ++i)
            {
                res.Extend(left);
            }

            return res;
        }

        /// <summary>
        /// Repeats a list a specified number of times, returning a new list.
        /// </summary>
        public static List<T> operator *(int count, List<T>? right)
        {
            return right * count;
        }

        #endregion

        #region Truthiness Operators

        /// <summary>
        /// Returns true if the list is not null and not empty.
        /// </summary>
        public static bool operator true(List<T>? list)
        {
            return list is not null && list._list.Count > 0;
        }

        /// <summary>
        /// Returns true if the list is null or empty.
        /// </summary>
        public static bool operator false(List<T>? list)
        {
            return list is null || list._list.Count == 0;
        }

        #endregion
    }
}
