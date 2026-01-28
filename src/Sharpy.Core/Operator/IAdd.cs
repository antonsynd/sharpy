namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static void IAdd(ref int left, int right) => left += right;
        public static void IAdd(ref long left, long right) => left += right;
        public static void IAdd(ref float left, float right) => left += right;
        public static void IAdd(ref double left, double right) => left += right;
        public static void IAdd(ref decimal left, decimal right) => left += right;

        public static void __IAdd__(ref int left, int right) => left += right;
        public static void __IAdd__(ref long left, long right) => left += right;
        public static void __IAdd__(ref float left, float right) => left += right;
        public static void __IAdd__(ref double left, double right) => left += right;
        public static void __IAdd__(ref decimal left, decimal right) => left += right;
    }
}
