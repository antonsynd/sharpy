// Snapshot: Comparison operators (==, !=, <, >, <=, >=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class ComparisonOperators
{
    public static int X = 10;
    public static int Y = 20;
    public static int Z = 10;
    public static void Main()
    {
#line 9 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X == Z);
#line 10 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X == Y);
#line 11 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X != Y);
#line 14 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X < Y);
#line 15 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(Y > X);
#line 16 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X > Y);
#line 19 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X <= Z);
#line 20 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X >= Z);
#line 21 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(Y <= X);
    }
}
