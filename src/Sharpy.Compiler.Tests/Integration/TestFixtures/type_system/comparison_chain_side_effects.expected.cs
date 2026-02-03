#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ComparisonChainSideEffects
{
    public static class Program
    {
        public static void Main()
        {
#line 15 "comparison_chain_side_effects.spy"
            Counter c = new Counter();
#line 20 "comparison_chain_side_effects.spy"
            bool result = 0 < (c.Increment() is var __cmp_0 ? __cmp_0 : __cmp_0) && __cmp_0 < 3;
#line 21 "comparison_chain_side_effects.spy"
            global::Sharpy.Core.Exports.Print(result);
#line 22 "comparison_chain_side_effects.spy"
            global::Sharpy.Core.Exports.Print(c.Value);
#line 25 "comparison_chain_side_effects.spy"
            Counter c2 = new Counter();
#line 27 "comparison_chain_side_effects.spy"
            bool result2 = 10 < (c2.Increment() is var __cmp_1 ? __cmp_1 : __cmp_1) && __cmp_1 < 20;
#line 28 "comparison_chain_side_effects.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 29 "comparison_chain_side_effects.spy"
            global::Sharpy.Core.Exports.Print(c2.Value);
#line 32 "comparison_chain_side_effects.spy"
            int x = 1;
#line 33 "comparison_chain_side_effects.spy"
            int y = 2;
#line 34 "comparison_chain_side_effects.spy"
            int z = 3;
#line 35 "comparison_chain_side_effects.spy"
            global::Sharpy.Core.Exports.Print(x < y && y < z);
        }
    }

    public class Counter
    {
        public int Value;
        public int Increment()
        {
#line 11 "comparison_chain_side_effects.spy"
            this.Value = this.Value + 1;
#line 12 "comparison_chain_side_effects.spy"
            return this.Value;
        }

        public Counter()
        {
#line 8 "comparison_chain_side_effects.spy"
            this.Value = 0;
        }
    }
}
