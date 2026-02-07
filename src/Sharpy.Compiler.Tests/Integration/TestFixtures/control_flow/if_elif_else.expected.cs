#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class IfElifElse
{
    public static void Categorize(int x)
    {
#line 2 "if_elif_else.spy"
        if (x > 0)
        {
#line 3 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("positive");
        }
        else if (x < 0)
        {
#line 5 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("negative");
        }
        else
        {
#line 7 "if_elif_else.spy"
            global::Sharpy.Builtins.Print("zero");
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
