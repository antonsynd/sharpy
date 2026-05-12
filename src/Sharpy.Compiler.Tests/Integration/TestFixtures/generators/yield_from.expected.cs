#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class YieldFrom
{
    public static System.Collections.Generic.IEnumerable<int> Inner()
    {
#line (2, 5) - (2, 13) 1 "yield_from.spy"
        yield return 0;
#line (3, 5) - (3, 13) 1 "yield_from.spy"
        yield return 1;
    }

    public static System.Collections.Generic.IEnumerable<int> Outer()
    {
#line (6, 5) - (6, 24) 1 "yield_from.spy"
        foreach (var __yieldItem_0 in Inner())
        {
            yield return __yieldItem_0;
        }

#line (7, 5) - (7, 13) 1 "yield_from.spy"
        yield return 2;
#line (8, 5) - (8, 13) 1 "yield_from.spy"
        yield return 3;
    }

    public static void Main()
    {
#line (11, 5) - (13, 1) 1 "yield_from.spy"
        foreach (var __loopVar_1 in Outer())
        {
            var x = __loopVar_1;
#line (12, 9) - (12, 17) 1 "yield_from.spy"
            global::Sharpy.Builtins.Print(x);
        }
    }
}
