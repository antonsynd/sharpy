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
#line (9, 5) - (9, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X == global::ComparisonOperators.Z);
#line (10, 5) - (10, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X == global::ComparisonOperators.Y);
#line (11, 5) - (11, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X != global::ComparisonOperators.Y);
#line (14, 5) - (14, 17) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X < global::ComparisonOperators.Y);
#line (15, 5) - (15, 17) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.Y > global::ComparisonOperators.X);
#line (16, 5) - (16, 17) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X > global::ComparisonOperators.Y);
#line (19, 5) - (19, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X <= global::ComparisonOperators.Z);
#line (20, 5) - (20, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.X >= global::ComparisonOperators.Z);
#line (21, 5) - (21, 18) 8 "comparison_operators.spy"
        global::Sharpy.Builtins.Print(global::ComparisonOperators.Y <= global::ComparisonOperators.X);
#line hidden
    }
}
#line default
