#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ForLoopRange
{
    public static class Program
    {
        public static void Main()
        {
#line 2 "for_loop_range.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(5))
            {
                var i = __loopVar_0;
#line 3 "for_loop_range.spy"
                global::Sharpy.Core.Exports.Print(i);
            }
        }
    }
}
