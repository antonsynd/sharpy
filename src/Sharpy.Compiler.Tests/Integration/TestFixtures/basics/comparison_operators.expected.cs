// Snapshot: Comparison operators (==, !=, <, >, <=, >=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ComparisonOperators
{
    public static int X = 10;
    public static int Y = 20;
    public static int Z = 10;
    public static void Main()
    {
#line (9, 5) - (9, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X == Z);
#line (10, 5) - (10, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X == Y);
#line (11, 5) - (11, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X != Y);
#line (14, 5) - (14, 17) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X < Y);
#line (15, 5) - (15, 17) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(Y > X);
#line (16, 5) - (16, 17) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X > Y);
#line (19, 5) - (19, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X <= Z);
#line (20, 5) - (20, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(X >= Z);
#line (21, 5) - (21, 18) 1 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(Y <= X);
    }
}
