// Snapshot: Function calling another function
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class FunctionCallingFunction0002
{
    public static int CalculateSum(int a, int b)
    {
#line (3, 5) - (3, 18) 1 "function_calling_function_0002.spy"
        return a + b;
    }

    public static int CalculateProduct(int a, int b)
    {
#line (6, 5) - (6, 18) 1 "function_calling_function_0002.spy"
        return a * b;
    }

    public static int CombinedOperation(int x, int y)
    {
#line (9, 5) - (9, 37) 1 "function_calling_function_0002.spy"
        var sumResult = CalculateSum(x, y);
#line (10, 5) - (10, 45) 1 "function_calling_function_0002.spy"
        var productResult = CalculateProduct(x, y);
#line (11, 5) - (11, 40) 1 "function_calling_function_0002.spy"
        return sumResult + productResult;
    }

    public static void Main()
    {
#line (14, 5) - (14, 38) 1 "function_calling_function_0002.spy"
        var result = CombinedOperation(3, 7);
#line (15, 5) - (15, 18) 1 "function_calling_function_0002.spy"
        global::Sharpy.Builtins.Print(result);
    }
}
