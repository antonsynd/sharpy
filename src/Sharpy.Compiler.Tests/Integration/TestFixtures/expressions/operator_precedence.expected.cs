#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class OperatorPrecedence
{
    public static void Main()
    {
#line (4, 5) - (4, 23) 1 "operator_precedence.spy"
        global::Sharpy.Builtins.Print((int)(global::System.Math.Pow(2, (int)(global::System.Math.Pow(3, 2)))));
#line (6, 5) - (6, 21) 1 "operator_precedence.spy"
        global::Sharpy.Builtins.Print(2 + 3 * 4);
#line (8, 5) - (8, 23) 1 "operator_precedence.spy"
        global::Sharpy.Builtins.Print((2 + 3) * 4);
#line (10, 5) - (10, 26) 1 "operator_precedence.spy"
        int? x = null;
#line (11, 5) - (11, 25) 1 "operator_precedence.spy"
        int y = x ?? 5 + 3;
#line (12, 5) - (12, 13) 1 "operator_precedence.spy"
        global::Sharpy.Builtins.Print(y);
    }
}
