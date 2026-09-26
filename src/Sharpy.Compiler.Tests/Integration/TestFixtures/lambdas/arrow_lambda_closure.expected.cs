#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ArrowLambdaClosure
{
    public static partial class ArrowLambdaClosureModule
    {
        public static global::System.Func<int, int> MakeAdder(int n)
        {
#line (2, 5) - (2, 30) 12 "arrow_lambda_closure.spy"
            return (int x) => x + n;
#line hidden
        }

        public static void Main()
        {
#line (5, 5) - (5, 25) 12 "arrow_lambda_closure.spy"
            global::System.Func<int, int> add5 = global::ArrowLambdaClosure.ArrowLambdaClosureModule.MakeAdder(5);
#line (6, 5) - (6, 27) 12 "arrow_lambda_closure.spy"
            global::System.Func<int, int> add10 = global::ArrowLambdaClosure.ArrowLambdaClosureModule.MakeAdder(10);
#line (7, 5) - (7, 19) 12 "arrow_lambda_closure.spy"
            global::Sharpy.Builtins.Print(add5(3));
#line (8, 5) - (8, 20) 12 "arrow_lambda_closure.spy"
            global::Sharpy.Builtins.Print(add10(3));
#line hidden
        }
    }
}
#line default
