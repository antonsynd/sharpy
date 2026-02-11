namespace Sharpy
{
    public static partial class Operator
    {
        public static void IMul(ref int left, int right) => left *= right;
        public static void IMul(ref long left, long right) => left *= right;
        public static void IMul(ref float left, float right) => left *= right;
        public static void IMul(ref double left, double right) => left *= right;
        public static void IMul(ref decimal left, decimal right) => left *= right;
    }
}
