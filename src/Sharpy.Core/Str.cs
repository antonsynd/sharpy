namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an arbitrary object to <see cref="string"/>.
        /// Returns <c>"None"</c> for null, Python-style <c>"True"</c>/<c>"False"</c>
        /// for booleans, and <see cref="object.ToString"/> for everything else.
        /// </summary>
        public static string Str(object x)
        {
            if (x is null)
            {
                return "None";
            }

            if (x is bool b)
            {
                return b ? "True" : "False";
            }

            return x.ToString() ?? "";
        }

        /// <summary>
        /// Return the C# <see cref="string"/> unchanged.
        /// </summary>
        public static string Str(string s)
        {
            return s;
        }

        /// <summary>
        /// Convert an <see cref="int"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(int i)
        {
            return i.ToString();
        }

        /// <summary>
        /// Convert a <see cref="long"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(long l)
        {
            return l.ToString();
        }

        /// <summary>
        /// Convert a <see cref="double"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(double d)
        {
            return d.ToString();
        }

        /// <summary>
        /// Convert a <see cref="float"/> to <see cref="string"/> without boxing.
        /// </summary>
        public static string Str(float f)
        {
            return f.ToString();
        }

        /// <summary>
        /// Convert a <see cref="bool"/> to <see cref="string"/>.
        /// Returns Python-style <c>"True"</c> or <c>"False"</c>.
        /// </summary>
        public static string Str(bool b)
        {
            return b ? "True" : "False";
        }
    }
}
