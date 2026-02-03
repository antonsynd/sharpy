#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LoopInFunction0001
{
    public static class Program
    {
        public static int SumEvens(int limit)
        {
#line 3 "loop_in_function_0001.spy"
            int total = 0;
#line 4 "loop_in_function_0001.spy"
            int i = 0;
#line 5 "loop_in_function_0001.spy"
            while (i <= limit)
            {
#line 6 "loop_in_function_0001.spy"
                if (i % 2 == 0)
                {
#line 7 "loop_in_function_0001.spy"
                    total = total + i;
                }

#line 8 "loop_in_function_0001.spy"
                i = i + 1;
            }

#line 9 "loop_in_function_0001.spy"
            return total;
        }

        public static void Main()
        {
#line 12 "loop_in_function_0001.spy"
            int result = SumEvens(10);
#line 13 "loop_in_function_0001.spy"
            global::Sharpy.Core.Exports.Print(result);
        }
    }
}
