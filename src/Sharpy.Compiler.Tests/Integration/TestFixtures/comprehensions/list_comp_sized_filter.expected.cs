// Snapshot: Single-for comprehension over a sized list with a filter (D4 typed-source decl + IfClause)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListCompSizedFilter
{
    public static void Main()
    {
#line (2, 5) - (2, 41) 1 "list_comp_sized_filter.spy"
        Sharpy.List<int> nums = new Sharpy.List<int>()
        {
            1,
            -2,
            3,
            -4,
            5
        };
        Sharpy.List<int> __src_1 = nums;
        var __comp_0 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_1).Count);
        foreach (var __loopVar_2 in __src_1)
        {
            var x = __loopVar_2;
            if (x > 0)
            {
                __comp_0.Add(x * 2);
            }
        }

#line (3, 5) - (3, 56) 1 "list_comp_sized_filter.spy"
        Sharpy.List<int> result = __comp_0;
#line (4, 5) - (6, 1) 1 "list_comp_sized_filter.spy"
        foreach (var __loopVar_3 in result)
        {
            var r = __loopVar_3;
#line (5, 9) - (5, 17) 1 "list_comp_sized_filter.spy"
            global::Sharpy.Builtins.Print(r);
        }
    }
}
