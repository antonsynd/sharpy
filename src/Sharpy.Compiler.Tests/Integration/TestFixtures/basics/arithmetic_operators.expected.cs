#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ArithmeticOperators
{
    public static class Program
    {
        public static int A = 15;
        public static int B = 4;
        public static int SumResult = A + B;
        public static int DiffResult = A - B;
        public static int ProdResult = A * B;
        public static int DivResult = (int)System.Math.Floor((double)((double)(A) / B));
        public static int ModResult = A % B;
        public static void Main()
        {
#line 13 "arithmetic_operators.spy"
            global::Sharpy.Core.Exports.Print(SumResult);
#line 14 "arithmetic_operators.spy"
            global::Sharpy.Core.Exports.Print(DiffResult);
#line 15 "arithmetic_operators.spy"
            global::Sharpy.Core.Exports.Print(ProdResult);
#line 16 "arithmetic_operators.spy"
            global::Sharpy.Core.Exports.Print(DivResult);
#line 17 "arithmetic_operators.spy"
            global::Sharpy.Core.Exports.Print(ModResult);
        }
    }
}
