// Snapshot: Set comprehension
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class SetComprehension
{
    public static void Main()
    {
#line 3 "set_comprehension.spy"
        System.Collections.Generic.List<int> items = new System.Collections.Generic.List<int>()
        {
            1,
            2,
            2,
            3,
            3,
            3
        };
#line 4 "set_comprehension.spy"
        System.Collections.Generic.HashSet<int> result = items.Select(x => x).ToHashSet();
#line 5 "set_comprehension.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
    }
}
