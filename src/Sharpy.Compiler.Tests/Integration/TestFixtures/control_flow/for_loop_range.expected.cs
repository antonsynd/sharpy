// Snapshot: For loop with range()
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ForLoopRange
{
    public static partial class ForLoopRangeModule
    {
        public static void Main()
        {
#line (2, 5) - (3, 17) 12 "for_loop_range.spy"
            foreach (var __loopVar_0 in global::Sharpy.Builtins.Range(5))
#line hidden
            {
                var i = __loopVar_0;
#line (3, 9) - (3, 17) 16 "for_loop_range.spy"
                global::Sharpy.Builtins.Print(i);
#line hidden
            }
        }
    }
}
#line default
