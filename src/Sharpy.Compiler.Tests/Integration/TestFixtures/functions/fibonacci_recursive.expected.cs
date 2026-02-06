#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy.FibonacciRecursive
{
    public static class Program
    {
        public static int Fibonacci(int n)
        {
#line 2 "fibonacci_recursive.spy"
            if (n <= 1)
            {
#line 3 "fibonacci_recursive.spy"
                return n;
            }

#line 4 "fibonacci_recursive.spy"
            return Fibonacci(n - 1) + Fibonacci(n - 2);
        }

        public static int Result = Fibonacci(10);
        public static void Main()
        {
#line 9 "fibonacci_recursive.spy"
            global::Sharpy.Builtins.Print(Result);
        }
    }
}
