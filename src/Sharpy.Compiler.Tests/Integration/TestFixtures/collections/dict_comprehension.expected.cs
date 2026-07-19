// Snapshot: Dictionary comprehension
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DictComprehension
{
    public static void Main()
    {
        var __src_1 = global::Sharpy.Builtins.Range(5);
        var __comp_0 = new Sharpy.Dict<int, int>(((global::Sharpy.ISized)__src_1).Count);
        foreach (var __loopVar_2 in __src_1)
        {
            var i = __loopVar_2;
            __comp_0[i] = i * 2;
        }

#line (3, 5) - (3, 59) 8 "dict_comprehension.spy"
        Sharpy.Dict<int, int> result = __comp_0;
#line (4, 5) - (4, 23) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
#line (5, 5) - (5, 21) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[0]);
#line (6, 5) - (6, 21) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[1]);
#line (7, 5) - (7, 21) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[2]);
#line (8, 5) - (8, 21) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[3]);
#line (9, 5) - (9, 21) 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[4]);
#line hidden
    }
}
#line default
