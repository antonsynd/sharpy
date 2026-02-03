#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnreachableAfterContinue
{
    public static class Program
    {
        public static void Main()
        {
#line 2 "unreachable_after_continue.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(5))
            {
                var i = __loopVar_0;
#line 3 "unreachable_after_continue.spy"
                if (i == 2)
                {
#line 4 "unreachable_after_continue.spy"
                    continue;
#line 5 "unreachable_after_continue.spy"
                    global::Sharpy.Core.Exports.Print("never reached");
                }
            }

#line 6 "unreachable_after_continue.spy"
            global::Sharpy.Core.Exports.Print("done");
        }
    }
}
