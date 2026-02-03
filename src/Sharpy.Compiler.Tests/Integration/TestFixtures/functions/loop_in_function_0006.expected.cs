#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LoopInFunction0006
{
    public static class Program
    {
        public static int SumRange(int n)
        {
#line 3 "loop_in_function_0006.spy"
            int total = 0;
#line 4 "loop_in_function_0006.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(n))
            {
                var i = __loopVar_0;
#line 5 "loop_in_function_0006.spy"
                total = total + i;
            }

#line 6 "loop_in_function_0006.spy"
            return total;
        }

        public static void Main()
        {
#line 9 "loop_in_function_0006.spy"
            var result = SumRange(5);
#line 10 "loop_in_function_0006.spy"
            global::Sharpy.Core.Exports.Print(result);
#line 12 "loop_in_function_0006.spy"
            var result2 = SumRange(10);
#line 13 "loop_in_function_0006.spy"
            global::Sharpy.Core.Exports.Print(result2);
        }
    }
}
