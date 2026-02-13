namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string of one character whose Unicode code point is the integer i.
        /// This is the inverse of ord().
        /// </summary>
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
