namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static void IMul(ref int left, int right) => left *= right;
        public static void IMul(ref long left, long right) => left *= right;
        public static void IMul(ref float left, float right) => left *= right;
        public static void IMul(ref double left, double right) => left *= right;
        public static void IMul(ref decimal left, decimal right) => left *= right;

        public static void __IMul__(ref int left, int right) => left *= right;
        public static void __IMul__(ref long left, long right) => left *= right;
        public static void __IMul__(ref float left, float right) => left *= right;
        public static void __IMul__(ref double left, double right) => left *= right;
        public static void __IMul__(ref decimal left, decimal right) => left *= right;
    }
}
