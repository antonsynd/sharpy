// Snapshot: Arithmetic operators (+, -, *, /, //, %, **)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ArithmeticOperators
{
    public static partial class ArithmeticOperatorsModule
    {
        public static int A = 15;
        public static int B = 4;
        public static int SumResult = global::ArithmeticOperators.ArithmeticOperatorsModule.A + global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        public static int DiffResult = global::ArithmeticOperators.ArithmeticOperatorsModule.A - global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        public static int ProdResult = global::ArithmeticOperators.ArithmeticOperatorsModule.A * global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        public static int DivResult = global::Sharpy.Builtins.FloorDiv(global::ArithmeticOperators.ArithmeticOperatorsModule.A, global::ArithmeticOperators.ArithmeticOperatorsModule.B);
        public static int ModResult = global::Sharpy.Builtins.FloorMod(global::ArithmeticOperators.ArithmeticOperatorsModule.A, global::ArithmeticOperators.ArithmeticOperatorsModule.B);
        public static void Main()
        {
#line (13, 5) - (13, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(SumResult);
#line (14, 5) - (14, 23) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(DiffResult);
#line (15, 5) - (15, 23) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(ProdResult);
#line (16, 5) - (16, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(DivResult);
#line (17, 5) - (17, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(ModResult);
#line hidden
        }
    }
}
#line default
