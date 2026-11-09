// Snapshot: a None lambda body against a void delegate elides to an empty block on all three paths (#1514): untyped `() =>`, typed `(int n) =>`, hoisted `void h(int n = 3)`
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace LambdaNoneVoidDelegate1514
{
    public static partial class LambdaNoneVoidDelegate1514Module
    {
        public static void Main()
        {
#line (6, 5) - (6, 21) 12 "lambda_none_void_delegate_1514.spy"
            global::System.Action f = () =>
#line hidden
            {
            };
#line (7, 5) - (7, 8) 12 "lambda_none_void_delegate_1514.spy"
            f();
#line (8, 5) - (8, 28) 12 "lambda_none_void_delegate_1514.spy"
            global::System.Action<int> g = (int n) =>
#line hidden
            {
            };
#line (9, 5) - (9, 9) 12 "lambda_none_void_delegate_1514.spy"
            g(5);
#line (10, 5) - (10, 32) 12 "lambda_none_void_delegate_1514.spy"
            void h(int n = 3)
#line hidden
            {
            }

#line (11, 5) - (11, 8) 12 "lambda_none_void_delegate_1514.spy"
            h();
#line (12, 5) - (12, 9) 12 "lambda_none_void_delegate_1514.spy"
            h(7);
#line (13, 5) - (13, 16) 12 "lambda_none_void_delegate_1514.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }
}
#line default
