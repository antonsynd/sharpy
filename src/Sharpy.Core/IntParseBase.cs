using System;
using System.Globalization;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Parse a string with an explicit base (2, 8, 10, or 16) into the 64-bit range through
        /// the one numeric-text grammar (<see cref="NumericText"/>, #2357).
        /// </summary>
        internal static long ParseIntWithBase(string s, int @base, string funcName)
        {
            return NumericText.ToSigned(s, @base, long.MinValue, long.MaxValue, funcName);
        }
    }
}
