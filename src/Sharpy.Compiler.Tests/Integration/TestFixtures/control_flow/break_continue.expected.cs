// Snapshot: Break and continue statements in loops
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BreakContinue
{
    public static void Main()
    {
#line (4, 5) - (4, 10) 1 "break_continue.spy"
        var i = 0;
#line (5, 5) - (13, 1) 1 "break_continue.spy"
        while (i < 10)
        {
#line (6, 9) - (6, 15) 1 "break_continue.spy"
            i = i + 1;
#line (7, 9) - (9, 1) 1 "break_continue.spy"
            if (i == 3)
            {
#line (8, 13) - (8, 22) 1 "break_continue.spy"
                continue;
            }

#line (9, 9) - (11, 1) 1 "break_continue.spy"
            if (i == 6)
            {
#line (10, 13) - (10, 19) 1 "break_continue.spy"
                break;
            }

#line (11, 9) - (11, 17) 1 "break_continue.spy"
            global::Sharpy.Builtins.Print(i);
        }

#line (13, 5) - (13, 15) 1 "break_continue.spy"
        global::Sharpy.Builtins.Print(100);
    }
}
