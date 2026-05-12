#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class YieldBasic
{
    public static System.Collections.Generic.IEnumerable<int> CountUp(int n)
    {
#line (2, 5) - (2, 10) 1 "yield_basic.spy"
        var i = 0;
#line (3, 5) - (7, 1) 1 "yield_basic.spy"
        while (i < n)
        {
#line (4, 9) - (4, 17) 1 "yield_basic.spy"
            yield return i;
#line (5, 9) - (5, 15) 1 "yield_basic.spy"
            i = i + 1;
        }
    }

    public static void Main()
    {
#line (8, 5) - (10, 1) 1 "yield_basic.spy"
        foreach (var __loopVar_0 in CountUp(5))
        {
            var x = __loopVar_0;
#line (9, 9) - (9, 17) 1 "yield_basic.spy"
            global::Sharpy.Builtins.Print(x);
        }
    }
}
