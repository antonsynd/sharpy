#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassWithLoop0014
{
    public static class Program
    {
        public static void Main()
        {
#line 15 "class_with_loop_0014.spy"
            var acc = new Accumulator();
#line 16 "class_with_loop_0014.spy"
            var result = acc.SumTo(5);
#line 17 "class_with_loop_0014.spy"
            global::Sharpy.Core.Exports.Print(result);
#line 18 "class_with_loop_0014.spy"
            global::Sharpy.Core.Exports.Print(acc.Total);
        }
    }

    public class Accumulator
    {
        public int Total;
        public int SumTo(int n)
        {
#line 10 "class_with_loop_0014.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(1, n + 1))
            {
                var i = __loopVar_0;
#line 11 "class_with_loop_0014.spy"
                this.Total = this.Total + i;
            }

#line 12 "class_with_loop_0014.spy"
            return this.Total;
        }

        public Accumulator()
        {
#line 7 "class_with_loop_0014.spy"
            this.Total = 0;
        }
    }
}
