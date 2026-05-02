// Snapshot: Arithmetic operators (+, -, *, /, //, %, **)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ArithmeticOperators
{
    public static int A = 15;
    public static int B = 4;
    public static int SumResult = A + B;
    public static int DiffResult = A - B;
    public static int ProdResult = A * B;
    public static int DivResult = (B == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(A) / B)));
    public static int ModResult = A % B;
    public static void Main()
    {
#line (13, 5) - (13, 22) 1 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(SumResult);
#line (14, 5) - (14, 23) 1 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(DiffResult);
#line (15, 5) - (15, 23) 1 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(ProdResult);
#line (16, 5) - (16, 22) 1 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(DivResult);
#line (17, 5) - (17, 22) 1 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(ModResult);
    }
}
