#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassWithLoop0004
{
    public static class Program
    {
        public static void Main()
        {
#line 15 "class_with_loop_0004.spy"
            var c = new Counter(5);
#line 16 "class_with_loop_0004.spy"
            c.IncrementTimes(3);
#line 17 "class_with_loop_0004.spy"
            global::Sharpy.Core.Exports.Print(c.Count);
        }
    }

    public class Counter
    {
        public int Count;
        public void IncrementTimes(int times)
        {
#line 9 "class_with_loop_0004.spy"
            int i = 0;
#line 10 "class_with_loop_0004.spy"
            while (i < times)
            {
#line 11 "class_with_loop_0004.spy"
                this.Count = this.Count + 1;
#line 12 "class_with_loop_0004.spy"
                i = i + 1;
            }
        }

        public Counter(int start)
        {
#line 6 "class_with_loop_0004.spy"
            this.Count = start;
        }
    }
}
