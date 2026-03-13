namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return left * right for int operands.</summary>
        public static int Mul(int left, int right) => left * right;
        /// <summary>Return left * right for long operands.</summary>
        public static long Mul(long left, long right) => left * right;
        /// <summary>Return left * right for float operands.</summary>
        public static float Mul(float left, float right) => left * right;
        /// <summary>Return left * right for double operands.</summary>
        public static double Mul(double left, double right) => left * right;
        /// <summary>Return left * right for decimal operands.</summary>
        public static decimal Mul(decimal left, decimal right) => left * right;
    }
}
