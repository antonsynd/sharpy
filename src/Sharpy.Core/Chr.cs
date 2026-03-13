namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string of one character whose Unicode code point is the integer i.
        /// This is the inverse of ord().
        /// </summary>
        /// <param name="i">A Unicode code point (0 to 0x10FFFF)</param>
        /// <returns>A string of one character</returns>
        /// <exception cref="ValueError">Thrown when i is out of range</exception>
        /// <example>
        /// <code>
        /// chr(65)     # "A"
        /// chr(8364)   # "€"
        /// chr(97)     # "a"
        /// </code>
        /// </example>
        public static string Chr(int i)
        {
            if (i < 0 || i > 0x10FFFF)
            {
                throw new ValueError("chr() arg not in range(0x110000)");
            }

            return char.ConvertFromUtf32(i);
        }
    }
}
