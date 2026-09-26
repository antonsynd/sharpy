#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OutInlineAuto
{
    public static partial class OutInlineAutoModule
    {
        public static bool TryParse(string s, out int result)
        {
#line (2, 5) - (2, 20) 12 "out_inline_auto.spy"
            result = global::Sharpy.Builtins.Int(s);
#line (3, 5) - (3, 17) 12 "out_inline_auto.spy"
            return true;
#line hidden
        }

        public static void Main()
        {
#line (6, 5) - (6, 47) 12 "out_inline_auto.spy"
            var success = global::OutInlineAuto.OutInlineAutoModule.TryParse("42", out var value);
#line (7, 5) - (7, 26) 12 "out_inline_auto.spy"
            global::Sharpy.Builtins.Print(success, value);
#line hidden
        }
    }
}
#line default
