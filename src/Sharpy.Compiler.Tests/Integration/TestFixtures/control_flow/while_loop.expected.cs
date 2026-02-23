// Snapshot: While loop
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class WhileLoop
{
    public static void Countdown(int n)
    {
#line 2 "while_loop.spy"
        while (n > 0)
        {
#line 3 "while_loop.spy"
            global::Sharpy.Builtins.Print(n);
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
