// Snapshot: Recursive function with return type annotation
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class FibonacciRecursive
{
    public static int Fibonacci(int n)
    {
#line (2, 5) - (4, 1) 8 "fibonacci_recursive.spy"
        if (n <= 1)
#line hidden
        {
#line (3, 9) - (3, 18) 12 "fibonacci_recursive.spy"
            return n;
#line hidden
        }

#line (4, 5) - (4, 48) 8 "fibonacci_recursive.spy"
        return Fibonacci(n - 1) + Fibonacci(n - 2);
#line hidden
    }

    public static int Result = Fibonacci(10);
    public static void Main()
    {
#line (9, 5) - (9, 18) 8 "fibonacci_recursive.spy"
        global::Sharpy.Builtins.Print(Result);
#line hidden
    }
}
#line default
