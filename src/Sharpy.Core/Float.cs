namespace Sharpy.Core
{
    /// <summary>
    /// Type conversion functions for float (delegates to Double).
    /// Python's float() maps to .NET double. These overloads ensure
    /// that the builtin discovery finds "float" as a valid builtin name.
    /// </summary>
    public static partial class Exports
    {
        public static double Float(bool b)
        {
            return Double(b);
        }

        public static double Float(int i)
        {
            return Double(i);
        }

        public static double Float(long l)
        {
            return Double(l);
        }

        public static double Float(float f)
        {
            return Double(f);
        }

        public static double Float(double d)
        {
            return Double(d);
        }

        public static double Float(decimal m)
        {
            return Double(m);
        }

        public static double Float(string s)
        {
            return Double(s);
        }
    }
}
