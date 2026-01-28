namespace Sharpy.Core
{
    public static partial class Exports
    {
        /// <summary>
        /// Return a string containing a printable representation of an object.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="object.ToString()"/> to get the representation.
        /// Sharpy types (List, Set, Dict) override ToString() to produce
        /// Python-compatible repr output (e.g., "[1, 2, 3]", "{1, 2}", etc.).
        /// </remarks>
        public static string Repr(object? obj)
        {
            return obj?.ToString() ?? "None";
        }
    }
}
