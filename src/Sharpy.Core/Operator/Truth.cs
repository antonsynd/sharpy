using Object = Sharpy.Core.Object;
using Sharpy.Core;

namespace Sharpy.Operator;

using Sharpy.Collections.Interfaces;

public static partial class Exports
{
    public static bool Truth(IBoolConvertible boolConvertible)
    {
        return boolConvertible.__Bool__();
    }

    public static bool Truth(ISized sized)
    {
        return sized.__Len__() > 0;
    }

    public static bool Truth(Object obj)
    {
        return !obj.__Bool__();
    }
}
