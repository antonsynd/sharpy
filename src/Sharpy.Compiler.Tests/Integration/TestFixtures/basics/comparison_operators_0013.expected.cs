#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ComparisonOperators0013
{
    public static class Program
    {
        public static int A = 10;
        public static int B = 20;
        public static int C = 10;
        public static void Main()
        {
#line 9 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A == C);
#line 10 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A == B);
#line 11 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A != B);
#line 14 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A < B);
#line 15 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(B > A);
#line 16 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A > B);
#line 19 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A <= C);
#line 20 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(A >= C);
#line 21 "comparison_operators_0013.spy"
            global::Sharpy.Core.Exports.Print(B <= A);
        }
    }
}
