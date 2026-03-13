namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return left + right for int operands.</summary>
        public static int Add(int left, int right) => left + right;
        /// <summary>Return left + right for long operands.</summary>
        public static long Add(long left, long right) => left + right;
        /// <summary>Return left + right for float operands.</summary>
        public static float Add(float left, float right) => left + right;
        /// <summary>Return left + right for double operands.</summary>
        public static double Add(double left, double right) => left + right;
        /// <summary>Return left + right for decimal operands.</summary>
        public static decimal Add(decimal left, decimal right) => left + right;
        /// <summary>Return left + right (concatenation) for string operands.</summary>
        public static string Add(string left, string right) => left + right;
    }
}
