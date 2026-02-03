#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LogicalOperatorsBool
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "logical_operators_bool.spy"
            bool a = true;
#line 4 "logical_operators_bool.spy"
            bool b = false;
#line 6 "logical_operators_bool.spy"
            bool result1 = a && b;
#line 7 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 9 "logical_operators_bool.spy"
            bool result2 = a || b;
#line 10 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 12 "logical_operators_bool.spy"
            bool result3 = !a;
#line 13 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result3);
#line 15 "logical_operators_bool.spy"
            bool result4 = !b;
#line 16 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result4);
#line 18 "logical_operators_bool.spy"
            int x = 10;
#line 19 "logical_operators_bool.spy"
            int y = 5;
#line 21 "logical_operators_bool.spy"
            bool result5 = x > 5 && y < 10;
#line 22 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result5);
#line 24 "logical_operators_bool.spy"
            bool result6 = x < 5 || y > 0;
#line 25 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result6);
#line 27 "logical_operators_bool.spy"
            bool result7 = !(x == y);
#line 28 "logical_operators_bool.spy"
            global::Sharpy.Core.Exports.Print(result7);
        }
    }
}
