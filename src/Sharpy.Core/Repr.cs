namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string containing a printable representation of an object.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="object.ToString()"/> to get the representation.
        /// Sharpy types (List, Set, Dict) override ToString() to produce
        /// Python-compatible repr output (e.g., "[1, 2, 3]", "{1, 2}", etc.).
        /// </remarks>
        /// <param name="obj">The object to get the representation of</param>
        /// <returns>A printable string representation</returns>
        /// <example>
        /// <code>
        /// repr("hello")      # "'hello'"
        /// repr([1, 2, 3])    # "[1, 2, 3]"
        /// repr(None)         # "None"
        /// </code>
        /// </example>
        public static string Repr(object? obj)
        {
            return obj?.ToString() ?? "None";
        }
    }
}
