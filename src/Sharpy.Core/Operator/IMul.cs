namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>In-place multiplication: left *= right (int).</summary>
        public static void IMul(ref int left, int right) => left *= right;
        /// <summary>In-place multiplication: left *= right (long).</summary>
        public static void IMul(ref long left, long right) => left *= right;
        /// <summary>In-place multiplication: left *= right (float).</summary>
        public static void IMul(ref float left, float right) => left *= right;
        /// <summary>In-place multiplication: left *= right (double).</summary>
        public static void IMul(ref double left, double right) => left *= right;
        /// <summary>In-place multiplication: left *= right (decimal).</summary>
        public static void IMul(ref decimal left, decimal right) => left *= right;
    }
}
