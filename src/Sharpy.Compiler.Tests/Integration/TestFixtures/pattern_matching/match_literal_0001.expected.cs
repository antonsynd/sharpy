#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchLiteral0001
{
    public static void Main()
    {
#line (2, 5) - (2, 21) 8 "match_literal_0001.spy"
        int value = 42;
#line (3, 5) - (10, 1) 8 "match_literal_0001.spy"
        switch (value)
#line hidden
        {
            case 1:
#line (5, 13) - (5, 25) 16 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print("one");
#line hidden
                break;
            case 42:
#line (7, 13) - (7, 31) 16 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print("forty-two");
#line hidden
                break;
            default:
#line (9, 13) - (9, 27) 16 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print("other");
#line hidden
                break;
        }
    }
}
#line default
