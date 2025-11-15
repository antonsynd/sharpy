using Object = Sharpy.Core.Object;
using Sharpy.Core;

namespace Sharpy.Operator;

using Sharpy.Collections.Interfaces;

public static partial class Exports
{
    public static bool Not(IBoolConvertible boolConvertible)
    {
        return !boolConvertible.__Bool__();
    }

    public static bool Not(ISized sized)
    {
        return sized.__Len__() == 0;
    }

    public static bool Not(Object obj)
    {
        return !obj.__Bool__();
    }
}
