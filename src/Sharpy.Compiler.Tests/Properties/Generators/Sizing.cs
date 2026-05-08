namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class Sizing
{
    public static int FuelFromSize(int size) => size switch
    {
        <= 10 => 1,
        <= 30 => 2,
        <= 50 => 3,
        <= 70 => 4,
        <= 90 => 5,
        _ => 6
    };

    public static int MaxListLength(int fuel) => Math.Max(0, Math.Min(fuel, 4));
    public static int MaxBodyLength(int fuel) => Math.Max(1, Math.Min(fuel, 3));
    public static int MaxParameters(int fuel) => Math.Max(0, Math.Min(fuel, 4));
}
