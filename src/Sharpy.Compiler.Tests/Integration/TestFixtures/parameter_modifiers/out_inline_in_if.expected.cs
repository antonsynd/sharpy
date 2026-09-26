#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OutInlineInIf
{
    public static partial class OutInlineInIfModule
    {
        public static bool TryParse(string s, out int result)
        {
#line (2, 5) - (2, 20) 12 "out_inline_in_if.spy"
            result = global::Sharpy.Builtins.Int(s);
#line (3, 5) - (3, 17) 12 "out_inline_in_if.spy"
            return true;
#line hidden
        }

        public static void Main()
        {
#line (6, 5) - (7, 21) 12 "out_inline_in_if.spy"
            if (global::OutInlineInIf.OutInlineInIfModule.TryParse("42", out int value))
#line hidden
            {
#line (7, 9) - (7, 21) 16 "out_inline_in_if.spy"
                global::Sharpy.Builtins.Print(value);
#line hidden
            }
        }
    }
}
#line default
