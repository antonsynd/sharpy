// Snapshot: For loop with range()
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ForLoopRange
{
    public static void Main()
    {
#line 2 "for_loop_range.spy"
        foreach (var __loopVar_0 in global::Sharpy.Builtins.Range(5))
        {
            var i = __loopVar_0;
#line 3 "for_loop_range.spy"
            global::Sharpy.Builtins.Print(i);
        }
    }
}
