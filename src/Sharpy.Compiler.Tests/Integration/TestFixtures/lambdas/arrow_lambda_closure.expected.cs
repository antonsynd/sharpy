#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ArrowLambdaClosure
{
    public static System.Func<int, int> MakeAdder(int n)
    {
#line (2, 5) - (2, 30) 1 "arrow_lambda_closure.spy"
        return (int x) => x + n;
    }

    public static void Main()
    {
#line (5, 5) - (5, 25) 1 "arrow_lambda_closure.spy"
        System.Func<int, int> add5 = MakeAdder(5);
#line (6, 5) - (6, 27) 1 "arrow_lambda_closure.spy"
        System.Func<int, int> add10 = MakeAdder(10);
#line (7, 5) - (7, 19) 1 "arrow_lambda_closure.spy"
        global::Sharpy.Builtins.Print(add5(3));
#line (8, 5) - (8, 20) 1 "arrow_lambda_closure.spy"
        global::Sharpy.Builtins.Print(add10(3));
    }
}
