#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ForRangeWithStep0004
{
    public static class Program
    {
        public static void Main()
        {
#line 17 "for_range_with_step_0004.spy"
            var counter = new StepCounter();
#line 19 "for_range_with_step_0004.spy"
            counter.CountByTwos(10);
#line 20 "for_range_with_step_0004.spy"
            global::Sharpy.Core.Exports.Print(999);
#line 22 "for_range_with_step_0004.spy"
            counter.CountByFives(10, 30);
#line 23 "for_range_with_step_0004.spy"
            global::Sharpy.Core.Exports.Print(888);
#line 25 "for_range_with_step_0004.spy"
            counter.CountdownByThrees(20);
        }
    }

    public class StepCounter
    {
        public void CountByTwos(int limit)
        {
#line 5 "for_range_with_step_0004.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(0, limit, 2))
            {
                var i = __loopVar_0;
#line 6 "for_range_with_step_0004.spy"
                global::Sharpy.Core.Exports.Print(i);
            }
        }

        public void CountByFives(int start, int end)
        {
#line 9 "for_range_with_step_0004.spy"
            foreach (var __loopVar_1 in global::Sharpy.Core.Exports.Range(start, end, 5))
            {
                var i = __loopVar_1;
#line 10 "for_range_with_step_0004.spy"
                global::Sharpy.Core.Exports.Print(i);
            }
        }

        public void CountdownByThrees(int start)
        {
#line 13 "for_range_with_step_0004.spy"
            foreach (var __loopVar_2 in global::Sharpy.Core.Exports.Range(start, 0, -3))
            {
                var i = __loopVar_2;
#line 14 "for_range_with_step_0004.spy"
                global::Sharpy.Core.Exports.Print(i);
            }
        }
    }
}
