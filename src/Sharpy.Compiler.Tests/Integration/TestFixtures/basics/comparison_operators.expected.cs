// Snapshot: Comparison operators (==, !=, <, >, <=, >=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ComparisonOperators
{
    public static partial class ComparisonOperatorsModule
    {
        public static int X = 10;
        public static int Y = 20;
        public static int Z = 10;
        public static void Main()
        {
#line (9, 5) - (9, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X == global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (10, 5) - (10, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X == global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (11, 5) - (11, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X != global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (14, 5) - (14, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X < global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (15, 5) - (15, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.Y > global::ComparisonOperators.ComparisonOperatorsModule.X);
#line (16, 5) - (16, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X > global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (19, 5) - (19, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X <= global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (20, 5) - (20, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X >= global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (21, 5) - (21, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.Y <= global::ComparisonOperators.ComparisonOperatorsModule.X);
#line hidden
        }
    }
}
#line default
