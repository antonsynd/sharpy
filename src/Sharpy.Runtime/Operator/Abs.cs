namespace Sharpy.Operator;

using System.Numerics;
using System.Runtime.InteropServices;

public static partial class Exports
{
    public static decimal Abs(decimal x)
    {
        return Math.Abs(x);
    }

    public static double Abs(double x)
    {
        return Math.Abs(x);
    }

    public static int Abs(int x)
    {
        return Math.Abs(x);
    }

    public static IntPtr Abs(IntPtr x)
    {
        return Math.Abs(x);
    }

    public static short Abs(short x)
    {
        return Math.Abs(x);
    }

    public static long Abs(long x)
    {
        return Math.Abs(x);
    }

    public static float Abs(float x)
    {
        return Math.Abs(x);
    }

    public static sbyte Abs(sbyte x)
    {
        return Math.Abs(x);
    }

    public static T Abs<T>(T? x) where T : INumberBase<T>
    {
        if (x is null)
        {
            throw new ArgumentNullException(nameof(x));
        }

        return T.Abs(x);
    }

    // TODO: Optional<T> doesn't implement __Abs__
    // public static T Abs<T>(Optional<T> x) where T : IAbsoluteValue<T>
    // {
    //     if (!x.HasValue())
    //     {
    //         throw new ArgumentNullException(nameof(x));
    //     }
    //
    //     return x.__Abs__();
    // }
}
