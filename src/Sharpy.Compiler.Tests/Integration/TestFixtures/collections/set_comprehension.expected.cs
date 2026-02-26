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
#line 3 "set_comprehension.spy"
        Sharpy.List<int> items = new Sharpy.List<int>()
        {
            1,
            2,
            2,
            3,
            3,
            3
        };
#line 4 "set_comprehension.spy"
        Sharpy.Set<int> result = new Sharpy.Set<int>(items.Select((int x) => x));
#line 5 "set_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
    }
}
