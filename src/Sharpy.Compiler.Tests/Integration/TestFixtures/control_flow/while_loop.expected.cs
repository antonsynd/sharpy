#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.WhileLoop
{
    public static class Program
    {
        public static void Countdown(int n)
        {
#line 2 "while_loop.spy"
            while (n > 0)
            {
#line 3 "while_loop.spy"
                global::Sharpy.Core.Exports.Print(n);
#line 4 "while_loop.spy"
                n = n - 1;
            }
        }

        public static void Main()
        {
#line 7 "while_loop.spy"
            Countdown(3);
        }
    }
}
