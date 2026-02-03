#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FibonacciIterative
{
    public static class Program
    {
        public static int Fibonacci(int n)
        {
#line 2 "fibonacci_iterative.spy"
            if (n <= 1)
            {
#line 3 "fibonacci_iterative.spy"
                return n;
            }

#line 5 "fibonacci_iterative.spy"
            int a = 0;
#line 6 "fibonacci_iterative.spy"
            int b = 1;
#line 7 "fibonacci_iterative.spy"
            int i = 2;
#line 9 "fibonacci_iterative.spy"
            while (i <= n)
            {
#line 10 "fibonacci_iterative.spy"
                int temp = a + b;
#line 11 "fibonacci_iterative.spy"
                a = b;
#line 12 "fibonacci_iterative.spy"
                b = temp;
#line 13 "fibonacci_iterative.spy"
                i = i + 1;
            }

#line 15 "fibonacci_iterative.spy"
            return b;
        }

        public static int Result = Fibonacci(10);
        public static void Main()
        {
#line 20 "fibonacci_iterative.spy"
            global::Sharpy.Core.Exports.Print(Result);
        }
    }
}
