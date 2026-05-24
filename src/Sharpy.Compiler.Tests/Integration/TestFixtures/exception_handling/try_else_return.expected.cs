#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class TryElseReturn
{
    public static string SafeDivide(int a, int b)
    {
#line (2, 5) - (2, 21) 1 "try_else_return.spy"
        int result = 0;
#line (3, 5) - (3, 20) 1 "try_else_return.spy"
        int total = 0;
#line (4, 5) - (12, 1) 1 "try_else_return.spy"
        {
            bool __trySucceeded_0 = false;
            try
            {
#line (5, 9) - (5, 19) 1 "try_else_return.spy"
                result = a;
#line (6, 9) - (6, 27) 1 "try_else_return.spy"
                total = result + b;
                __trySucceeded_0 = true;
            }
            catch (Exception)
            {
#line (8, 9) - (8, 24) 1 "try_else_return.spy"
                return "error";
            }

            if (__trySucceeded_0)
            {
#line (10, 9) - (10, 35) 1 "try_else_return.spy"
                return FormattableString.Invariant($"result: {(total)}");
            }

            throw new global::System.InvalidOperationException("unreachable");
        }
    }

    public static void Main()
    {
#line (13, 5) - (13, 30) 1 "try_else_return.spy"
        global::Sharpy.Builtins.Print(SafeDivide(10, 5));
#line (14, 5) - (14, 29) 1 "try_else_return.spy"
        global::Sharpy.Builtins.Print(SafeDivide(3, 7));
    }
}
