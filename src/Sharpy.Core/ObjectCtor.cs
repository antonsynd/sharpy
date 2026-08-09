namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Construct a bare object, matching CPython's <c>object()</c>, which takes no arguments.
        /// </summary>
        /// <remarks>
        /// This exists so a reference to the builtin type <c>object</c> has an overload set to pin
        /// against — the constructor-reference Conversion family's precondition (#1272).
        /// </remarks>
        /// <returns>A new object instance</returns>
        public static object Object()
        {
            return new object();
        }
    }
}
