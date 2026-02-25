namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Aligns a string within a field of given width using the specified fill character
        /// and alignment mode. Used by f-string format spec codegen for custom fill characters
        /// and center-alignment.
        /// </summary>
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
