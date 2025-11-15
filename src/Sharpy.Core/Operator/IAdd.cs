using Sharpy.Core;
namespace Sharpy.Operator;

public static partial class Exports
{
    public static void IAdd<T>(IInplaceAddable<T> left, T right)
    {
        left.__IAdd__(right);
    }

    public static void IAdd<TAugend, TAddend>(ref TAugend left, TAddend right)
        where TAugend : IAddable<TAugend, TAddend>
    {
        left = left.__Add__(right);
    }

    public static void __IAdd__<T>(IInplaceAddable<T> left, T right) => IAdd<T>(left, right);

    public static void __IAdd__<TAugend, TAddend>(ref TAugend left, TAddend right)
        where TAugend : IAddable<TAugend, TAddend>
        => IAdd<TAugend, TAddend>(ref left, right);
}
