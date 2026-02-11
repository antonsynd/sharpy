namespace Sharpy
{
    public static partial class Operator
    {
        public static void IAdd(ref int left, int right) => left += right;
        public static void IAdd(ref long left, long right) => left += right;
        public static void IAdd(ref float left, float right) => left += right;
        public static void IAdd(ref double left, double right) => left += right;
        public static void IAdd(ref decimal left, decimal right) => left += right;
    }
}
