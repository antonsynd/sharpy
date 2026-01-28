namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
        /// <remarks>
        /// This returns true for both lists if they contain the same elements,
        /// even if they are not the actual same list reference.
        /// </remarks>
        public static bool operator ==(List<T>? left, List<T>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.Equals(right) ?? false;
        }

        public static bool operator !=(List<T>? left, List<T>? right)
        {
            return !(left == right);
        }

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

        public static bool operator true(List<T>? list)
        {
            return list is not null && list._list.Count > 0;
        }

        public static bool operator false(List<T>? list)
        {
            return list is null || list._list.Count == 0;
        }

        public static bool operator <(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported("<", "NoneType");
            }

            return left.__Lt__(right);
        }

        public static bool operator <=(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported("<=", "NoneType");
            }

            return left.__Le__(right);
        }

        public static bool operator >(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported(">", "NoneType");
            }

            return left.__Gt__(right);
        }

        public static bool operator >=(List<T>? left, List<T>? right)
        {
            if (left is null)
            {
                throw TypeError.OpNotSupported(">=", "NoneType");
            }

            return left.__Ge__(right);
        }
    }
}
