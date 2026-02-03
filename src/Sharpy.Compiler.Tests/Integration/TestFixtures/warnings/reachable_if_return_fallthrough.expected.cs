#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ReachableIfReturnFallthrough
{
    public static class Program
    {
        public static int Foo(int x)
        {
#line 3 "reachable_if_return_fallthrough.spy"
            if (x > 0)
            {
#line 4 "reachable_if_return_fallthrough.spy"
                return 1;
            }

#line 6 "reachable_if_return_fallthrough.spy"
            return -1;
        }

        public static void Main()
        {
#line 9 "reachable_if_return_fallthrough.spy"
            global::Sharpy.Core.Exports.Print(Foo(5));
#line 10 "reachable_if_return_fallthrough.spy"
            global::Sharpy.Core.Exports.Print(Foo(-3));
        }
    }
}
