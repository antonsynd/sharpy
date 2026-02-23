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
#line 3 "list_comprehension.spy"
        Sharpy.List<int> result = new Sharpy.List<int>(global::Sharpy.Builtins.Range(5).Select(x => x * 2));
#line 4 "list_comprehension.spy"
        foreach (var __loopVar_0 in result)
        {
            var item = __loopVar_0;
#line 5 "list_comprehension.spy"
            global::Sharpy.Builtins.Print(item);
        }

#line 6 "list_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
    }
}
