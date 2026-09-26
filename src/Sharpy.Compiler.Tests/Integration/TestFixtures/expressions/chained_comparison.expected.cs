#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ChainedComparison
{
    public static partial class ChainedComparisonModule
    {
        public static void Main()
        {
#line (2, 5) - (2, 16) 12 "chained_comparison.spy"
            int x = 5;
#line (3, 5) - (3, 22) 12 "chained_comparison.spy"
            global::Sharpy.Builtins.Print(1 < x && x < 10);
#line (4, 5) - (4, 22) 12 "chained_comparison.spy"
            global::Sharpy.Builtins.Print(1 < x && x > 10);
#line (5, 5) - (5, 21) 12 "chained_comparison.spy"
            global::Sharpy.Builtins.Print(0 < x && x < 3);
#line hidden
        }
    }
}
#line default
