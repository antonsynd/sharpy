#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Program
    {
        public static T Identity<T>(T x)
        {
#line 3 "generic_function.spy"
            return x;
        }

        public static T Swap<T>(T a, T b)
        {
#line 6 "generic_function.spy"
            T temp = a;
#line 7 "generic_function.spy"
            a = b;
#line 8 "generic_function.spy"
            return a;
        }

        public static void Main()
        {
#line 12 "generic_function.spy"
            int resultInt = Identity<int>(42);
#line 13 "generic_function.spy"
            global::Sharpy.Builtins.Print(resultInt);
#line 16 "generic_function.spy"
            int x = 42;
#line 17 "generic_function.spy"
            int y = 100;
#line 18 "generic_function.spy"
            int result = Swap<int>(x, y);
#line 19 "generic_function.spy"
            global::Sharpy.Builtins.Print(result);
#line 22 "generic_function.spy"
            double a = 3.14;
#line 23 "generic_function.spy"
            double b = 2.71;
#line 24 "generic_function.spy"
            double resultF = Swap<double>(a, b);
#line 25 "generic_function.spy"
            global::Sharpy.Builtins.Print(resultF);
        }
    }
}
