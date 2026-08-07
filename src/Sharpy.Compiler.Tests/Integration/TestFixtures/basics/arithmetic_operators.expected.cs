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
    public static int DivResult = global::Sharpy.Builtins.FloorDiv(A, B);
    public static int ModResult = global::Sharpy.Builtins.FloorMod(A, B);
    public static void Main()
    {
#line (13, 5) - (13, 22) 8 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(SumResult);
#line (14, 5) - (14, 23) 8 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(DiffResult);
#line (15, 5) - (15, 23) 8 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(ProdResult);
#line (16, 5) - (16, 22) 8 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(DivResult);
#line (17, 5) - (17, 22) 8 "arithmetic_operators.spy"
        global::Sharpy.Builtins.Print(ModResult);
#line hidden
    }
}
#line default
