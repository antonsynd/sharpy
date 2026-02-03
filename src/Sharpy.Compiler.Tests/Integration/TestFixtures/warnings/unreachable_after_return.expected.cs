#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnreachableAfterReturn
{
    public static class Program
    {
        public static int Foo()
        {
#line 2 "unreachable_after_return.spy"
            return 42;
#line 3 "unreachable_after_return.spy"
            int x = 10;
#line 4 "unreachable_after_return.spy"
            return x;
        }

        public static void Main()
        {
#line 7 "unreachable_after_return.spy"
            global::Sharpy.Core.Exports.Print(Foo());
        }
    }
}
