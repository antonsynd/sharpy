#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OutInlineMultiple
{
    public static partial class OutInlineMultipleModule
    {
        public static bool ParsePair(string s, out int a, out int b)
        {
#line (2, 5) - (2, 11) 12 "out_inline_multiple.spy"
            a = 10;
#line (3, 5) - (3, 11) 12 "out_inline_multiple.spy"
            b = 20;
#line (4, 5) - (4, 17) 12 "out_inline_multiple.spy"
            return true;
#line hidden
        }

        public static void Main()
        {
#line (7, 5) - (7, 63) 12 "out_inline_multiple.spy"
            var success = global::OutInlineMultiple.OutInlineMultipleModule.ParsePair("x", out int first, out int second);
#line (8, 5) - (8, 34) 12 "out_inline_multiple.spy"
            global::Sharpy.Builtins.Print(success, first, second);
#line hidden
        }
    }
}
#line default
