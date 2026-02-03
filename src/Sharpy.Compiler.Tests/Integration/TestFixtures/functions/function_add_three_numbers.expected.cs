#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FunctionAddThreeNumbers
{
    public static class Program
    {
        public static int AddThreeNumbers(int a, int b, int c)
        {
#line 3 "function_add_three_numbers.spy"
            int result = a + b + c;
#line 4 "function_add_three_numbers.spy"
            return result;
        }

        public static int X = 5;
        public static int Y = 12;
        public static int Z = 8;
        public static int SumValue = AddThreeNumbers(X, Y, Z);
        public static void Main()
        {
#line 12 "function_add_three_numbers.spy"
            global::Sharpy.Core.Exports.Print(SumValue);
        }
    }
}
