namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for float32 (System.Single).
    /// Delegates to <see cref="Builtins.Float(bool)"/> etc. and then narrows to float.
    /// Overflow to <see cref="float.PositiveInfinity"/> / <see cref="float.NegativeInfinity"/>
    /// is intentional: Python's float() never raises on overflow, so float32() follows suit.
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to float32. True becomes 1.0f, False becomes 0.0f.
        /// </summary>
        public static float Float32(bool b)
        {
            return b ? 1.0f : 0.0f;
        }

        /// <summary>
        /// Convert int to float32.
        /// </summary>
        public static float Float32(int i)
        {
            return (float)i;
        }

        /// <summary>
        /// Convert long to float32.
        /// </summary>
        public static float Float32(long l)
        {
            return (float)l;
        }

        /// <summary>
        /// Convert float to float32 (identity).
        /// </summary>
        public static float Float32(float f)
        {
            return f;
        }

        /// <summary>
        /// Convert double to float32 (narrowing). Overflow produces Infinity.
        /// </summary>
        public static float Float32(double d)
        {
            return (float)d;
        }

        /// <summary>
        /// Convert decimal to float32.
        /// </summary>
        public static float Float32(decimal m)
        {
            return (float)m;
        }

        /// <summary>
        /// Parse string to float32. Overflow produces Infinity, matching Python semantics.
        /// </summary>
        public static float Float32(string s)
        {
            return (float)Float(s);
        }

        /// <summary>
        /// Convert byte to float32.
        /// </summary>
        public static float Float32(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to float32.
        /// </summary>
        public static float Float32(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to float32.
        /// </summary>
        public static float Float32(short s)
        {
            return s;
        }

        /// <summary>
        /// Convert ushort to float32.
        /// </summary>
        public static float Float32(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to float32.
        /// </summary>
        public static float Float32(uint u)
        {
            return (float)u;
        }

        /// <summary>
        /// Convert ulong to float32.
        /// </summary>
        public static float Float32(ulong ul)
        {
            return (float)ul;
        }
    }
}
