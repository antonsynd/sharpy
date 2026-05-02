// Snapshot: If/elif/else conditional branching
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class IfElifElse
{
    public static void Categorize(int x)
    {
#line (2, 5) - (9, 1) 1 "if_elif_else.spy"
        if (x > 0)
        {
#line (3, 9) - (3, 26) 1 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("positive");
        }
        else if (x < 0)
        {
#line (5, 9) - (5, 26) 1 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("negative");
        }
        else
        {
#line (7, 9) - (7, 22) 1 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("zero");
        }
    }

    public static void Main()
    {
#line (10, 5) - (10, 18) 1 "if_elif_else.spy"
        Categorize(5);
#line (11, 5) - (11, 19) 1 "if_elif_else.spy"
        Categorize(-3);
#line (12, 5) - (12, 18) 1 "if_elif_else.spy"
        Categorize(0);
    }
}
