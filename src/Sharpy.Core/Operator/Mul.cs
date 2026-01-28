namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static int Mul(int left, int right) => left * right;
        public static long Mul(long left, long right) => left * right;
        public static float Mul(float left, float right) => left * right;
        public static double Mul(double left, double right) => left * right;
        public static decimal Mul(decimal left, decimal right) => left * right;

        public static int __Mul__(int left, int right) => left * right;
        public static long __Mul__(long left, long right) => left * right;
        public static float __Mul__(float left, float right) => left * right;
        public static double __Mul__(double left, double right) => left * right;
        public static decimal __Mul__(decimal left, decimal right) => left * right;
    }
}
