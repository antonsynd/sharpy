// Snapshot: Function calling another function
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class FunctionCallingFunction0002
{
    public static int CalculateSum(int a, int b)
    {
#line 3 "function_calling_function_0002.spy"
        return a + b;
    }

    public static int CalculateProduct(int a, int b)
    {
#line 6 "function_calling_function_0002.spy"
        return a * b;
    }

    public static int CombinedOperation(int x, int y)
    {
#line 9 "function_calling_function_0002.spy"
        var sumResult = CalculateSum(x, y);
#line 10 "function_calling_function_0002.spy"
        var productResult = CalculateProduct(x, y);
#line 11 "function_calling_function_0002.spy"
        return sumResult + productResult;
    }

    public static void Main()
    {
#line 14 "function_calling_function_0002.spy"
        var result = CombinedOperation(3, 7);
#line 15 "function_calling_function_0002.spy"
        global::Sharpy.Builtins.Print(result);
    }
}
