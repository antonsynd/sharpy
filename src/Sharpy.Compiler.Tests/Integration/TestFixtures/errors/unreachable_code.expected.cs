#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnreachableCode
{
    public static class Program
    {
        public static int Foo()
        {
#line 2 "unreachable_code.spy"
            return 1;
#line 3 "unreachable_code.spy"
            int x = 2;
#line 4 "unreachable_code.spy"
            return x;
        }

        public static void Main()
        {
#line 7 "unreachable_code.spy"
            global::Sharpy.Core.Exports.Print(Foo());
        }
    }
}
