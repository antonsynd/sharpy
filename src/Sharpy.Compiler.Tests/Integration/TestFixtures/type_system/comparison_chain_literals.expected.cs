#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ComparisonChainLiterals
{
    public static class Program
    {
        public static void Main()
        {
#line 6 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(1 < 2 && 2 < 3);
#line 7 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(1 < 2 && 2 < 2);
#line 8 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(3 > 2 && 2 > 1);
#line 9 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(1 <= 1 && 1 <= 2);
#line 12 "comparison_chain_literals.spy"
            int x = 10;
#line 13 "comparison_chain_literals.spy"
            int y = 20;
#line 14 "comparison_chain_literals.spy"
            int z = 30;
#line 15 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(x < y && y < z);
#line 16 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(x < y && y > z);
#line 19 "comparison_chain_literals.spy"
            int a = 1;
#line 20 "comparison_chain_literals.spy"
            int b = 2;
#line 21 "comparison_chain_literals.spy"
            int c = 3;
#line 22 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(a < b && b <= c);
#line 23 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(a <= b && b < c);
#line 26 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(1 < 2 && 2 < 3 && 3 < 4);
#line 27 "comparison_chain_literals.spy"
            global::Sharpy.Core.Exports.Print(1 < 2 && 2 < 3 && 3 < 2);
        }
    }
}
