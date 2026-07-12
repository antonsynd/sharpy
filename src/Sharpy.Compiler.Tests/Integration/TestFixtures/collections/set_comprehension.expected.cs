// Snapshot: Set comprehension
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class SetComprehension
{
    public static void Main()
    {
#line (3, 5) - (3, 43) 1 "set_comprehension.spy"
        Sharpy.List<int> items = new Sharpy.List<int>()
        {
            1,
            2,
            2,
            3,
            3,
            3
        };
        var __src_1 = items;
        var __comp_0 = new Sharpy.Set<int>(((global::Sharpy.ISized)__src_1).Count);
        foreach (var __loopVar_2 in __src_1)
        {
            var x = __loopVar_2;
            __comp_0.Add(x);
        }

#line (4, 5) - (4, 43) 1 "set_comprehension.spy"
        Sharpy.Set<int> result = __comp_0;
#line (5, 5) - (5, 23) 1 "set_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
    }
}
