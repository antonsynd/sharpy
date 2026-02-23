// Snapshot: Type shorthand syntax for generic collections
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListShorthand
{
    public static int SumList(Sharpy.List<int> items)
    {
#line 3 "list_shorthand.spy"
        int total = 0;
#line 4 "list_shorthand.spy"
        foreach (var __loopVar_0 in items)
        {
            var item = __loopVar_0;
#line 5 "list_shorthand.spy"
            total = total + item;
        }

#line 6 "list_shorthand.spy"
        return total;
    }

    public static void Main()
    {
#line 9 "list_shorthand.spy"
        Sharpy.List<int> numbers = new Sharpy.List<int>()
        {
            1,
            2,
            3,
            4,
            5
        };
#line 10 "list_shorthand.spy"
        global::Sharpy.Builtins.Print(SumList(numbers));
    }
}
