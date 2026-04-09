using System.Collections.Generic;
using System.Collections;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert a bool to bool (identity).
        /// </summary>
        /// <param name="b">The bool value</param>
        /// <returns>The same bool value</returns>
        public static bool Bool(bool b)
        {
            return b;
        }

        /// <summary>
        /// Convert a decimal to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="d">The decimal value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(decimal d)
        {
            return d != 0;
        }

        /// <summary>
        /// Convert a float to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="f">The float value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(float f)
        {
            return f != 0;
        }

        /// <summary>
        /// Convert a double to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="d">The double value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(double d)
        {
            return d != 0;
        }

        /// <summary>
        /// Convert an int to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="i">The int value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(int i)
        {
            return i != 0;
        }

        /// <summary>
        /// Convert a uint to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="u">The uint value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(uint u)
        {
            return u != 0;
        }

        /// <summary>
        /// Convert a short to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="s">The short value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(short s)
        {
            return s != 0;
        }

        /// <summary>
        /// Convert a ushort to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="u">The ushort value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(ushort u)
        {
            return u != 0;
        }

        /// <summary>
        /// Convert a long to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="l">The long value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(long l)
        {
            return l != 0;
        }

        /// <summary>
        /// Convert a ulong to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="u">The ulong value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(ulong u)
        {
            return u != 0;
        }

        /// <summary>
        /// Convert a byte to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="b">The byte value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(byte b)
        {
            return b != 0;
        }

        /// <summary>
        /// Convert an sbyte to bool. Returns False if zero, True otherwise.
        /// </summary>
        /// <param name="s">The sbyte value</param>
        /// <returns>False if zero, True otherwise</returns>
        public static bool Bool(sbyte s)
        {
            return s != 0;
        }

        /// <summary>
        /// Convert a string to bool. Returns False if the string is null or empty, True otherwise.
        /// </summary>
        /// <param name="s">The string value</param>
        /// <returns>False if null or empty, True otherwise</returns>
        public static bool Bool(string s)
        {
            if (s is null)
            {
                return false;
            }

            return s.Length > 0;
        }

        /// <summary>
        /// Convert an arbitrary object to bool using Python's truth testing protocol.
        /// Checks __bool__ (IBoolConvertible), then __len__ (ISized), then collection emptiness.
        /// Non-null objects without these protocols are truthy.
        /// </summary>
        /// <param name="obj">The object to test for truthiness</param>
        /// <returns>The truth value of the object</returns>
        /// <example>
        /// <code>
        /// bool(0)        # False
        /// bool(1)        # True
        /// bool("")       # False
        /// bool("hello")  # True
        /// bool([])       # False
        /// bool([1, 2])   # True
        /// bool(None)     # False
        /// </code>
        /// </example>
        public static bool Bool(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj is string @string)
            {
                return Bool(@string);
            }

            if (obj is bool @bool)
            {
                return Bool(@bool);
            }

            if (obj is int @int)
            {
                return Bool(@int);
            }

            if (obj is uint @uint)
            {
                return Bool(@uint);
            }

            if (obj is byte @byte)
            {
                return Bool(@byte);
            }

            if (obj is sbyte @sbyte)
            {
                return Bool(@sbyte);
            }

            if (obj is short @short)
            {
                return Bool(@short);
            }

            if (obj is ushort @ushort)
            {
                return Bool(@ushort);
            }

            if (obj is long @long)
            {
                return Bool(@long);
            }

            if (obj is ulong @ulong)
            {
                return Bool(@ulong);
            }

            if (obj is double @double)
            {
                return Bool(@double);
            }

            if (obj is decimal @decimal)
            {
                return Bool(@decimal);
            }

            if (obj is float @float)
            {
                return Bool(@float);
            }

            // __bool__ dispatch: types with IBoolConvertible (user-defined __bool__)
            if (obj is IBoolConvertible boolConvertible)
            {
                return boolConvertible.IsTrue;
            }

            // __len__ fallback: types with ISized (__len__ != 0 is truthy)
            if (obj is ISized sized)
            {
                return sized.Count != 0;
            }

            // Collection types - check Count for emptiness
            // Note: ICollection (non-generic) is for arrays and old-style collections
            if (obj is System.Collections.ICollection collection)
            {
                return collection.Count > 0;
            }

            // Check for ICollection<T> or IReadOnlyCollection<T> via interface check
            // This handles List<T>, Set<T>, etc. that use explicit interface implementations
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var genericDef = iface.GetGenericTypeDefinition();
                    if (genericDef == typeof(ICollection<>) || genericDef == typeof(IReadOnlyCollection<>))
                    {
                        var countProp = iface.GetProperty("Count");
                        if (countProp is not null)
                        {
                            var count = (int)countProp.GetValue(obj)!;
                            return count > 0;
                        }
                    }
                }
            }

            // Non-null objects are truthy by default (matching Python's behavior)
            return true;
        }
    }
}
