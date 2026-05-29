using System;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible pprint module.
    /// Provides pretty-printing of data structures with configurable formatting.
    /// </summary>
    public static partial class Pprint
    {
        /// <summary>
        /// Pretty-print an object to stdout.
        /// Equivalent to Python's pprint.pprint() / pprint.pp().
        /// </summary>
        /// <param name="obj">The object to pretty-print.</param>
        /// <param name="indent">Number of spaces for each nesting level.</param>
        /// <param name="width">Maximum line width.</param>
        /// <param name="depth">Maximum nesting depth (null means unlimited).</param>
        /// <param name="compact">Whether to fit multiple items on each line.</param>
        /// <param name="sortDicts">Whether to sort dict keys.</param>
        public static void Pp(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            var printer = new PrettyPrinter(indent, width, depth, compact, sortDicts);
            printer.Pprint(obj);
        }

        /// <summary>
        /// Format an object into a pretty-printed string.
        /// </summary>
        /// <param name="obj">The object to format.</param>
        /// <param name="indent">Number of spaces for each nesting level.</param>
        /// <param name="width">Maximum line width.</param>
        /// <param name="depth">Maximum nesting depth (null means unlimited).</param>
        /// <param name="compact">Whether to fit multiple items on each line.</param>
        /// <param name="sortDicts">Whether to sort dict keys.</param>
        /// <returns>A pretty-printed string representation.</returns>
        public static string Pformat(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            var printer = new PrettyPrinter(indent, width, depth, compact, sortDicts);
            return printer.Pformat(obj);
        }

        /// <summary>
        /// Determine whether the object can be pretty-printed as a valid literal.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the object is readable.</returns>
        public static bool Isreadable(object? obj)
        {
            var printer = new PrettyPrinter();
            return printer.Isreadable(obj);
        }

        /// <summary>
        /// Determine whether the object requires recursive representation.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the object contains circular references.</returns>
        public static bool Isrecursive(object? obj)
        {
            var printer = new PrettyPrinter();
            return printer.Isrecursive(obj);
        }
    }
}
