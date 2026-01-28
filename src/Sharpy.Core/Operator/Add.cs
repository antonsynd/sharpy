namespace Sharpy.Operator;

public static partial class Exports
{
    public static int Add(int left, int right) => left + right;
    public static long Add(long left, long right) => left + right;
    public static float Add(float left, float right) => left + right;
    public static double Add(double left, double right) => left + right;
    public static decimal Add(decimal left, decimal right) => left + right;
    public static string Add(string left, string right) => left + right;

    public static int __Add__(int left, int right) => left + right;
    public static long __Add__(long left, long right) => left + right;
    public static float __Add__(float left, float right) => left + right;
    public static double __Add__(double left, double right) => left + right;
    public static decimal __Add__(decimal left, decimal right) => left + right;
    public static string __Add__(string left, string right) => left + right;
}
