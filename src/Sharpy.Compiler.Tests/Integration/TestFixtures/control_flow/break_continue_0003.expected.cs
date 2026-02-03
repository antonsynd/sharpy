#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.BreakContinue0003
{
    public static class Program
    {
        public static int FindFirstMultiple(int target, int limit)
        {
#line 4 "break_continue_0003.spy"
            int result = -1;
#line 5 "break_continue_0003.spy"
            int i = 1;
#line 6 "break_continue_0003.spy"
            while (i <= limit)
            {
#line 7 "break_continue_0003.spy"
                if (i % target == 0)
                {
#line 8 "break_continue_0003.spy"
                    result = i;
#line 9 "break_continue_0003.spy"
                    break;
                }

#line 10 "break_continue_0003.spy"
                i = i + 1;
            }

#line 11 "break_continue_0003.spy"
            return result;
        }

        public static int SumOddNumbers(int maxVal)
        {
#line 14 "break_continue_0003.spy"
            int total = 0;
#line 15 "break_continue_0003.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(1, maxVal + 1))
            {
                var i = __loopVar_0;
#line 16 "break_continue_0003.spy"
                if (i % 2 == 0)
                {
#line 17 "break_continue_0003.spy"
                    continue;
                }

#line 18 "break_continue_0003.spy"
                total = total + i;
            }

#line 19 "break_continue_0003.spy"
            return total;
        }

        public static int FirstMult = FindFirstMultiple(7, 50);
        public static int OddSum = SumOddNumbers(10);
        public static int Outer = 0;
        public static void Main()
        {
#line 27 "break_continue_0003.spy"
            global::Sharpy.Core.Exports.Print(FirstMult);
#line 30 "break_continue_0003.spy"
            global::Sharpy.Core.Exports.Print(OddSum);
#line 33 "break_continue_0003.spy"
            while (Outer < 5)
            {
#line 34 "break_continue_0003.spy"
                int inner = 0;
#line 35 "break_continue_0003.spy"
                while (inner < 5)
                {
#line 36 "break_continue_0003.spy"
                    if (inner == 3)
                    {
#line 37 "break_continue_0003.spy"
                        break;
                    }

#line 38 "break_continue_0003.spy"
                    inner = inner + 1;
                }

#line 39 "break_continue_0003.spy"
                global::Sharpy.Core.Exports.Print(inner);
#line 40 "break_continue_0003.spy"
                Outer = Outer + 1;
            }
        }
    }
}
