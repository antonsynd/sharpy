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
#line (3, 5) - (3, 59) 1 "dict_comprehension.spy"
        Sharpy.Dict<int, int> result = (Sharpy.Dict<int, int>)(global::Sharpy.Builtins.Range(5).ToDictionary(i => i, i => i * 2));
#line (4, 5) - (4, 23) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
#line (5, 5) - (5, 21) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[0]);
#line (6, 5) - (6, 21) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[1]);
#line (7, 5) - (7, 21) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[2]);
#line (8, 5) - (8, 21) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[3]);
#line (9, 5) - (9, 21) 1 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[4]);
    }
}
