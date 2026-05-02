#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class OutInlineDeclaration
{
    public static bool TryParse(string s, out int result)
    {
#line (2, 5) - (2, 20) 1 "out_inline_declaration.spy"
        result = global::Sharpy.Builtins.Int(s);
#line (3, 5) - (3, 17) 1 "out_inline_declaration.spy"
        return true;
    }

    public static void Main()
    {
#line (6, 5) - (6, 46) 1 "out_inline_declaration.spy"
        var success = TryParse("42", out int value);
#line (7, 5) - (7, 26) 1 "out_inline_declaration.spy"
        global::Sharpy.Builtins.Print(success, value);
    }
}
