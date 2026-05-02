// Snapshot: Generic function with type parameters
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class GenericFunction
{
    public static T Identity<T>(T x)
    {
#line (3, 5) - (3, 14) 1 "generic_function.spy"
        return x;
    }

    public static T Swap<T>(T a, T b)
    {
#line (6, 5) - (6, 17) 1 "generic_function.spy"
        T temp = a;
#line (7, 5) - (7, 10) 1 "generic_function.spy"
        a = b;
#line (8, 5) - (8, 14) 1 "generic_function.spy"
        return a;
    }

    public static void Main()
    {
#line (12, 5) - (12, 41) 1 "generic_function.spy"
        int resultInt = Identity<int>(42);
#line (13, 5) - (13, 22) 1 "generic_function.spy"
        global::Sharpy.Builtins.Print(resultInt);
#line (16, 5) - (16, 17) 1 "generic_function.spy"
        int x = 42;
#line (17, 5) - (17, 18) 1 "generic_function.spy"
        int y = 100;
#line (18, 5) - (18, 35) 1 "generic_function.spy"
        int result = Swap<int>(x, y);
#line (19, 5) - (19, 18) 1 "generic_function.spy"
        global::Sharpy.Builtins.Print(result);
#line (22, 5) - (22, 21) 1 "generic_function.spy"
        double a = 3.14d;
#line (23, 5) - (23, 21) 1 "generic_function.spy"
        double b = 2.71d;
#line (24, 5) - (24, 41) 1 "generic_function.spy"
        double resultF = Swap<double>(a, b);
#line (25, 5) - (25, 20) 1 "generic_function.spy"
        global::Sharpy.Builtins.Print(resultF);
    }
}
