using Sharpy.Core;
namespace Sharpy.Operator;

using System.Numerics;
using System.Runtime.InteropServices;

public static partial class Exports
{
    public static decimal Abs(decimal x)
    {
        return System.Math.Abs(x);
    }

    public static double Abs(double x)
    {
        return System.Math.Abs(x);
    }

    public static int Abs(int x)
    {
        return System.Math.Abs(x);
    }

    public static IntPtr Abs(IntPtr x)
    {
        return System.Math.Abs(x);
    }

    public static short Abs(short x)
    {
        return System.Math.Abs(x);
    }

    public static long Abs(long x)
    {
        return System.Math.Abs(x);
    }

    public static float Abs(float x)
    {
        return System.Math.Abs(x);
    }

    public static sbyte Abs(sbyte x)
    {
        return System.Math.Abs(x);
    }

    public static T Abs<T>(T? x) where T : INumberBase<T>
    {
        if (x is null)
        {
            throw new ArgumentNullException(nameof(x));
        }

        return T.Abs(x);
    }
}
