#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class OutInlineInIf
{
    public static bool TryParse(string s, out int result)
    {
#line (2, 5) - (2, 20) 1 "out_inline_in_if.spy"
        result = global::Sharpy.Builtins.Int(s);
#line (3, 5) - (3, 17) 1 "out_inline_in_if.spy"
        return true;
    }

    public static void Main()
    {
#line (6, 5) - (8, 1) 1 "out_inline_in_if.spy"
        if (TryParse("42", out int value))
        {
#line (7, 9) - (7, 21) 1 "out_inline_in_if.spy"
            global::Sharpy.Builtins.Print(value);
        }
    }
}
