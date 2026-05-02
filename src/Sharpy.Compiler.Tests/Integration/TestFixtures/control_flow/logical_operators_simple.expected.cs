// Snapshot: Logical operators (and, or, not)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class LogicalOperatorsSimple
{
    public static void Main()
    {
#line (3, 5) - (3, 20) 1 "logical_operators_simple.spy"
        bool a = true;
#line (4, 5) - (4, 21) 1 "logical_operators_simple.spy"
        bool b = false;
#line (7, 5) - (7, 29) 1 "logical_operators_simple.spy"
        bool result1 = a && b;
#line (8, 5) - (8, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result1);
#line (10, 5) - (10, 28) 1 "logical_operators_simple.spy"
        bool result2 = a || b;
#line (11, 5) - (11, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result2);
#line (13, 5) - (13, 27) 1 "logical_operators_simple.spy"
        bool result3 = !a;
#line (14, 5) - (14, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result3);
#line (16, 5) - (16, 27) 1 "logical_operators_simple.spy"
        bool result4 = !b;
#line (17, 5) - (17, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result4);
#line (20, 5) - (20, 17) 1 "logical_operators_simple.spy"
        int x = 10;
#line (21, 5) - (21, 16) 1 "logical_operators_simple.spy"
        int y = 5;
#line (23, 5) - (23, 38) 1 "logical_operators_simple.spy"
        bool result5 = x > 5 && y < 10;
#line (24, 5) - (24, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result5);
#line (26, 5) - (26, 36) 1 "logical_operators_simple.spy"
        bool result6 = x < 5 || y > 0;
#line (27, 5) - (27, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result6);
#line (29, 5) - (29, 34) 1 "logical_operators_simple.spy"
        bool result7 = !(x == y);
#line (30, 5) - (30, 19) 1 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result7);
    }
}
