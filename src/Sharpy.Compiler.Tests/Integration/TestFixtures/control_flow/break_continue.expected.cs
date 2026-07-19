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
#line (4, 5) - (4, 10) 8 "break_continue.spy"
        var i = 0;
#line (5, 5) - (13, 1) 8 "break_continue.spy"
        while (i < 10)
#line hidden
        {
#line (6, 9) - (6, 15) 12 "break_continue.spy"
            i = i + 1;
#line (7, 9) - (9, 1) 12 "break_continue.spy"
            if (i == 3)
#line hidden
            {
#line (8, 13) - (8, 22) 16 "break_continue.spy"
                continue;
#line hidden
            }

#line (9, 9) - (11, 1) 12 "break_continue.spy"
            if (i == 6)
#line hidden
            {
#line (10, 13) - (10, 19) 16 "break_continue.spy"
                break;
#line hidden
            }

#line (11, 9) - (11, 17) 12 "break_continue.spy"
            global::Sharpy.Builtins.Print(i);
#line hidden
        }

#line (13, 5) - (13, 15) 8 "break_continue.spy"
        global::Sharpy.Builtins.Print(100);
#line hidden
    }
}
#line default
