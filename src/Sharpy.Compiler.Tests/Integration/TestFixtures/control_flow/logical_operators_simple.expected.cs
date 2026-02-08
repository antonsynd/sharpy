// Snapshot: Logical operators (and, or, not)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class LogicalOperatorsSimple
{
    public static void Main()
    {
#line 3 "logical_operators_simple.spy"
        bool a = true;
#line 4 "logical_operators_simple.spy"
        bool b = false;
#line 7 "logical_operators_simple.spy"
        bool result1 = a && b;
#line 8 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result1);
#line 10 "logical_operators_simple.spy"
        bool result2 = a || b;
#line 11 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result2);
#line 13 "logical_operators_simple.spy"
        bool result3 = !a;
#line 14 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result3);
#line 16 "logical_operators_simple.spy"
        bool result4 = !b;
#line 17 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result4);
#line 20 "logical_operators_simple.spy"
        int x = 10;
#line 21 "logical_operators_simple.spy"
        int y = 5;
#line 23 "logical_operators_simple.spy"
        bool result5 = x > 5 && y < 10;
#line 24 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result5);
#line 26 "logical_operators_simple.spy"
        bool result6 = x < 5 || y > 0;
#line 27 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result6);
#line 29 "logical_operators_simple.spy"
        bool result7 = !(x == y);
#line 30 "logical_operators_simple.spy"
        global::Sharpy.Builtins.Print(result7);
    }
}
