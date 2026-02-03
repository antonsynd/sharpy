#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnreachableBothBranchesReturn
{
    public static class Program
    {
        public static int Foo(int x)
        {
#line 2 "unreachable_both_branches_return.spy"
            if (x > 0)
            {
#line 3 "unreachable_both_branches_return.spy"
                return 1;
            }
            else
            {
#line 5 "unreachable_both_branches_return.spy"
                return -1;
            }

#line 6 "unreachable_both_branches_return.spy"
            global::Sharpy.Core.Exports.Print("unreachable");
#line 7 "unreachable_both_branches_return.spy"
            return 0;
        }

        public static void Main()
        {
#line 10 "unreachable_both_branches_return.spy"
            global::Sharpy.Core.Exports.Print(Foo(5));
        }
    }
}
