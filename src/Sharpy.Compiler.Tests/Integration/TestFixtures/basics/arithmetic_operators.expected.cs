#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

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
            global::Sharpy.Builtins.Print(SumResult);
#line 14 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(DiffResult);
#line 15 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(ProdResult);
#line 16 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(DivResult);
#line 17 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(ModResult);
        }
    }
}
