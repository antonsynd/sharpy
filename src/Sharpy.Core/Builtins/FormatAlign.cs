namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Aligns a string within a field of given width using the specified fill character
        /// and alignment mode. Used by f-string format spec codegen for custom fill characters
        /// and center-alignment.
        /// </summary>
        /// <param name="value">The string to align</param>
        /// <param name="width">The total field width</param>
        /// <param name="fill">The fill character for padding</param>
        /// <param name="alignment">Alignment mode: '&lt;' left, '&gt;' right, '^' center, '=' numeric sign-aware</param>
        /// <returns>The aligned string, or <paramref name="value"/> unchanged if already wider than <paramref name="width"/></returns>
        public static string FormatAlign(string value, int width, char fill, char alignment)
        {
            if (value.Length >= width)
            {
                return value;
            }

            switch (alignment)
            {
                case '<':
                    return value.PadRight(width, fill);
                case '>':
                    return value.PadLeft(width, fill);
                case '^':
                    {
                        int totalPadding = width - value.Length;
                        int leftPadding = totalPadding / 2;
                        // Python puts extra padding on the right when total padding is odd
                        return value.PadLeft(value.Length + leftPadding, fill).PadRight(width, fill);
                    }
                case '=':
                    {
                        string sign = "";
                        string digits = value;
                        if (value.Length > 0 && (value[0] == '-' || value[0] == '+'))
                        {
                            sign = value.Substring(0, 1);
                            digits = value.Substring(1);
                        }
                        int digitPadWidth = width - sign.Length;
                        return sign + digits.PadLeft(digitPadWidth, fill);
                    }
                default:
                    return value;
            }
        }
    }
}
