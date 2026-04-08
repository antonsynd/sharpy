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
#line 2 "if_elif_else.spy"
        if (x > 0)
        {
#line 3 "if_elif_else.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"positive"));
        }
        else if (x < 0)
        {
#line 5 "if_elif_else.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"negative"));
        }
        else
        {
#line 7 "if_elif_else.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"zero"));
        }
    }

    public static void Main()
    {
#line 10 "if_elif_else.spy"
        Categorize(5);
#line 11 "if_elif_else.spy"
        Categorize(-3);
#line 12 "if_elif_else.spy"
        Categorize(0);
    }
}
