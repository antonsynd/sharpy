#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ComparisonFunctions
{
    public static class Program
    {
        public static int CompareIntegers(int a, int b)
        {
#line 5 "comparison_functions.spy"
            if (a > b)
            {
#line 6 "comparison_functions.spy"
                return 1;
            }
            else if (a < b)
            {
#line 8 "comparison_functions.spy"
                return -1;
            }
            else
            {
#line 10 "comparison_functions.spy"
                return 0;
            }
        }

        public static bool CheckRange(int value, int low, int high)
        {
#line 14 "comparison_functions.spy"
            return value >= low && value <= high;
        }

        public static int X = 42;
        public static int Y = 42;
        public static int Z = 99;
        public static void Main()
        {
#line 22 "comparison_functions.spy"
            var result1 = CompareIntegers(10, 5);
#line 23 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 25 "comparison_functions.spy"
            var result2 = CompareIntegers(3, 7);
#line 26 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 28 "comparison_functions.spy"
            var result3 = CompareIntegers(4, 4);
#line 29 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(result3);
#line 32 "comparison_functions.spy"
            var inRange = CheckRange(15, 10, 20);
#line 33 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(inRange);
#line 35 "comparison_functions.spy"
            var outOfRange = CheckRange(5, 10, 20);
#line 36 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(outOfRange);
#line 40 "comparison_functions.spy"
            var equalsCheck = X == Y;
#line 41 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(equalsCheck);
#line 43 "comparison_functions.spy"
            var notEqualsCheck = X != Z;
#line 44 "comparison_functions.spy"
            global::Sharpy.Core.Exports.Print(notEqualsCheck);
        }
    }
}
