#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NestedComprehension
{
    public static void Main()
    {
#line (2, 5) - (2, 56) 1 "nested_comprehension.spy"
        Sharpy.List<Sharpy.List<int>> matrix = new Sharpy.List<Sharpy.List<int>>()
        {
            new Sharpy.List<int>()
            {
                1,
                2
            },
            new Sharpy.List<int>()
            {
                3,
                4
            },
            new Sharpy.List<int>()
            {
                5,
                6
            }
        };
        var __comp_0 = new Sharpy.List<int>();
        foreach (var __loopVar_2 in matrix)
        {
            var row = __loopVar_2;
            foreach (var __loopVar_1 in row)
            {
                var x = __loopVar_1;
                __comp_0.Add(x);
            }
        }

#line (3, 5) - (3, 58) 1 "nested_comprehension.spy"
        Sharpy.List<int> flat = __comp_0;
#line (4, 5) - (4, 16) 1 "nested_comprehension.spy"
        global::Sharpy.Builtins.Print(flat);
    }
}
