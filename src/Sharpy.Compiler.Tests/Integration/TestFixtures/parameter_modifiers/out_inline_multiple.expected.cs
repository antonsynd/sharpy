#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class OutInlineMultiple
{
    public static bool ParsePair(string s, out int a, out int b)
    {
#line (2, 5) - (2, 11) 1 "out_inline_multiple.spy"
        a = 10;
#line (3, 5) - (3, 11) 1 "out_inline_multiple.spy"
        b = 20;
#line (4, 5) - (4, 17) 1 "out_inline_multiple.spy"
        return true;
    }

    public static void Main()
    {
#line (7, 5) - (7, 63) 1 "out_inline_multiple.spy"
        var success = ParsePair("x", out int first, out int second);
#line (8, 5) - (8, 34) 1 "out_inline_multiple.spy"
        global::Sharpy.Builtins.Print(success, first, second);
    }
}
