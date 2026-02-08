// Snapshot: Dictionary comprehension
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class DictComprehension
{
    public static void Main()
    {
#line 3 "dict_comprehension.spy"
        System.Collections.Generic.Dictionary<int, int> result = global::Sharpy.Builtins.Range(5).ToDictionary(i => i, i => i * 2);
#line 4 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
#line 5 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[0]);
#line 6 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[1]);
#line 7 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[2]);
#line 8 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[3]);
#line 9 "dict_comprehension.spy"
        global::Sharpy.Builtins.Print(result[4]);
    }
}
