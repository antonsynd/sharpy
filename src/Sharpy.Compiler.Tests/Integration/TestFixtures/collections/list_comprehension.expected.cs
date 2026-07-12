// Snapshot: List comprehension
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListComprehension
{
    public static void Main()
    {
        var __src_1 = global::Sharpy.Builtins.Range(5);
        var __comp_0 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_1).Count);
        foreach (var __loopVar_2 in __src_1)
        {
            var x = __loopVar_2;
            __comp_0.Add(x * 2);
        }

#line (3, 5) - (3, 51) 1 "list_comprehension.spy"
        Sharpy.List<int> result = __comp_0;
#line (4, 5) - (6, 1) 1 "list_comprehension.spy"
        foreach (var __loopVar_3 in result)
        {
            var item = __loopVar_3;
#line (5, 9) - (5, 20) 1 "list_comprehension.spy"
            global::Sharpy.Builtins.Print(item);
        }

#line (6, 5) - (6, 23) 1 "list_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
    }
}
